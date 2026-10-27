using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace com.github.lhervier.ksp.fix.constructionphysicsless
{
    /// <summary>
    /// Keeps the physicsless parts in place when a vessel comes off rails while a part is held in EVA construction
    /// mode. While a part is held, the game gives every physicsless part in flight a kinematic Rigidbody of its own,
    /// with no joint and no collision with its own vessel. On stock, a vessel coming off rails (end of a time warp,
    /// entering the physics range) turns every Rigidbody non-kinematic: these parts then fall through their vessel to
    /// the ground, and stay where they fell once the part is released. Such a Rigidbody is kept kinematic.
    /// </summary>
    internal static class PhysicslessPartsStayStill
    {
        private static readonly MethodInfo OnCurrentMousePartChanged =
            AccessTools.Method(typeof(Part), "OnCurrentMousePartChanged");
        private static readonly MethodInfo Unpack = AccessTools.Method(typeof(Part), nameof(Part.Unpack));

        // Whether a Rigidbody the stock handler leaves behind, when the part is released with the panel already
        // closed, is removed as the stock handler does with the panel open, instead of being only kept kinematic.
        private static bool removeOrphans;

        // The Rigidbodies Part.OnCurrentMousePartChanged added to a physicsless part and has not destroyed yet.
        // Rigidbodies a part already had (a wheel demoted by the Move or Rotate tool keeps its own) are never listed.
        private static readonly HashSet<Rigidbody> Added = new HashSet<Rigidbody>();

        // Frame of the last purge of destroyed Rigidbodies from Added.
        private static int purgedAtFrame = -1;

        /// <summary>
        /// Patches the physicsless parts' handling of the held part, and their unpacking. With
        /// <paramref name="orphansRemoved"/>, the Rigidbodies the stock code leaves behind when the panel closes
        /// first are removed too.
        /// </summary>
        /// <exception cref="MissingMemberException">Part does not have the methods patched.</exception>
        public static void Install(Harmony harmony, bool orphansRemoved)
        {
            if (OnCurrentMousePartChanged == null || Unpack == null)
            {
                throw new MissingMemberException("Part.OnCurrentMousePartChanged or Part.Unpack");
            }
            removeOrphans = orphansRemoved;
            harmony.Patch(OnCurrentMousePartChanged,
                prefix: new HarmonyMethod(typeof(PhysicslessPartsStayStill), nameof(OnCurrentMousePartChangedPrefix)),
                postfix: new HarmonyMethod(typeof(PhysicslessPartsStayStill), nameof(OnCurrentMousePartChangedPostfix)));
            harmony.Patch(Unpack,
                postfix: new HarmonyMethod(typeof(PhysicslessPartsStayStill), nameof(UnpackPostfix)));
            GameEvents.onGameSceneLoadRequested.Add(scene => Added.Clear());
        }

        /// <summary>Tells the postfix whether the part had a Rigidbody before the stock handler ran.</summary>
        private static void OnCurrentMousePartChangedPrefix(Part __instance, out bool __state)
        {
            __state = __instance.rb != null;
        }

        /// <summary>
        /// Records the Rigidbody the stock handler has just added to a physicsless part, and forgets the one it
        /// destroys.
        /// </summary>
        private static void OnCurrentMousePartChangedPostfix(Part __instance, Part p, bool __state)
        {
            // Every part in flight runs this handler for the same event: one purge per frame is enough.
            if (purgedAtFrame != Time.frameCount)
            {
                purgedAtFrame = Time.frameCount;
                Added.RemoveWhere(r => r == null);
            }
            Rigidbody rb = __instance.rb;
            if (rb == null)
            {
                return;
            }
            if (p != null)
            {
                if (!__state && __instance.physicalSignificance == Part.PhysicalSignificance.NONE)
                {
                    Added.Add(rb);
                }
                return;
            }
            // Nothing held any more. The stock handler destroys the Rigidbody (Destroy is deferred, rb is not null
            // yet) only when the panel is open in Construction mode; otherwise it leaves it in place, and it stays
            // listed so that it is still kept kinematic.
            if (StockDestroysOnRelease(__instance))
            {
                Added.Remove(rb);
                return;
            }
            if (!removeOrphans || !Added.Contains(rb) ||
                __instance.physicalSignificance != Part.PhysicalSignificance.NONE)
            {
                return;
            }
            // The stock clean-up, which the stock handler skipped (panel already closed).
            UnityEngine.Object.Destroy(rb);
            if (__instance.vessel != null)
            {
                CollisionManager.SetCollidersOnVessel(__instance.vessel, false, __instance.GetPartColliders());
            }
            Added.Remove(rb);
            if (Log.IsDebugEnabled)
            {
                Log.Debug($"{__instance.partInfo.title} ({__instance.flightID}) orphan Rigidbody removed: the held " +
                    "part was released with the construction panel already closed");
            }
        }

        /// <summary>
        /// Whether Part.OnCurrentMousePartChanged(null) destroys the Rigidbody of <paramref name="part"/>: same
        /// conditions as the stock handler.
        /// </summary>
        private static bool StockDestroysOnRelease(Part part)
        {
            EVAConstructionModeController controller = EVAConstructionModeController.Instance;
            return HighLogic.LoadedSceneIsFlight
                && controller != null
                && controller.IsOpen
                && controller.panelMode == EVAConstructionModeController.PanelMode.Construction
                && part.physicalSignificance == Part.PhysicalSignificance.NONE;
        }

        /// <summary>
        /// Turns back to kinematic the Rigidbody the held part gave this physicsless part, which Part.Unpack has just
        /// made non-kinematic.
        /// </summary>
        private static void UnpackPostfix(Part __instance)
        {
            Rigidbody rb = __instance.rb;
            if (rb == null || !Added.Contains(rb))
            {
                return;
            }
            // Made physical since (ResetToPhysicalPart reuses the Rigidbody through GetComponent): it is now the
            // part's own body, left to the stock code.
            if (__instance.physicalSignificance != Part.PhysicalSignificance.NONE)
            {
                Added.Remove(rb);
                return;
            }
            // Part.Unpack leaves the Rigidbody of a dead part alone.
            if (__instance.State == PartStates.DEAD)
            {
                return;
            }
            rb.isKinematic = true;
            if (Log.IsDebugEnabled)
            {
                Log.Debug($"{__instance.partInfo.title} ({__instance.flightID}) kept kinematic after unpacking: " +
                    "a part is held in EVA construction mode");
            }
        }
    }
}
