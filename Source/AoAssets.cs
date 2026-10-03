using System.IO;
using UnityEngine;

namespace AOSix.Source
{
    // Shaders come from the `aosix` bundle next to the DLL (Assets/aosix). Kept loaded: unloading a bundle
    // unloads the shaders the live materials use.
    internal static class AoAssets
    {
        private static AssetBundle _bundle;
        private static bool _tried;

        public static Shader Gtao;
        public static Shader ScreenAmbient;

        public static bool Load()
        {
            if (_tried) return Gtao != null;
            _tried = true;

            string path = Path.Combine(Path.GetDirectoryName(typeof(AoAssets).Assembly.Location), "Assets", "aosix");
            if (!File.Exists(path))
            {
                Plugin.MyLog.LogError("[AOSix] Shader bundle not found at " + path + " - AO disabled.");
                return false;
            }
            _bundle = AssetBundle.LoadFromFile(path);
            if (_bundle == null)
            {
                Plugin.MyLog.LogError("[AOSix] Failed to load " + path + " - AO disabled.");
                return false;
            }
            foreach (Shader s in _bundle.LoadAllAssets<Shader>())
            {
                if (s.name == "Hidden/AOSix/GTAO") Gtao = s;
                else if (s.name == "Hidden/AOSix/ScreenAmbient") ScreenAmbient = s;
            }
            if (Gtao == null || !Gtao.isSupported)
            {
                Plugin.MyLog.LogError("[AOSix] GTAO shader missing or unsupported - AO disabled.");
                Gtao = null;
            }
            if (ScreenAmbient != null && !ScreenAmbient.isSupported)
            {
                Plugin.MyLog.LogError("[AOSix] ScreenAmbient shader unsupported - Custom Ambient stays vanilla.");
                ScreenAmbient = null;
            }
            Plugin.MyLog.LogInfo($"[AOSix] Loaded shaders: GTAO={Gtao != null} ScreenAmbient={ScreenAmbient != null}");
            return Gtao != null;
        }
    }
}
