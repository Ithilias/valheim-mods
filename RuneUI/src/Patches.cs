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

        // The hotbar goes first: the quick bar and bars are stacked on where it is this frame.
        private static readonly Feature[] Features =
        {
            new Feature { Name = "hotbar", Update = Hotbar.Update, Remove = Hotbar.Remove, ResetState = Hotbar.ResetState },
            new Feature { Name = "quick bar", Update = QuickBar2.Update, Remove = QuickBar2.Remove, ResetState = QuickBar2.ResetState },
            new Feature { Name = "bars", Update = HudBars.Update, Remove = HudBars.Remove, ResetState = HudBars.ResetState },
            new Feature { Name = "buffs", Update = BuffBar.Update, Remove = BuffBar.Remove, ResetState = BuffBar.ResetState },
            new Feature { Name = "party list", Update = PartyList.Update, Remove = PartyList.Remove, ResetState = PartyList.ResetState },
            new Feature { Name = "skill toasts", Update = SkillToasts.Update, Remove = SkillToasts.Remove, ResetState = SkillToasts.ResetState },
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
            if (!Plugin.ModEnabled.Value) return;
            if (!Failed.Contains("bars")) Guard("bars", () => HudBars.HideVanilla(__instance), HudBars.Remove);
            if (!Failed.Contains("buffs")) Guard("buffs", () => BuffBar.HideVanilla(__instance), BuffBar.Remove);
        }

        /// <summary>Runs a hook that is not per-frame hot; a failure switches that part off.</summary>
        private static void Guard(string name, Action action, Action undo = null)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Failed.Add(name);
                Plugin.Log.LogError($"The {name} part of Rune UI failed and is off until restart: {e}");
                try { undo?.Invoke(); } catch { /* already broken, nothing more to undo */ }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "Update")]
        private static void PlayerUpdatePostfix(Player __instance)
        {
            if (!Plugin.ModEnabled.Value || !ReferenceEquals(__instance, Player.m_localPlayer)) return;
            if (!Failed.Contains("quick bar")) Guard("quick bar", () => QuickBar2.HandleInput(__instance));
            if (!Failed.Contains("food slots") && QuickBar2.CanTakeInput(__instance))
                Guard("food slots", () => FoodPouch.HandleInput(__instance));
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
    
        // Food slots: the inventory screen. A failure here hides the slots but never touches the food.

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoryGui), "UpdateInventory")]
        private static void InventoryGuiUpdateInventoryPostfix(InventoryGui __instance, Player player)
        {
            if (!Failed.Contains("food slots"))
                Guard("food slots", () => FoodPouchGui.Update(__instance, player), FoodPouchGui.Remove);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
        private static bool InventoryGuiOnSelectedItemPrefix(InventoryGui __instance, InventoryGrid grid,
            ItemDrop.ItemData item, InventoryGrid.Modifier mod)
        {
            // Not gated on any setting: food already in the slots must stay consistent.
            try
            {
                return FoodPouchGui.AllowSelect(__instance, grid, item, mod);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Food slot check failed, blocking the move to be safe: {e}");
                return false;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
        private static void InventoryGridUpdateGuiPostfix(InventoryGrid __instance)
        {
            if (!Failed.Contains("quality rings")) Guard("quality rings", () => QualityRing.UpdateGrid(__instance));
        }

        // Food slots: saving, weight and death. These always run, even with the mod switched off,
        // so food in the slots is never lost.

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.Load))]
        private static void PlayerLoadPostfix(Player __instance)
        {
            try { FoodPouch.OnLoad(__instance); }
            catch (Exception e) { Plugin.Log.LogError($"Loading the food slots failed: {e}"); }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.Save))]
        private static void PlayerSavePrefix(Player __instance)
        {
            try { FoodPouch.OnSave(__instance); }
            catch (Exception e) { Plugin.Log.LogError($"Saving the food slots failed: {e}"); }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetTotalWeight))]
        private static void InventoryGetTotalWeightPostfix(Inventory __instance, ref float __result)
        {
            __result += FoodPouch.ExtraWeight(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.CreateTombStone))]
        private static void PlayerCreateTombStonePrefix(Player __instance)
        {
            try { FoodPouch.OnDeath(__instance); }
            catch (Exception e) { Plugin.Log.LogError($"Moving the food slots to the tombstone failed: {e}"); }
        }
    
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill))]
        private static void RaiseSkillPrefix(Skills __instance, Skills.SkillType skillType, out float __state)
        {
            __state = -1f;
            if (Failed.Contains("skill toasts")) return;
            try { __state = SkillToasts.LevelBefore(__instance, skillType); }
            catch (Exception e) { Failed.Add("skill toasts"); Plugin.Log.LogError($"Skill toasts failed and are off until restart: {e}"); }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill))]
        private static void RaiseSkillPostfix(Skills __instance, Skills.SkillType skillType, float __state)
        {
            if (Failed.Contains("skill toasts")) return;
            try { SkillToasts.OnRaised(__instance, skillType, __state); }
            catch (Exception e) { Failed.Add("skill toasts"); Plugin.Log.LogError($"Skill toasts failed and are off until restart: {e}"); }
        }
    
        [HarmonyPostfix]
        [HarmonyPatch(typeof(KeyHints), "Update")]
        private static void KeyHintsUpdatePostfix(KeyHints __instance)
        {
            if (!Failed.Contains("key hints")) Guard("key hints", () => KeyHintsStack.Update(__instance), KeyHintsStack.Restore);
        }
    }
}
