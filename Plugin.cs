using AOSix.Patches;
using AOSix.Source;
using BepInEx;
using BepInEx.Logging;

namespace AOSix
{
    [BepInPlugin("com.matsix.aosix", "AOSix", "2.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource MyLog;

        private void Awake()
        {
            MyLog = Logger;
            AoConfig.Bind(Config);

            new SetSSAOPatch().Enable();
            new AmbientLightInitPatch().Enable();

            AoConfig.Enabled.SettingChanged += (_, __) => { SetSSAOPatch.Reapply(); AoAmbient.ApplyAll(); };
            AoConfig.OccludeCustomAmbient.SettingChanged += (_, __) => AoAmbient.ApplyAll();

            MyLog.LogInfo("AOSix loaded!");
        }
    }
}
