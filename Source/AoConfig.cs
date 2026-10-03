using BepInEx.Configuration;

namespace AOSix.Source
{
    internal sealed class ConfigurationManagerAttributes { public bool? IsAdvanced; }

    internal static class AoConfig
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> Intensity;
        public static ConfigEntry<float> Radius;
        public static ConfigEntry<float> Thickness;
        public static ConfigEntry<float> MaxScreenRadius;
        public static ConfigEntry<float> MinStep;
        public static ConfigEntry<float> MaxDistance;
        public static ConfigEntry<bool> OccludeCustomAmbient;
        public static ConfigEntry<bool> ShowAO;
        public static ConfigEntry<bool> Debug;

        public static void Bind(ConfigFile cfg)
        {
            const string g = "1. General";
            Enabled = cfg.Bind(g, "Enabled", true,
                "Replace the game's SSAO with AOSix. Quality follows the in-game SSAO setting (Off = no AO; " +
                "Colored Highest Quality also tints the bounce light by surface color). Disabled = the game's own AO.");
            Intensity = cfg.Bind(g, "Intensity", 1.5f, new ConfigDescription(
                "AO strength. 1 = physically based; higher darkens contact shadows more.",
                new AcceptableValueRange<float>(0.25f, 4f)));
            Radius = cfg.Bind(g, "Radius", 1f, new ConfigDescription(
                "World-space reach of the occlusion, in meters.",
                new AcceptableValueRange<float>(0.2f, 4f)));
            Thickness = cfg.Bind(g, "Thickness", 0.5f, new ConfigDescription(
                "How thick an occluder is assumed to be, in meters. Lower lets light pass behind thin things " +
                "(grass, poles, railings); higher treats everything as solid.",
                new AcceptableValueRange<float>(0.05f, 3f)));
            MaxScreenRadius = cfg.Bind(g, "Max Screen Radius", 50f, new ConfigDescription(
                "Cap on the radius in screen space, as % of screen height. Keeps AO on close objects (gun, hands) " +
                "sampling their own features instead of the whole screen. Vanilla HBAO uses ~256 px (24% at 1080p).",
                new AcceptableValueRange<float>(5f, 100f)));
            MinStep = cfg.Bind(g, "Min Step Pixels", 0f, new ConfigDescription(
                "Depth steps smaller than this many pixels (at their distance) are not occluders. Hides the false " +
                "AO line where road/terrain layers meet (meshes floating a few cm apart, a few px tall at road " +
                "distance) while the same few cm on the gun, dozens of px, keep their shading. 0 = off.",
                new AcceptableValueRange<float>(0f, 32f)));
            MaxDistance = cfg.Bind(g, "Max Distance", 500f, new ConfigDescription(
                "AO fades out toward this distance (starting at 35% of it).",
                new AcceptableValueRange<float>(20f, 500f)));
            OccludeCustomAmbient = cfg.Bind(g, "Occlude Custom Ambient", true,
                "EFT adds its sky ambient in a late pass that ignores AO, which washes the AO out. On = that " +
                "ambient is occluded too. Off = the game's original pass (for comparison).");

            const string d = "2. Debug";
            ShowAO = cfg.Bind(d, "Show AO Only", false, new ConfigDescription(
                "Replace the scene with the AO term.", null, new ConfigurationManagerAttributes { IsAdvanced = true }));
            Debug = cfg.Bind(d, "Debug Log", false, new ConfigDescription(
                "Log the AO setup every few seconds.", null, new ConfigurationManagerAttributes { IsAdvanced = true }));
        }
    }
}
