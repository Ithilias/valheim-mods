using HarmonyLib;
using UnityEngine;

namespace RoundMinimap
{
    [HarmonyPatch]
    internal static class Patches
    {
        private static readonly AccessTools.FieldRef<Minimap, bool> PinUpdateRequiredRef =
            AccessTools.FieldRefAccess<Minimap, bool>("m_pinUpdateRequired");

        private static string _appliedHiddenElements;
        private static Minimap _tracked;

        /// <summary>Asks vanilla to rebuild pin positions and visibility on its next pass.</summary>
        public static void RequestPinUpdate(Minimap map)
        {
            if (map != null) PinUpdateRequiredRef(map) = true;
        }

        /// <summary>
        /// Runs after Minimap.Update, which is where vanilla calls UpdateMap (markers) and
        /// UpdatePins. Overriding here means our values are the ones that survive the frame.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Minimap), "Update")]
        private static void MinimapUpdatePostfix(Minimap __instance)
        {
            try
            {
                // Leaving to the menu destroys the minimap HUD, so a rejoin hands us a brand new
                // instance while our caches still describe the old, destroyed one.
                if (!ReferenceEquals(__instance, _tracked))
                {
                    _tracked = __instance;
                    MinimapShape.ResetState();
                    MinimapRotation.ResetState();
                    MinimapCompass.ResetState();
                    MinimapZoom.ResetState();
                    BiomeLabelStyle.ResetState();
                    _appliedHiddenElements = null;
                }

                if (Plugin.DumpHierarchyKey.Value.IsDown() && __instance.m_smallRoot != null)
                    Plugin.Log.LogInfo(UiTools.DumpHierarchy(__instance.m_smallRoot.transform));

                if (!Plugin.ModEnabled.Value)
                {
                    TearDown(__instance);
                    return;
                }

                SyncHiddenElements(__instance);

                if (__instance.m_mode == Minimap.MapMode.Small)
                    MinimapZoom.Update(__instance);

                // The square map is plain vanilla: rotating it would sample past the map image's
                // edges in the corners, so rotation belongs to the round map only.
                bool needsCircle = Plugin.RoundMinimap.Value;
                bool rotating = needsCircle && Plugin.RotateMinimap.Value;

                MinimapShape.ApplyPlacement(__instance, needsCircle);

                if (needsCircle)
                    MinimapShape.Apply(__instance);
                else if (MinimapShape.IsApplied)
                    MinimapShape.Remove(__instance);

                if (rotating && __instance.m_mode == Minimap.MapMode.Small)
                    MinimapRotation.Apply(__instance, Time.deltaTime);
                else
                    MinimapRotation.Restore(__instance);

                // The compass rides on the circle, so it needs the same geometry and the angle the
                // map is currently turned by (zero when rotation is off, which puts north at the top).
                if (MinimapShape.IsApplied)
                {
                    MinimapCompass.Apply(__instance, MinimapShape.Radius);
                    MinimapCompass.SetAngle(__instance, MinimapRotation.Angle);
                }
                else if (MinimapCompass.IsApplied)
                {
                    MinimapCompass.Remove();
                }

                // After the layout pass, which is what moves the biome label under the circle.
                BiomeLabelStyle.Apply(__instance);

                // Last, so it measures the map where this frame left it.
                RunIsolated(ref _statusLayoutFailed, "Status effect layout", StatusEffectClearance.Apply, __instance,
                    StatusEffectClearance.Restore);
                RunIsolated(ref _shipLayoutFailed, "Sailing display layout", ShipHudClearance.Apply, __instance,
                    ShipHudClearance.Restore);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Minimap update failed, disabling mod for this session: {e}");
                Plugin.ModEnabled.Value = false;
            }
        }

        /// <summary>
        /// The game rebuilds the status effect icons in here whenever the number of effects changes,
        /// laying them out afresh. Reapplying our layout straight after keeps them from showing in
        /// the vanilla spots for a frame.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Hud), "UpdateStatusEffects")]
        private static void HudUpdateStatusEffectsPostfix(Hud __instance)
        {
            if (!Plugin.ModEnabled.Value) return;
            RunIsolated(ref _statusLayoutFailed, "Status effect layout", StatusEffectClearance.OnIconsRebuilt, __instance,
                StatusEffectClearance.Restore);
        }

        private static bool _statusLayoutFailed;
        private static bool _shipLayoutFailed;

        /// <summary>
        /// The status effect and sailing display layouts reach into the game's HUD, which a game
        /// update can change. If one fails, only that part is switched off for the session, with one
        /// log entry, and the rest of the mod keeps working.
        /// </summary>
        private static void RunIsolated<T>(ref bool failed, string what, System.Action<T> action, T target,
            System.Action restore)
        {
            if (failed) return;
            try
            {
                action(target);
            }
            catch (System.Exception e)
            {
                failed = true;
                Plugin.Log.LogError($"{what} failed, leaving it to the game for this session: {e}");
                try { restore(); }
                catch (System.Exception) { /* already reported */ }
            }
        }

        private static void TearDown(Minimap map)
        {
            MinimapRotation.Restore(map);
            if (MinimapShape.IsApplied) MinimapShape.Remove(map);
            if (MinimapCompass.IsApplied) MinimapCompass.Remove();
            BiomeLabelStyle.Remove();
            MinimapShape.RestorePlacement(map);
            StatusEffectClearance.Restore();
            ShipHudClearance.Restore();
            if (!string.IsNullOrEmpty(_appliedHiddenElements))
            {
                MinimapShape.ApplyHiddenElements(map, Split(_appliedHiddenElements), hidden: false);
                _appliedHiddenElements = null;
            }
        }

        private static void SyncHiddenElements(Minimap map)
        {
            string wanted = Plugin.HiddenElements.Value ?? string.Empty;
            if (wanted == (_appliedHiddenElements ?? string.Empty)) return;

            if (!string.IsNullOrEmpty(_appliedHiddenElements))
                MinimapShape.ApplyHiddenElements(map, Split(_appliedHiddenElements), hidden: false);

            if (!string.IsNullOrEmpty(wanted))
                MinimapShape.ApplyHiddenElements(map, Split(wanted), hidden: true);

            _appliedHiddenElements = wanted;
        }

        private static string[] Split(string value) =>
            value.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
    }
}
