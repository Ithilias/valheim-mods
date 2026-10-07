using System.Collections.Generic;
using UnityEngine;

namespace RuneUI
{
    /// <summary>
    /// On death, puts the food and gear slots into the tombstone with the rest, and marks what was in
    /// the food slots or worn. When the player picks those items up again, food goes back into its
    /// slot and gear is worn again, as long as nothing has taken that place in the meantime.
    /// </summary>
    internal static class DeathKeeper
    {
        // Item custom data: "food:<slot>:<player id>" or "equip::<player id>". Saved with the item, so
        // it survives the tombstone being unloaded.
        private const string Key = "ithilias.runeui.restore";

        private static bool _dirty;
        private static Inventory _watched;
        private static int _grownFrom = -1;

        /// <summary>Player.CreateTombStone prefix.</summary>
        public static void BeforeTombstone(Player player)
        {
            _grownFrom = -1;
            if (!ReferenceEquals(player, Player.m_localPlayer)) return;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.DeathKeepInventory)) return;
            bool keepEquipped = ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.DeathKeepEquip);

            Inventory main = player.GetInventory();
            string id = player.GetPlayerID().ToString();
            if (!keepEquipped)
                foreach (var item in main.GetAllItems())
                    if (item.m_equipped) item.m_customData[Key] = "equip::" + id;

            var moving = new List<KeyValuePair<Inventory, ItemDrop.ItemData>>();
            Inventory pouch = FoodPouch.Current;
            if (pouch != null)
                foreach (var item in pouch.GetAllItems())
                {
                    item.m_customData[Key] = "food:" + item.m_gridPos.x + ":" + id;
                    moving.Add(new KeyValuePair<Inventory, ItemDrop.ItemData>(pouch, item));
                }
            Inventory gear = GearSlots.Current;
            if (gear != null && !keepEquipped)
                foreach (var item in gear.GetAllItems())
                {
                    if (item.m_equipped) item.m_customData[Key] = "equip::" + id;
                    moving.Add(new KeyValuePair<Inventory, ItemDrop.ItemData>(gear, item));
                }
            if (moving.Count == 0) return;

            // Vanilla gives the tombstone the inventory's size and keeps every item where it was. With a
            // full inventory, grow it for the moment so the slots fit too; the tombstone keeps the rows.
            GearSlots.Suspend();
            try
            {
                foreach (var pair in moving)
                {
                    if (!SavedInventory.FindEmpty(main, out Vector2i pos))
                    {
                        if (_grownFrom < 0) _grownFrom = main.GetHeight();
                        main.SetHeight(main.GetHeight() + 1);
                        pos = new Vector2i(0, main.GetHeight() - 1);
                    }
                    SavedInventory.Move(pair.Key, main, pair.Value, pos);
                }
            }
            finally
            {
                GearSlots.Resume();
            }
        }

        /// <summary>Player.CreateTombStone postfix: undo the extra rows and forget marks on what stayed.</summary>
        public static void AfterTombstone(Player player)
        {
            if (!ReferenceEquals(player, Player.m_localPlayer)) return;
            Inventory main = player.GetInventory();
            foreach (var item in main.GetAllItems()) item.m_customData.Remove(Key);
            if (_grownFrom >= 0 && !ShrinkTo(main, _grownFrom))
                Plugin.Log.LogWarning("Items kept on death did not fit; your inventory keeps an extra row until they are moved.");
            _grownFrom = -1;
        }

        /// <summary>
        /// Moves items below row <paramref name="height"/> into free cells above it, then shrinks the
        /// inventory to that height. Returns false, and keeps the rows, if they do not all fit.
        /// </summary>
        public static bool ShrinkTo(Inventory inventory, int height)
        {
            foreach (var item in new List<ItemDrop.ItemData>(inventory.GetAllItems()))
            {
                if (item.m_gridPos.y < height) continue;
                Vector2i pos = new Vector2i(-1, -1);
                for (int y = 0; y < height && pos.x < 0; y++)
                for (int x = 0; x < inventory.GetWidth() && pos.x < 0; x++)
                    if (inventory.GetItemAt(x, y) == null) pos = new Vector2i(x, y);
                if (pos.x < 0) return false;
                inventory.RemoveItem(item);
                inventory.AddItem(item, pos);
            }
            inventory.SetHeight(height);
            return true;
        }

        /// <summary>Called after Player.Update for the local player.</summary>
        public static void Update(Player player)
        {
            Inventory main = player.GetInventory();
            if (!ReferenceEquals(main, _watched))
            {
                _watched = main;
                main.m_onChanged += () => _dirty = true;
                _dirty = true;
            }
            if (!_dirty || player.IsDead()) return;
            _dirty = false;
            Restore(player, main);
        }

        private static void Restore(Player player, Inventory main)
        {
            string id = player.GetPlayerID().ToString();
            foreach (var item in new List<ItemDrop.ItemData>(main.GetAllItems()))
            {
                if (!item.m_customData.TryGetValue(Key, out string mark)) continue;
                item.m_customData.Remove(Key);
                string[] parts = mark.Split(':');
                if (parts.Length != 3 || parts[2] != id) continue;

                if (parts[0] == "food" && int.TryParse(parts[1], out int slot))
                {
                    Inventory pouch = Plugin.FoodSlotsEnabled.Value ? FoodPouch.Get() : null;
                    if (pouch != null && slot >= 0 && slot < FoodPouch.Size && pouch.GetItemAt(slot, 0) == null)
                        SavedInventory.Move(main, pouch, item, new Vector2i(slot, 0));
                }
                else if (parts[0] == "equip" && !item.m_equipped && PlaceFree(player, item))
                {
                    // Armour then moves into its gear slot by itself.
                    player.EquipItem(item);
                }
            }
        }

        /// <summary>Whether wearing <paramref name="item"/> would take nothing else off.</summary>
        private static bool PlaceFree(Player player, ItemDrop.ItemData item)
        {
            int gearSlot = GearSlots.SlotFor(item);
            if (gearSlot >= 0)
            {
                Inventory gear = GearSlots.Current;
                if (gear != null && gear.GetItemAt(gearSlot, 0) != null) return false;
                foreach (var other in player.GetInventory().GetAllItems())
                    if (other.m_equipped && GearSlots.SlotFor(other) == gearSlot) return false;
                return true;
            }
            ItemDrop.ItemData right = player.RightItem, left = player.LeftItem;
            switch (item.m_shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.Ammo:
                case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
                    return player.GetAmmoItem() == null;
                case ItemDrop.ItemData.ItemType.Shield:
                    return left == null;
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.Tool:
                case ItemDrop.ItemData.ItemType.Torch:
                    return right == null;
                default:
                    return right == null && left == null;
            }
        }

        public static void ResetState()
        {
            _dirty = false;
            _watched = null;
            _grownFrom = -1;
        }
    }
}
