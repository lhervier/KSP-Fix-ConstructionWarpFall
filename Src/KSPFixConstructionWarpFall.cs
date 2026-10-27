using System;
using HarmonyLib;
using UnityEngine;

namespace com.github.lhervier.ksp.fix.constructionwarpfall
{
    /// <summary>
    /// Installs the fix the settings turn on, once, when KSP starts: from then on, the physicsless parts no longer
    /// fall through their vessel when it comes off rails while a part is held in EVA construction mode.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class KSPFixConstructionWarpFall : MonoBehaviour
    {
        private const string HarmonyId = "com.github.lhervier.ksp.fix.constructionwarpfall";

        private void Start()
        {
            Settings.Load();
            Log.Info($"Version {typeof(KSPFixConstructionWarpFall).Assembly.GetName().Version}, " +
                $"physicslessPartsStayStill {Settings.PhysicslessPartsStayStill}, " +
                $"orphanRigidbodiesRemoved {Settings.OrphanRigidbodiesRemoved}, log level {Log.Level}");
            if (!Settings.PhysicslessPartsStayStill)
            {
                return;
            }
            try
            {
                PhysicslessPartsStayStill.Install(new Harmony(HarmonyId), Settings.OrphanRigidbodiesRemoved);
                Log.Info("Physicsless parts stay still fix installed");
            }
            catch (Exception e)
            {
                Log.Error("Could not install the physicsless parts stay still fix, they can still fall after a time " +
                    $"warp with a part held, as on stock: {e}");
            }
        }
    }
}
