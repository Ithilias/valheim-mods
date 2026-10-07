using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

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
            if (!Failed.Contains("slots after death")) Guard("slots after death", () => DeathKeeper.Update(__instance));
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
    
        // Food and gear slots: the inventory screen. A failure here hides the slots but never touches
        // the items in them.

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoryGui), "UpdateInventory")]
        private static void InventoryGuiUpdateInventoryPostfix(InventoryGui __instance, Player player)
        {
            if (!Failed.Contains("inventory slots"))
                Guard("inventory slots", () => InventoryPanels.Update(__instance, player), InventoryPanels.Remove);
        }

        private struct SelectState
        {
            public bool Allowed;
            public Inventory DragFrom;
            public ItemDrop.ItemData Dragged;
            public Vector2i DragPos;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
        private static bool InventoryGuiOnSelectedItemPrefix(InventoryGui __instance, InventoryGrid grid,
            ItemDrop.ItemData item, Vector2i pos, InventoryGrid.Modifier mod, out SelectState __state)
        {
            // Vanilla unequips and re-equips around every move; the gear slots handle that after it.
            GearSlots.Suspend();
            __state = default;
            // Not gated on any setting: items already in the slots must stay consistent.
            try
            {
                __state.Allowed = InventoryPanels.AllowSelect(__instance, grid, item, pos, mod,
                    out __state.DragFrom, out __state.Dragged);
                if (__state.Dragged != null) __state.DragPos = __state.Dragged.m_gridPos;
                return __state.Allowed;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Inventory slot check failed, blocking the move to be safe: {e}");
                return false;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
        private static void InventoryGuiOnSelectedItemPostfix(InventoryGrid grid, Vector2i pos, SelectState __state)
        {
            if (!__state.Allowed || __state.Dragged == null) return;
            try { GearSlots.AfterDrop(grid, pos, __state.DragFrom, __state.DragPos); }
            catch (Exception e) { Plugin.Log.LogError($"Wearing gear dropped into the slots failed: {e}"); }
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
        private static void InventoryGuiOnSelectedItemFinalizer() => GearSlots.Resume();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryGui), "UpdateContainer")]
        private static void InventoryGuiUpdateContainerPrefix(InventoryGui __instance, out Inventory __state)
        {
            __state = null;
            try { __state = InventoryPanels.HideDragFromSlots(__instance); }
            catch (Exception e) { Plugin.Log.LogError($"Keeping an item picked up from the slots failed: {e}"); }
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(InventoryGui), "UpdateContainer")]
        private static void InventoryGuiUpdateContainerFinalizer(InventoryGui __instance, Inventory __state)
        {
            try { InventoryPanels.RestoreDragFromSlots(__instance, __state); }
            catch (Exception e) { Plugin.Log.LogError($"Restoring an item picked up from the slots failed: {e}"); }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
        private static void InventoryGridUpdateGuiPostfix(InventoryGrid __instance)
        {
            if (!Failed.Contains("quality rings")) Guard("quality rings", () => QualityRing.UpdateGrid(__instance));
        }

        // Food and gear slots: saving, weight, wearing and death. These always run, even with the mod
        // switched off, so items in the slots are never lost.

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.Load))]
        private static void PlayerLoadPrefix() => GearSlots.Loading = true;

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Player), nameof(Player.Load))]
        private static void PlayerLoadFinalizer() => GearSlots.Loading = false;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.Load))]
        private static void PlayerLoadPostfix(Player __instance)
        {
            try { FoodPouch.OnLoad(__instance); }
            catch (Exception e) { Plugin.Log.LogError($"Loading the food slots failed: {e}"); }
            try { GearSlots.OnLoad(__instance); }
            catch (Exception e) { Plugin.Log.LogError($"Loading the gear slots failed: {e}"); }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.Save))]
        private static void PlayerSavePrefix(Player __instance)
        {
            try { FoodPouch.OnSave(__instance); }
            catch (Exception e) { Plugin.Log.LogError($"Saving the food slots failed: {e}"); }
            try { GearSlots.OnSave(__instance); }
            catch (Exception e) { Plugin.Log.LogError($"Saving the gear slots failed: {e}"); }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetTotalWeight))]
        private static void InventoryGetTotalWeightPostfix(Inventory __instance, ref float __result)
        {
            __result += FoodPouch.ExtraWeight(__instance) + GearSlots.ExtraWeight(__instance);
        }

        /// <summary>Vanilla only equips items in the wearer's inventory; let the gear slots count as theirs.</summary>
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
        private static IEnumerable<CodeInstruction> EquipItemTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var contains = AccessTools.Method(typeof(Inventory), nameof(Inventory.ContainsItem));
            var holds = AccessTools.Method(typeof(GearSlots), nameof(GearSlots.HoldsForEquip));
            bool replaced = false;
            foreach (var instruction in instructions)
            {
                if (!replaced && instruction.Calls(contains))
                {
                    instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                    instruction.operand = holds;
                    replaced = true;
                }
                yield return instruction;
            }
            if (!replaced) Plugin.Log.LogError("Could not find the inventory check in EquipItem; gear in the gear slots cannot be worn.");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
        private static void EquipItemPostfix(Humanoid __instance, ItemDrop.ItemData item, bool __result)
        {
            try { GearSlots.AfterEquip(__instance, item, __result); }
            catch (Exception e) { Plugin.Log.LogError($"Moving worn gear into its slot failed: {e}"); }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UnequipItem))]
        private static void UnequipItemPostfix(Humanoid __instance, ItemDrop.ItemData item)
        {
            try { GearSlots.AfterUnequip(__instance, item); }
            catch (Exception e) { Plugin.Log.LogError($"Moving gear out of its slot failed: {e}"); }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetEquippedItems))]
        private static void InventoryGetEquippedItemsPostfix(Inventory __instance, List<ItemDrop.ItemData> __result)
        {
            try { GearSlots.AddEquipped(__instance, __result); }
            catch (Exception e) { Plugin.Log.LogError($"Listing worn gear failed: {e}"); }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetWornItems))]
        private static void InventoryGetWornItemsPostfix(Inventory __instance, List<ItemDrop.ItemData> worn)
        {
            try { GearSlots.AddWorn(__instance, worn); }
            catch (Exception e) { Plugin.Log.LogError($"Listing gear for repair failed: {e}"); }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetAllItems), typeof(string), typeof(List<ItemDrop.ItemData>))]
        private static void InventoryGetAllItemsByNamePostfix(Inventory __instance, string name, List<ItemDrop.ItemData> items)
        {
            try { GearSlots.AddNamed(__instance, name, items); }
            catch (Exception e) { Plugin.Log.LogError($"Listing gear for upgrades failed: {e}"); }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
        private static void DoCraftingPrefix(InventoryGui __instance, Player player, out GearSlots.CraftState __state)
        {
            __state = new GearSlots.CraftState { Slot = -1 };
            try { __state = GearSlots.BeforeCraft(__instance, player); }
            catch (Exception e) { Plugin.Log.LogError($"Preparing gear for an upgrade failed: {e}"); }
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
        private static void DoCraftingFinalizer(Player player, GearSlots.CraftState __state)
        {
            try { GearSlots.AfterCraft(player, __state); }
            catch (Exception e) { Plugin.Log.LogError($"Putting upgraded gear back failed: {e}"); }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.CreateTombStone))]
        private static void PlayerCreateTombStonePrefix(Player __instance)
        {
            try { DeathKeeper.BeforeTombstone(__instance); }
            catch (Exception e) { Plugin.Log.LogError($"Moving the food and gear slots to the tombstone failed: {e}"); }
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Player), nameof(Player.CreateTombStone))]
        private static void PlayerCreateTombStoneFinalizer(Player __instance)
        {
            try { DeathKeeper.AfterTombstone(__instance); }
            catch (Exception e) { Plugin.Log.LogError($"Tidying up after the tombstone failed: {e}"); }
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
