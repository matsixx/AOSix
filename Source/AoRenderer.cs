using EFT.Settings.Graphics;
using UnityEngine;
using UnityEngine.Rendering;

namespace AOSix.Source
{
    // Drives the AO command buffer on the main camera. The buffer itself is static (temporaries sized off the
    // camera, loop counts from uniforms) and only rebuilt when HDR / debug / the camera event changes; the
    // per-eye matrices and knobs go in as globals from OnPreRender, which fires per eye in VR multipass
    // right before that eye's render executes the buffer.
    internal class AoRenderer : MonoBehaviour
    {
        public static ESSAOMode Mode = ESSAOMode.HighQuality;

        private const int PassDepth0 = 0, PassDown = 1, PassTrace = 2, PassBlurH = 3, PassBlurV = 4,
                          PassApply = 5, PassApplyLdr = 6, PassDebug = 7;

        private static readonly int IdGb0 = Shader.PropertyToID("_AOSix_GBuffer0");
        private static readonly int IdGb2 = Shader.PropertyToID("_AOSix_GBuffer2");
        private static readonly int IdD0 = Shader.PropertyToID("_AOSix_Depth0");
        private static readonly int IdD1 = Shader.PropertyToID("_AOSix_Depth1");
        private static readonly int IdD2 = Shader.PropertyToID("_AOSix_Depth2");
        private static readonly int IdD3 = Shader.PropertyToID("_AOSix_Depth3");
        private static readonly int IdDSrc = Shader.PropertyToID("_AOSix_DepthSrc");
        private static readonly int IdRaw = Shader.PropertyToID("_AOSix_AORaw");
        private static readonly int IdTmp = Shader.PropertyToID("_AOSix_AOTmp");
        private static readonly int IdBlurSrc = Shader.PropertyToID("_AOSix_BlurSrc");
        private static readonly int IdFinal = Shader.PropertyToID("_AOSix_AOFinal");
        private static readonly int IdUVToView = Shader.PropertyToID("_AOSix_UVToView");
        private static readonly int IdWorldToView = Shader.PropertyToID("_AOSix_WorldToView");
        private static readonly int IdParams = Shader.PropertyToID("_AOSix_Params");
        private static readonly int IdParams2 = Shader.PropertyToID("_AOSix_Params2");
        private static readonly int IdParams3 = Shader.PropertyToID("_AOSix_Params3");
        private static readonly int IdActive = Shader.PropertyToID("_AOSix_AOActive");

        private static Material _mat;

        private Camera _cam;
        private CommandBuffer _cb;
        private CommandBuffer _cbEnd;   // end of the camera render: clears the "AO valid" flag
        // The finished AO. A PERSISTENT texture, not a command-buffer temporary: it is read again by EFT's
        // Custom Ambient pass (AfterLighting), and a temporary shared across events resolved to a scene
        // buffer there (the whole image took on the scene's green). Same pattern as AmbientLight's own
        // _StencilShadow mask, written at BeforeLighting and read by that same pass.
        private RenderTexture _aoFinal;
        private bool _attached;
        private CameraEvent _evt;
        private int _builtKey = -1;
        private float _nextOrderCheck, _nextLog;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        private void OnDisable() => Detach();

        private void OnDestroy()
        {
            Detach();
            _cb?.Release();
            _cb = null;
            _cbEnd?.Release();
            _cbEnd = null;
            if (_aoFinal != null)
            {
                _aoFinal.Release();
                Destroy(_aoFinal);
                _aoFinal = null;
            }
        }

        private void EnsureAoTexture()
        {
            int w = Mathf.Max(1, _cam.pixelWidth), h = Mathf.Max(1, _cam.pixelHeight);
            if (_aoFinal != null && (_aoFinal.width != w || _aoFinal.height != h))
            {
                _aoFinal.Release();
                Destroy(_aoFinal);
                _aoFinal = null;
            }
            if (_aoFinal == null)
            {
                _aoFinal = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                {
                    name = "AOSix AO", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
                };
                _aoFinal.Create();
                _builtKey = -1;   // the command buffer targets it
            }
            // A real global, set from C#: every pass this frame (ours and the Custom Ambient) reads this one.
            Shader.SetGlobalTexture(IdFinal, _aoFinal);
        }

