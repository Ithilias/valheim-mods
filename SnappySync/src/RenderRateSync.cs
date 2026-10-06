using System;
using HarmonyLib;
using UnityEngine;

namespace SnappySync
{
    /// <summary>
    /// Moves the receiving-side catch-up off the physics tick and onto the render loop.
    ///
    /// Vanilla drives ZSyncTransform.ClientSync from MonoUpdaters.FixedUpdate. ClientSync guards
    /// itself with "did I already run this rendered frame", which reads like the cadence but is
    /// only a ceiling: the actual rate is min(fps, 1/fixedDeltaTime). Nothing in the game assigns
    /// Time.fixedDeltaTime, so it is Unity's default 50 Hz, and every player above 50 fps
    /// converges at exactly 50 Hz. Rendering more frames buys nothing.
    ///
    /// MonoUpdaters.LateUpdate already walks the same ZSyncTransform.Instances list to call
    /// CustomLateUpdate, which only runs OwnerSync and so does nothing at all for an object you do
    /// not own. That is a free per-frame hook on exactly the right component. We skip the fixed
    /// tick and drive the same public entry point from there instead, once per rendered frame.
    ///
    /// Objects with a kinematic rigidbody are left on the vanilla path. Their branch ends in
    /// Rigidbody.MovePosition, which is a physics call and belongs on the physics tick. Creatures
    /// are non-kinematic while awake (Character only sets isKinematic while its AI sleeps), so the
    /// case this mod exists for is covered.
    /// </summary>
    internal static class RenderRateSync
    {
        private static AccessTools.FieldRef<ZSyncTransform, bool> s_isKinematic;
        private static bool s_ready;

        /// <summary>Set while we are the ones calling CustomFixedUpdate, so our own skip lets it through.</summary>
        private static bool s_reentrant;

        internal static bool Active => s_ready && Plugin.Enabled.Value && Plugin.RenderRate.Value;

        internal static void Init()
        {
            try
            {
                s_isKinematic = AccessTools.FieldRefAccess<ZSyncTransform, bool>("m_isKinematicBody");
                s_ready = s_isKinematic != null;
            }
            catch (Exception e)
            {
                s_ready = false;
                Plugin.Log.LogError("Could not reach ZSyncTransform.m_isKinematicBody, so render " +
                                    "rate smoothing is off and the vanilla physics-tick cadence " +
                                    $"stays in place: {e.Message}");
            }
        }

        /// <summary>True when this instance's catch-up is ours to drive rather than the fixed tick's.</summary>
        internal static bool Handles(ZSyncTransform sync)
        {
            if (!Active) return false;

            try
            {
                return !s_isKinematic(sync);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Re-entrancy is what keeps this honest: CustomFixedUpdate is public and already does the
        /// ZNetView validity check, so we reuse it rather than reaching for the private ClientSync.
        /// The flag tells our own prefix to stand aside for this one call.
        /// </summary>
        internal static void Run(ZSyncTransform sync)
        {
            s_reentrant = true;
            try
            {
                sync.CustomFixedUpdate(Time.deltaTime);
            }
            finally
            {
                s_reentrant = false;
            }
        }

        internal static bool IsReentrant => s_reentrant;
    }

    [HarmonyPatch(typeof(ZSyncTransform), nameof(ZSyncTransform.CustomFixedUpdate))]
    internal static class FixedTickSkipPatch
    {
        /// <summary>Skip the physics-tick pass for objects we drive from LateUpdate instead.</summary>
        [HarmonyPrefix]
        private static bool Prefix(ZSyncTransform __instance)
        {
            if (RenderRateSync.IsReentrant) return true;
            return !RenderRateSync.Handles(__instance);
        }
    }

    [HarmonyPatch(typeof(ZSyncTransform), nameof(ZSyncTransform.CustomLateUpdate))]
    internal static class RenderTickSyncPatch
    {
        /// <summary>
        /// Runs after OwnerSync, which returns immediately for anything we do not own, so the two
        /// never overlap on the same object.
        /// </summary>
        [HarmonyPostfix]
        private static void Postfix(ZSyncTransform __instance)
        {
            if (!RenderRateSync.Handles(__instance)) return;
            RenderRateSync.Run(__instance);
        }
    }
}
