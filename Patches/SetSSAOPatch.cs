using AOSix.Source;
using EFT.CameraControl;
using EFT.Settings.Graphics;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace AOSix.Patches
{
    // The game's SSAO setting lands here. Enabled: turn off EFT's HBAO / PostProcessing AO and drive AOSix
    // from the chosen quality. Disabled: vanilla runs.
    internal class SetSSAOPatch : ModulePatch
    {
        private static readonly FieldInfo HbaoField = AccessTools.Field(typeof(CameraManager), "_hbao");
        private static readonly FieldInfo AoField = AccessTools.Field(typeof(CameraManager), "_ambientOcclusion");

        private static CameraManager _last;
        private static ESSAOMode _lastMode = ESSAOMode.HighQuality;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(CameraManager), nameof(CameraManager.SetSSAO));
        }

        [PatchPrefix]
        private static bool Prefix(CameraManager __instance, ESSAOMode ssaoMode)
        {
            _last = __instance;
            _lastMode = ssaoMode;
            AoRenderer.Mode = ssaoMode;
            if (!AoConfig.Enabled.Value || !AoAssets.Load()) return true;   // no bundle -> keep vanilla AO

            if (HbaoField?.GetValue(__instance) is Behaviour hbao) hbao.enabled = false;
            if (AoField?.GetValue(__instance) is Behaviour ao) ao.enabled = false;

            Camera cam = __instance.Camera;
            if (cam != null && cam.GetComponent<AoRenderer>() == null)
                cam.gameObject.AddComponent<AoRenderer>();
            return false;
        }

        // Enabled toggled live: re-run the setting so vanilla AO comes back / goes away.
        public static void Reapply()
        {
            if (_last != null) _last.SetSSAO(_lastMode);
        }
    }

    internal class AmbientLightInitPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(AmbientLight), nameof(AmbientLight.Initialize));
        }

        [PatchPostfix]
        private static void Postfix(AmbientLight __instance)
        {
            AoAmbient.Register(__instance);
        }
    }
}
