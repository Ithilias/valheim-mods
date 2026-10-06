using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SnappySync
{
    /// <summary>
    /// Captures the delta time the game passed into ClientSync so the smoothing below can use it
    /// instead of Time.deltaTime.
    ///
    /// This matters: Unity returns Time.fixedDeltaTime from Time.deltaTime when it is read inside
    /// FixedUpdate, which is where vanilla drives ClientSync from. Reading Time.deltaTime there
    /// yields a constant 0.02 no matter the real frame time, which would quietly turn the
    /// exponential below back into a fixed per-step factor, exactly the frame-rate dependence this
    /// mod exists to remove. Taking the argument works on whichever path calls us.
    /// </summary>
    [HarmonyPatch(typeof(ZSyncTransform), "ClientSync")]
    internal static class ClientSyncDeltaPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(float dt)
        {
            if (RenderRateSync.IsReentrant)
            {
                // Our own LateUpdate pass, so dt is already the real frame time.
                Smoothing.CurrentDt = dt;
                return;
            }

            // The physics-tick path. ClientSync is capped to once per rendered frame, so the true
            // interval between two runs is whichever is longer: the fixed timestep, or the frame.
            // Below 50 fps the frame wins, and taking dt at face value there is what made the
            // easing slower than configured for exactly the players who could least afford it.
            Smoothing.CurrentDt = Mathf.Max(dt, Smoothing.LastFrameTime);
        }
    }

    [HarmonyPatch]
    internal static class Smoothing
    {
        /// <summary>Delta time for the sync pass currently running. Set by ClientSyncDeltaPatch.</summary>
        internal static float CurrentDt;

        /// <summary>
        /// Length of the last rendered frame, sampled in Plugin.LateUpdate where Time.deltaTime
        /// really is the frame time. Read from FixedUpdate it would report the fixed timestep.
        /// </summary>
        internal static float LastFrameTime;

        internal static long Smoothed;
        internal static long Snapped;

        /// <summary>
        /// Both smoothing paths. ZSyncTransform.ClientSync handles kinematic bodies inline (one
        /// Vector3.Lerp into Rigidbody.MovePosition); everything else routes through SyncPosition,
        /// which has two more Lerps into Transform.set_position. Creatures are non-kinematic while
        /// awake, so they take the SyncPosition path: patching only ClientSync does nothing for
        /// them.
        ///
        /// Rotation is deliberately left alone. Both rotation paths already use Quaternion.Slerp
        /// at 0.5 per step against position's 0.2, and the kinematic path does not smooth rotation
        /// at all. Position was the laggard; speeding it up brings the two into line.
        /// </summary>
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodBase clientSync = AccessTools.Method(typeof(ZSyncTransform), "ClientSync");
            MethodBase syncPos = AccessTools.Method(typeof(ZSyncTransform), "SyncPosition");
            if (clientSync != null) yield return clientSync;
            if (syncPos != null) yield return syncPos;
        }

        /// <summary>
        /// Drop-in replacement for the Vector3.Lerp calls that ease a world position toward its
        /// received value. Same signature, so the transpiler only swaps the method reference and
        /// the evaluation stack is untouched.
        /// </summary>
        internal static Vector3 CatchUp(Vector3 current, Vector3 target, float vanillaT)
        {
            if (!Plugin.Enabled.Value) return Vector3.Lerp(current, target, vanillaT);

            // The game already teleports past 5 m before reaching either call site, so this only
            // fires when Snap Distance has been lowered below vanilla.
            if (Vector3.Distance(current, target) >= Plugin.SnapDistance.Value)
            {
                Snapped++;
                return target;
            }

            return Approach(current, target, vanillaT);
        }

        /// <summary>
        /// The same easing without the teleport, for the one call site that smooths a position
        /// relative to a parent (a passenger on a moving ship). Vanilla has no distance cutoff
        /// there at all, so applying one would introduce a pop that the game never had.
        /// </summary>
        internal static Vector3 CatchUpNoSnap(Vector3 current, Vector3 target, float vanillaT)
        {
            if (!Plugin.Enabled.Value) return Vector3.Lerp(current, target, vanillaT);
            return Approach(current, target, vanillaT);
        }

        /// <summary>
        /// Exponential approach against real elapsed time: the same wall-clock time constant
        /// whether the player runs at 50 or 144 fps. Vanilla's fixed 0.2 per step does not have
        /// this property.
        /// </summary>
        private static Vector3 Approach(Vector3 current, Vector3 target, float vanillaT)
        {
            float dt = CurrentDt;
            if (dt <= 0f) return current;

            Smoothed++;
            float t = 1f - Mathf.Exp(-Plugin.Rate.Value * dt);
            return Vector3.Lerp(current, target, Mathf.Clamp01(t));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);

            MethodInfo lerp = AccessTools.Method(typeof(Vector3), nameof(Vector3.Lerp),
                                                 new[] { typeof(Vector3), typeof(Vector3), typeof(float) });
            MethodInfo snapping = AccessTools.Method(typeof(Smoothing), nameof(CatchUp));
            MethodInfo plain = AccessTools.Method(typeof(Smoothing), nameof(CatchUpNoSnap));

            if (lerp == null || snapping == null || plain == null)
            {
                Plugin.Log.LogError("Could not resolve Vector3.Lerp - leaving vanilla smoothing alone.");
                return codes;
            }

            // In SyncPosition the first Lerp is the parent-relative one and the second is the
            // world-position one. Ordering is how they are told apart; if the game ever reorders
            // them the worst case is that the relative path gains a teleport it did not have.
            bool isSyncPosition = original.Name == "SyncPosition";

            int seen = 0;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode != OpCodes.Call) continue;
                if (!(codes[i].operand is MethodInfo m) || m != lerp) continue;

                bool relative = isSyncPosition && seen == 0;
                codes[i].operand = relative ? plain : snapping;
                seen++;
            }

            if (seen == 0)
            {
                Plugin.Log.LogError($"No Vector3.Lerp found in {original.Name} - the game changed. " +
                                    "Vanilla smoothing is unchanged for it.");
            }
            else
            {
                Plugin.Log.LogInfo($"Patched {seen} position-smoothing call(s) in {original.Name} " +
                                   "(expect 1 in ClientSync, 2 in SyncPosition).");
            }

            return codes;
        }
    }
}