        private void OnPreRender()
        {
            if (!AoConfig.Enabled.Value || Mode == ESSAOMode.Off || _cam.actualRenderingPath != RenderingPath.DeferredShading
                || !EnsureMaterial())
            {
                Detach();
                return;
            }

            if ((_cam.depthTextureMode & DepthTextureMode.Depth) == 0)
                _cam.depthTextureMode |= DepthTextureMode.Depth;

            bool deferredReflections = GraphicsSettings.GetShaderMode(BuiltinShaderType.DeferredReflections) != BuiltinShaderMode.Disabled;
            CameraEvent evt = deferredReflections ? CameraEvent.BeforeReflections : CameraEvent.BeforeLighting;
            bool hdr = _cam.allowHDR;
            bool debug = AoConfig.ShowAO.Value && hdr;
            EnsureAoTexture();
            int key = (hdr ? 1 : 0) | (debug ? 2 : 0) | ((int)evt << 2);
            if (key != _builtKey || !_attached)
            {
                Detach();
                Build(hdr, debug);
                _evt = evt;
                _cam.AddCommandBuffer(_evt, _cb);
                _cam.AddCommandBuffer(CameraEvent.AfterEverything, _cbEnd);
                _attached = true;
                _builtKey = key;
            }

            SetGlobals();
            KeepLast();
        }

        private static bool EnsureMaterial()
        {
            if (_mat != null) return true;
            if (!AoAssets.Load()) return false;
            _mat = new Material(AoAssets.Gtao) { hideFlags = HideFlags.DontSave };
            return true;
        }

        private void Detach()
        {
            if (_attached && _cam != null && _cb != null)
            {
                _cam.RemoveCommandBuffer(_evt, _cb);
                _cam.RemoveCommandBuffer(CameraEvent.AfterEverything, _cbEnd);
            }
            _attached = false;
        }

        private void Build(bool hdr, bool debug)
        {
            if (_cb == null) _cb = new CommandBuffer { name = "AOSix" };
            if (_cbEnd == null)
            {
                _cbEnd = new CommandBuffer { name = "AOSix End" };
                _cbEnd.SetGlobalFloat(IdActive, 0f);
            }
            CommandBuffer cb = _cb;
            cb.Clear();

            cb.SetGlobalTexture(IdGb0, BuiltinRenderTextureType.GBuffer0);
            cb.SetGlobalTexture(IdGb2, BuiltinRenderTextureType.GBuffer2);

            // Linear depth pyramid (-N = camera size / N). Far samples read coarse levels: cache-friendly at
            // large screen radii (hands/gun close to the camera).
            cb.GetTemporaryRT(IdD0, -1, -1, 0, FilterMode.Point, RenderTextureFormat.RFloat);
            cb.GetTemporaryRT(IdD1, -2, -2, 0, FilterMode.Point, RenderTextureFormat.RFloat);
            cb.GetTemporaryRT(IdD2, -4, -4, 0, FilterMode.Point, RenderTextureFormat.RFloat);
            cb.GetTemporaryRT(IdD3, -8, -8, 0, FilterMode.Point, RenderTextureFormat.RFloat);
            Draw(cb, IdD0, PassDepth0);
            cb.SetGlobalTexture(IdDSrc, IdD0);
            Draw(cb, IdD1, PassDown);
            cb.SetGlobalTexture(IdDSrc, IdD1);
            Draw(cb, IdD2, PassDown);
            cb.SetGlobalTexture(IdDSrc, IdD2);
            Draw(cb, IdD3, PassDown);

            cb.GetTemporaryRT(IdRaw, -1, -1, 0, FilterMode.Point, RenderTextureFormat.RGHalf);
            cb.GetTemporaryRT(IdTmp, -1, -1, 0, FilterMode.Point, RenderTextureFormat.RGHalf);
            Draw(cb, IdRaw, PassTrace);
            cb.SetGlobalTexture(IdBlurSrc, IdRaw);
            Draw(cb, IdTmp, PassBlurH);
            cb.SetGlobalTexture(IdBlurSrc, IdTmp);
            cb.SetRenderTarget(_aoFinal);
            cb.DrawProcedural(Matrix4x4.identity, _mat, PassBlurV, MeshTopology.Triangles, 3);
            cb.SetGlobalTexture(IdFinal, _aoFinal);

            if (debug)
            {
                var mrt = new RenderTargetIdentifier[] { BuiltinRenderTextureType.GBuffer0, BuiltinRenderTextureType.GBuffer1, BuiltinRenderTextureType.CameraTarget };
                cb.SetRenderTarget(mrt, mrt[0]);
                cb.DrawProcedural(Matrix4x4.identity, _mat, PassDebug, MeshTopology.Triangles, 3);
            }
            else if (hdr)
            {
                var mrt = new RenderTargetIdentifier[] { BuiltinRenderTextureType.GBuffer0, BuiltinRenderTextureType.CameraTarget };
                cb.SetRenderTarget(mrt, mrt[0]);
                cb.DrawProcedural(Matrix4x4.identity, _mat, PassApply, MeshTopology.Triangles, 3);
            }
            else
            {
                cb.SetRenderTarget(BuiltinRenderTextureType.GBuffer0);
                cb.DrawProcedural(Matrix4x4.identity, _mat, PassApplyLdr, MeshTopology.Triangles, 3);
            }

            cb.ReleaseTemporaryRT(IdD0);
            cb.ReleaseTemporaryRT(IdD1);
            cb.ReleaseTemporaryRT(IdD2);
            cb.ReleaseTemporaryRT(IdD3);
            cb.ReleaseTemporaryRT(IdRaw);
            cb.ReleaseTemporaryRT(IdTmp);
            // _AOSix_AOFinal is valid from here to the end of this camera's render (the Custom Ambient pass at
            // AfterLighting reads it, gated on this flag; _cbEnd clears it).
            cb.SetGlobalFloat(IdActive, 1f);
            cb.SetRenderTarget(default(RenderTexture));   // as Amplify's deferred path (proven in VR + flat)
        }

