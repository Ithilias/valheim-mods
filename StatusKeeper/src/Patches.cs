using HarmonyLib;

namespace StatusKeeper
{
    [HarmonyPatch]
    internal static class Patches
    {
        private static bool _restorePending;

        /// <summary>
        /// Runs before the game serialises the player, which writes m_customData as part of the
        /// same package, so anything put there here rides along into the character file.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.Save), typeof(ZPackage))]
        private static void PlayerSavePrefix(Player __instance)
        {
            if (!Plugin.ModEnabled.Value) return;
            if (__instance != Player.m_localPlayer) return;
            try
            {
                StatusPersistence.Capture(__instance);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Failed to save status effects: {e}");
            }
        }

        /// <summary>Load fills m_customData; the effects go back on once the player has spawned.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.Load), typeof(ZPackage))]
        private static void PlayerLoadPostfix()
        {
            _restorePending = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned), typeof(bool))]
        private static void PlayerOnSpawnedPostfix(Player __instance)
        {
            if (!_restorePending) return;
            _restorePending = false;
            if (!Plugin.ModEnabled.Value) return;
            if (__instance != Player.m_localPlayer) return;
            try
            {
                StatusPersistence.Restore(__instance);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Failed to restore status effects: {e}");
            }
        }
    }
}
