using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RuneUI
{
    /// <summary>
    /// Six slots for worn armour: helmet, chest, legs, cape, utility and trinket. They live in their own
    /// inventory (see <see cref="SavedInventory"/>), and what is in them is worn. Vanilla only equips,
    /// repairs and upgrades items in the player inventory, so those checks are widened to these slots.
    /// </summary>
    internal static class GearSlots
    {
        public const int Size = 6;

        private static readonly SavedInventory Store =
            new SavedInventory("Gear", "gear slots", "ithilias.runeui.gearslots", Size);

        // Items whose icons stand for each slot while it is empty; any item of the slot's type if none exist.
        private static readonly string[] PlaceholderItems =
            { "HelmetLeather", "ArmorLeatherChest", "ArmorLeatherLegs", "CapeDeerHide", "BeltStrength", "TrinketBronzeHealth" };
        private static readonly Sprite[] Placeholders = new Sprite[Size];
        private static ObjectDB _placeholderDb;

        // While above zero, equipping and unequipping do not move items in or out of the slots: vanilla
        // or this mod is moving them itself.
        private static int _busy;

        static GearSlots()
        {
            Store.Loaded = OnLoaded;
        }

        public static Inventory Get() => Store.Get();

        public static Inventory Current => Store.Current;

        public static bool Owns(Inventory inventory) => Store.Owns(inventory);

        /// <summary>The faint icon an empty slot shows, taken from a vanilla item worn there.</summary>
        public static Sprite Placeholder(int slot)
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null) return null;
            if (!ReferenceEquals(db, _placeholderDb))
            {
                _placeholderDb = db;
                for (int i = 0; i < Size; i++) Placeholders[i] = FindIcon(db, i);
            }
            return Placeholders[slot];
        }

        private static Sprite FindIcon(ObjectDB db, int slot)
        {
            ItemDrop.ItemData item = db.GetItemPrefab(PlaceholderItems[slot])?.GetComponent<ItemDrop>()?.m_itemData;
            if (item == null || SlotFor(item) != slot)
            {
                item = null;
                foreach (GameObject prefab in db.m_items)
                {
                    ItemDrop.ItemData candidate = prefab != null ? prefab.GetComponent<ItemDrop>()?.m_itemData : null;
                    if (candidate == null || SlotFor(candidate) != slot || candidate.m_shared.m_icons.Length == 0) continue;
                    item = candidate;
                    break;
                }
            }
            return item != null && item.m_shared.m_icons.Length > 0 ? item.m_shared.m_icons[0] : null;
        }

        /// <summary>The slot an item is worn in, or -1 for anything that is not armour.</summary>
        public static int SlotFor(ItemDrop.ItemData item)
        {
            if (item == null) return -1;
            switch (item.m_shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.Helmet: return 0;
                case ItemDrop.ItemData.ItemType.Chest: return 1;
                case ItemDrop.ItemData.ItemType.Legs: return 2;
                case ItemDrop.ItemData.ItemType.Shoulder: return 3;
                case ItemDrop.ItemData.ItemType.Utility: return 4;
                case ItemDrop.ItemData.ItemType.Trinket: return 5;
                default: return -1;
            }
        }

        public static void Suspend() => _busy++;

        public static void Resume() => _busy = Mathf.Max(0, _busy - 1);

        private static bool Active(Humanoid who) =>
            _busy == 0 && Plugin.GearSlotsEnabled.Value && who is Player player &&
            ReferenceEquals(player, Player.m_localPlayer) && !player.IsDead();

        public static void OnLoad(Player player) => Store.OnLoad(player);

        public static void OnSave(Player player) => Store.OnSave(player);

        public static float ExtraWeight(Inventory inventory) => Store.ExtraWeight(inventory);

        /// <summary>
        /// Wears what was saved in the slots, as vanilla does for its inventory on load, and moves armour
        /// worn from the inventory, for example from before these slots existed, into its slot.
        /// </summary>
        private static void OnLoaded(Player player, Inventory gear)
        {
            Suspend();
            try
            {
                foreach (var item in new List<ItemDrop.ItemData>(gear.GetAllItems()))
                {
                    if (!item.m_equipped) continue;
                    item.m_equipped = false;
                    player.EquipItem(item, false);
                }
            }
            finally
            {
                Resume();
            }
            if (!Plugin.GearSlotsEnabled.Value) return;
            foreach (var item in new List<ItemDrop.ItemData>(player.GetInventory().GetAllItems()))
                if (item.m_equipped) MoveIn(player, item);
        }

        /// <summary>Humanoid.EquipItem postfix: armour put on from the inventory moves into its slot.</summary>
        public static void AfterEquip(Humanoid who, ItemDrop.ItemData item, bool equipped)
        {
            if (equipped && Active(who)) MoveIn((Player)who, item);
        }

        /// <summary>Humanoid.UnequipItem postfix: armour taken off moves out of its slot, if there is room.</summary>
        public static void AfterUnequip(Humanoid who, ItemDrop.ItemData item)
        {
            if (item == null || !Active(who)) return;
            Inventory gear = Store.Current;
            if (gear == null || !gear.ContainsItem(item)) return;
            Inventory main = who.GetInventory();
            if (SavedInventory.FindEmpty(main, out Vector2i pos)) SavedInventory.Move(gear, main, item, pos);
        }

        /// <summary>Moves worn armour from the inventory or a quick slot into its gear slot.</summary>
        public static void MoveIn(Player player, ItemDrop.ItemData item)
        {
            int slot = SlotFor(item);
            Inventory gear = ReferenceEquals(player, Store.Owner) ? Store.Current : Store.Get();
            Inventory main = player.GetInventory();
            if (slot < 0 || gear == null) return;
            // The swap below puts what was in the slot where the item came from.
            if (!main.ContainsItem(item))
            {
                main = SavedInventory.Holding(player.GetInventory(), item);
                if (main == null || main == gear) return;
            }

            Suspend();
            try
            {
                var target = new Vector2i(slot, 0);
                ItemDrop.ItemData old = gear.GetItemAt(slot, 0);
                if (old == null)
                {
                    SavedInventory.Move(main, gear, item, target);
                    return;
                }
                // The slot still holds what was worn before (no room to move it out): swap the two.
                if (old.m_equipped) return;
                Vector2i from = item.m_gridPos;
                main.RemoveItem(item);
                gear.RemoveItem(old);
                main.AddItem(old, from);
                gear.AddItem(item, target);
            }
            finally
            {
                Resume();
            }
        }

        /// <summary>
        /// InventoryGui.OnSelectedItem postfix, after a drop. Vanilla re-equips moved items only in the
        /// player inventory: wear what was dropped into a slot, take off what was dragged out of one.
        /// </summary>
        public static void AfterDrop(InventoryGrid grid, Vector2i pos, Inventory dragFrom, Vector2i dragPos)
        {
            Player player = Player.m_localPlayer;
            if (player == null || dragFrom == null) return;
            Inventory target = grid.GetInventory();
            ItemDrop.ItemData landed = target.GetItemAt(pos.x, pos.y);
            if (Owns(target))
            {
                Wear(player, landed);
                return;
            }
            if (!Owns(dragFrom)) return;
            if (landed != null && landed.m_equipped && SlotFor(landed) >= 0) player.UnequipItem(landed);
            // Dropped onto armour of the same kind, which swapped into the slot.
            Wear(player, dragFrom.GetItemAt(dragPos.x, dragPos.y));
        }

        private static void Wear(Player player, ItemDrop.ItemData item)
        {
            if (item != null && !item.m_equipped) player.EquipItem(item);
        }

        public static void ResetState()
        {
            Store.ResetState();
            _busy = 0;
        }
    }
}