        private static void Draw(CommandBuffer cb, int target, int pass)
        {
            cb.SetRenderTarget(target);
            cb.DrawProcedural(Matrix4x4.identity, _mat, pass, MeshTopology.Triangles, 3);
        }

        private void SetGlobals()
        {
            // VR eye frustums are off-axis (m02/m12 nonzero, opposite per eye); the stereo getters give this
            // eye's (the per-eye fix Amplify needed against AO swimming between eyes).
            Matrix4x4 proj, view;
            if (_cam.stereoEnabled)
            {
                var eye = _cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Right
                    ? Camera.StereoscopicEye.Right : Camera.StereoscopicEye.Left;
                proj = _cam.GetStereoProjectionMatrix(eye);
                view = _cam.GetStereoViewMatrix(eye);
            }
            else
            {
                proj = _cam.projectionMatrix;
                view = _cam.worldToCameraMatrix;
            }

            Quality(Mode, out int slices, out int steps, out bool colored);
            float maxDist = AoConfig.MaxDistance.Value;
            Shader.SetGlobalVector(IdUVToView, new Vector4(2f / proj.m00, 2f / proj.m11,
                (proj.m02 - 1f) / proj.m00, (proj.m12 - 1f) / proj.m11));
            Shader.SetGlobalMatrix(IdWorldToView, view);
            Shader.SetGlobalVector(IdParams, new Vector4(AoConfig.Radius.Value, AoConfig.Thickness.Value,
                0.5f * proj.m11, AoConfig.Intensity.Value));
            Shader.SetGlobalVector(IdParams2, new Vector4(0.35f * maxDist, 1f / (0.65f * maxDist), slices, steps));
            Shader.SetGlobalVector(IdParams3, new Vector4(colored ? 1f : 0f, AoConfig.MaxScreenRadius.Value * 0.01f,
                AoConfig.MinStep.Value, 0f));   // w: a world-space step floor - REMOVED (it erased grass AO; the road step is fixed in POMSix)

            if (AoConfig.Debug.Value && Time.unscaledTime >= _nextLog)
            {
                _nextLog = Time.unscaledTime + 5f;
                var names = new System.Text.StringBuilder();
                foreach (var b in _cam.GetCommandBuffers(_evt)) names.Append(b.name).Append(" | ");
                Plugin.MyLog.LogInfo($"[AOSix] mode={Mode} slices={slices} steps={steps} colored={colored} " +
                    $"hdr={_cam.allowHDR} eye={_cam.stereoActiveEye} px={_cam.pixelWidth}x{_cam.pixelHeight} " +
                    $"evt={_evt}: {names}");
            }
        }

        // Slices x steps-per-side. The 4x4 noise tile + blur turn these into 16x the directions.
        private static void Quality(ESSAOMode m, out int slices, out int steps, out bool colored)
        {
            colored = m == ESSAOMode.ColoredHighestQuality;
            switch (m)
            {
                case ESSAOMode.FastestPerformance: slices = 1; steps = 4; break;
                case ESSAOMode.FastPerformance: slices = 2; steps = 4; break;
                case ESSAOMode.HighQuality: slices = 2; steps = 6; break;
                default: slices = 3; steps = 8; break;   // Highest / Colored Highest
            }
        }

        // Run after every other buffer at our event, so the AO sees the finished GBuffer: EFT injects water
        // (and GPU Instancer its data) at BeforeReflections too, and buffers there run in the order they were
        // added. This replaces the old "re-enable AO 3s into the raid" winter-grass workaround.
        private void KeepLast()
        {
            if (Time.unscaledTime < _nextOrderCheck) return;
            _nextOrderCheck = Time.unscaledTime + 1f;
            CommandBuffer[] list = _cam.GetCommandBuffers(_evt);
            if (list.Length > 0 && list[list.Length - 1] != _cb)
            {
                _cam.RemoveCommandBuffer(_evt, _cb);
                _cam.AddCommandBuffer(_evt, _cb);
                if (AoConfig.Debug.Value)
                    Plugin.MyLog.LogInfo($"[AOSix] Re-ordered to run last at {_evt} (after {list[list.Length - 1].name})");
            }
        }
    }
}
