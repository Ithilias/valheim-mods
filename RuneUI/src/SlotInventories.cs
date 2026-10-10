using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RuneUI
{
    /// <summary>
    /// Vanilla treats only the player inventory as the player's. These hooks widen its checks to the
    /// quick and gear slots, so items there can be worn, repaired, upgraded and seen by other mods.
    /// </summary>
    internal static class SlotInventories
    {
        private static readonly AccessTools.FieldRef<InventoryGui, ItemDrop.ItemData> CraftUpgradeItem =
            AccessTools.FieldRefAccess<InventoryGui, ItemDrop.ItemData>("m_craftUpgradeItem");

        /// <summary>Set while vanilla loads a character, which re-equips what GetEquippedItems lists.</summary>
        public static bool Loading;

        /// <summary>
        /// Humanoid.EquipItem transpiler target, replacing its "is the item in my inventory" check so
        /// items in the slots can be worn.
        /// </summary>
        public static bool HoldsForEquip(Inventory inventory, ItemDrop.ItemData item) =>
            inventory.ContainsItem(item) || SavedInventory.Holding(inventory, item) != null;

        /// <summary>
        /// Inventory.GetEquippedItems postfix: other mods, such as Epic Loot, find worn gear through it,
        /// so items worn from the slots are listed as part of the player's inventory.
        /// </summary>
        public static void AddEquipped(Inventory inventory, List<ItemDrop.ItemData> items)
        {
            if (Loading) return;
            foreach (Inventory slots in SavedInventory.OwnedBy(inventory))
            foreach (var item in slots.GetAllItems())
                if (item.m_equipped && !items.Contains(item)) items.Add(item);
        }

        /// <summary>Inventory.GetWornItems postfix: workbenches repair the slots too.</summary>
        public static void AddWorn(Inventory inventory, List<ItemDrop.ItemData> worn)
        {
            foreach (Inventory slots in SavedInventory.OwnedBy(inventory)) slots.GetWornItems(worn);
        }

        /// <summary>Inventory.GetAllItems(name) postfix: the upgrade list at workbenches shows the slots too.</summary>
        public static void AddNamed(Inventory inventory, string name, List<ItemDrop.ItemData> items)
        {
            foreach (Inventory slots in SavedInventory.OwnedBy(inventory)) slots.GetAllItems(name, items);
        }

        /// <summary>What <see cref="BeforeCraft"/> moved, for <see cref="AfterCraft"/> to put back.</summary>
        public struct CraftState
        {
            public Inventory From;
            public Vector2i Slot;
            public Vector2i Pos;
            public int Height;
            public bool WasEquipped;
        }

        /// <summary>
        /// InventoryGui.DoCrafting prefix. Vanilla upgrades an item only in the player inventory, so
        /// move one being upgraded there for the craft, into an extra row if the inventory is full.
        /// </summary>
        public static CraftState BeforeCraft(InventoryGui gui, Player player)
        {
            var state = new CraftState();
            ItemDrop.ItemData item = CraftUpgradeItem(gui);
            Inventory main = player.GetInventory();
            Inventory from = item != null ? SavedInventory.Holding(main, item) : null;
            if (from == null) return state;

            state.Height = main.GetHeight();
            if (!SavedInventory.FindEmpty(main, out Vector2i pos))
            {
                main.SetHeight(state.Height + 1);
                pos = new Vector2i(0, state.Height);
            }
            state.Slot = item.m_gridPos;
            state.WasEquipped = item.m_equipped;
            GearSlots.Suspend();
            try
            {
                if (!SavedInventory.Move(from, main, item, pos))
                {
                    if (main.GetHeight() != state.Height) main.SetHeight(state.Height);
                    return state;
                }
            }
            finally
            {
                GearSlots.Resume();
            }
            state.From = from;
            state.Pos = pos;
            return state;
        }

        /// <summary>
        /// InventoryGui.DoCrafting finalizer: put the upgraded item, or the one that was not upgraded,
        /// back into its slot. Gear goes back on, since what is in a gear slot is worn.
        /// </summary>
        public static void AfterCraft(Player player, CraftState state)
        {
            if (state.From == null) return;
            Inventory main = player.GetInventory();
            ItemDrop.ItemData result = main.GetItemAt(state.Pos.x, state.Pos.y);
            if (result != null && state.From.GetItemAt(state.Slot.x, state.Slot.y) == null)
            {
                GearSlots.Suspend();
                try
                {
                    SavedInventory.Move(main, state.From, result, state.Slot);
                }
                finally
                {
                    GearSlots.Resume();
                }
                bool wear = GearSlots.Owns(state.From) || state.WasEquipped;
                if (wear && state.From.ContainsItem(result) && !result.m_equipped) player.EquipItem(result, false);
            }
            if (main.GetHeight() > state.Height && !DeathKeeper.ShrinkTo(main, state.Height))
                Plugin.Log.LogWarning("An upgraded item did not fit back; your inventory keeps an extra row until it is moved.");
        }
    }
}
