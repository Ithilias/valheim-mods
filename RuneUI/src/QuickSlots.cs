using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace RuneUI
{
    /// <summary>
    /// Up to six extra slots for any item, each used with its own key, saved with the character (see
    /// <see cref="SavedInventory"/>). They extend the quick bar on the HUD and sit beside the
    /// inventory. They go into the tombstone on death; see <see cref="DeathKeeper"/>.
    /// </summary>
    internal static class QuickSlots
    {
        public const int MaxSize = 6;

        // The save key is from when these were food slots; keeping it keeps the food players saved.
        private static readonly SavedInventory Store =
            new SavedInventory("Quick slots", "quick slots", "ithilias.runeui.foodslots", MaxSize);

        private static readonly AccessTools.FieldRef<Inventory, int> Width =
            AccessTools.FieldRefAccess<Inventory, int>("m_width");

        /// <summary>The local player's quick slots, or null before a player exists.</summary>
        public static Inventory Get()
        {
            Inventory inventory = Store.Get();
            if (inventory != null) Resize(inventory);
            return inventory;
        }

        /// <summary>The slots as they are, without attaching to a new player.</summary>
        public static Inventory Current => Store.Current;

        public static bool Owns(Inventory inventory) => Store.Owns(inventory);

        /// <summary>Slots shown, from the setting; 0 when the slots are off.</summary>
        public static int Count => Plugin.QuickSlotsEnabled.Value ? Mathf.Clamp(Plugin.QuickSlotCount.Value, 0, MaxSize) : 0;

        /// <summary>
        /// Anything but ammo: vanilla only takes arrows and bolts from the player inventory when it
        /// shoots, so ammo in a quick slot could be equipped but never fired.
        /// </summary>
        public static bool Fits(ItemDrop.ItemData item) =>
            item != null && item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo &&
            item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.AmmoNonEquipable;

        public static KeyboardShortcut KeyFor(int slot) => Plugin.QuickSlotKeys[slot].Value;

        /// <summary>The slot's label setting, or its key as named on the player's keyboard layout.</summary>
        public static string KeyLabel(int slot)
        {
            string label = Plugin.QuickSlotLabels[slot].Value.Trim();
            if (label.Length > 0) return label;
            KeyboardShortcut key = KeyFor(slot);
            if (key.MainKey == KeyCode.None) return "";
            string name = KeyName(key.MainKey);
            foreach (KeyCode modifier in key.Modifiers) name = ModifierName(modifier) + "+" + name;
            return name;
        }

        private static string KeyName(KeyCode key)
        {
            string name = null;
            try { name = ZInput.KeyCodeToDisplayName(key); }
            catch { /* not every key code maps to a control */ }
            return string.IsNullOrEmpty(name) || name.StartsWith("$") ? key.ToString() : name.ToUpperInvariant();
        }

        private static string ModifierName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                    return "Alt";
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    return "Ctrl";
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                    return "Shift";
                default:
                    return KeyName(key);
            }
        }

        /// <summary>
        /// Shows as many slots as the setting asks for. Items in slots the count no longer covers move
        /// to the inventory; if they do not fit, those slots stay until there is room. Turning the
        /// slots off only hides them.
        /// </summary>
        private static void Resize(Inventory inventory)
        {
            if (!Plugin.QuickSlotsEnabled.Value) return;
            int wanted = Count;
            if (inventory.GetWidth() == Mathf.Max(wanted, 1)) return;
            Player player = Store.Owner;
            int needed = Mathf.Max(wanted, 1);
            foreach (var item in new List<ItemDrop.ItemData>(inventory.GetAllItems()))
            {
                if (item.m_gridPos.x < wanted) continue;
                Inventory main = player != null ? player.GetInventory() : null;
                if (main != null && !item.m_equipped && SavedInventory.FindEmpty(main, out Vector2i pos)
                    && SavedInventory.Move(inventory, main, item, pos))
                    continue;
                needed = Mathf.Max(needed, item.m_gridPos.x + 1);
            }
            if (inventory.GetWidth() != needed) Width(inventory) = needed;
        }

        public static void OnLoad(Player player) => Store.OnLoad(player);

        public static void OnSave(Player player) => Store.OnSave(player);

        public static float ExtraWeight(Inventory inventory) => Store.ExtraWeight(inventory);

        /// <summary>Whether slot <paramref name="slot"/> has an item and its key is held.</summary>
        public static bool KeyHeldWithItem(int slot)
        {
            Inventory inventory = Store.Current;
            if (inventory == null || slot >= Count || inventory.GetItemAt(slot, 0) == null) return false;
            KeyboardShortcut key = KeyFor(slot);
            return QuickKeys.Check(key, true);
        }

        /// <summary>Called after Player.Update for the local player.</summary>
        public static void HandleInput(Player player)
        {
            Inventory inventory = Get();
            if (inventory == null) return;
            for (int i = 0; i < Count; i++)
            {
                KeyboardShortcut key = KeyFor(i);
                if (!QuickKeys.Check(key, false)) continue;
                ItemDrop.ItemData item = inventory.GetItemAt(i, 0);
                if (item != null) player.UseItem(inventory, item, false);
            }
        }

        public static void ResetState() => Store.ResetState();
    }
}
