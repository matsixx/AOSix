using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AOSix.Source
{
    // Swaps EFT's Custom Ambient shader (AmbientLight._screenAmbientMaterial) for the AO-aware port. The
    // material is recreated on every AmbientLight.Initialize, so the swap is re-applied from its postfix; the
    // live instances are remembered from there for config toggles (no scene scans).
    internal static class AoAmbient
    {
        private static readonly FieldInfo MatField = AccessTools.Field(typeof(AmbientLight), "_screenAmbientMaterial");
        private static readonly List<AmbientLight> Live = new List<AmbientLight>();

        public static void Register(AmbientLight al)
        {
            if (!Live.Contains(al)) Live.Add(al);
            Apply(al);
        }

        public static void ApplyAll()
        {
            Live.RemoveAll(a => a == null);
            foreach (AmbientLight al in Live) Apply(al);
        }

        private static void Apply(AmbientLight al)
        {
            if (MatField?.GetValue(al) is not Material mat) return;
            bool ours = AoConfig.Enabled.Value && AoConfig.OccludeCustomAmbient.Value
                        && AoAssets.Load() && AoAssets.ScreenAmbient != null;
            Shader target = ours ? AoAssets.ScreenAmbient : al.ScreenAmbientShader;
            if (target != null && mat.shader != target)
            {
                mat.shader = target;
                if (AoConfig.Debug.Value) Plugin.MyLog.LogInfo("[AOSix] Custom Ambient shader -> " + target.name);
            }
        }
    }
}
