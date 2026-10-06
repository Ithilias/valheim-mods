using System;
using System.Collections.Generic;
using HarmonyLib;

namespace RuneUI
{
    [HarmonyPatch]
    internal static class Patches
    {
        private sealed class Feature
        {
            public string Name;
            public Action<Hud, Player> Update;
            public Action Remove;
            public Action ResetState;
        }

        private static readonly Feature[] Features =
        {
            new Feature { Name = "bars", Update = HudBars.Update, Remove = HudBars.Remove, ResetState = HudBars.ResetState },
            new Feature { Name = "food", Update = FoodRow.Update, Remove = FoodRow.Remove, ResetState = FoodRow.ResetState },
            new Feature { Name = "hotbar", Update = Hotbar.Update, Remove = Hotbar.Remove, ResetState = Hotbar.ResetState },
            new Feature { Name = "quick bar", Update = QuickBar2.Update, Remove = QuickBar2.Remove, ResetState = QuickBar2.ResetState },
            new Feature { Name = "party list", Update = PartyList.Update, Remove = PartyList.Remove, ResetState = PartyList.ResetState },
        };

        // A feature that throws is switched off for the session instead of spamming the log every frame.
        private static readonly HashSet<string> Failed = new HashSet<string>();

        private static Hud _tracked;
        private static bool _applied;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Hud), "Update")]
        private static void HudUpdatePostfix(Hud __instance)
        {
            // Hud and everything this mod parented to it are rebuilt on every login.
            if (!ReferenceEquals(__instance, _tracked))
            {
                _tracked = __instance;
                foreach (var f in Features) f.ResetState();
                Theme.SetFallbackFont(__instance.m_healthText != null ? __instance.m_healthText.font : null);
            }

            if (!Plugin.ModEnabled.Value)
            {
                if (_applied)
                {
                    foreach (var f in Features) Run(f, null, null, true);
                    _applied = false;
                }
                return;
            }

            _applied = true;
            Player player = Player.m_localPlayer;
            foreach (var f in Features)
            {
                if (Failed.Contains(f.Name)) continue;
                Run(f, __instance, player, false);
            }
        }

        private static void Run(Feature f, Hud hud, Player player, bool remove)
        {
            try
            {
                if (remove) f.Remove();
                else f.Update(hud, player);
            }
            catch (Exception e)
            {
                Failed.Add(f.Name);
                Plugin.Log.LogError($"The {f.Name} part of Rune UI failed and is off until restart: {e}");
                try { f.Remove(); } catch { /* already broken, nothing more to undo */ }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Hud), "LateUpdate")]
        private static void HudLateUpdatePostfix(Hud __instance)
        {
            if (!Plugin.ModEnabled.Value || Failed.Contains("bars")) return;
            try
            {
                HudBars.HideVanilla(__instance);
            }
            catch (Exception e)
            {
                Failed.Add("bars");
                Plugin.Log.LogError($"The bars part of Rune UI failed and is off until restart: {e}");
                HudBars.Remove();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "Update")]
        private static void PlayerUpdatePostfix(Player __instance)
        {
            if (!ReferenceEquals(__instance, Player.m_localPlayer) || Failed.Contains("quick bar")) return;
            try
            {
                QuickBar2.HandleInput(__instance);
            }
            catch (Exception e)
            {
                Failed.Add("quick bar");
                Plugin.Log.LogError($"The quick bar part of Rune UI failed and is off until restart: {e}");
            }
        }

        /// <summary>While the quick bar modifier is held, number keys belong to the quick bar.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.UseHotbarItem))]
        private static bool UseHotbarItemPrefix() => Failed.Contains("quick bar") || !QuickBar2.ModifierHeld;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(EnemyHud), "LateUpdate")]
        private static void EnemyHudLateUpdatePostfix(EnemyHud __instance)
        {
            if (Failed.Contains("player bars")) return;
            try
            {
                PlayerBarHider.Update(__instance);
            }
            catch (Exception e)
            {
                Failed.Add("player bars");
                Plugin.Log.LogError($"Hiding the bars over players failed and is off until restart: {e}");
                PlayerBarHider.Restore();
            }
        }
    }
}
