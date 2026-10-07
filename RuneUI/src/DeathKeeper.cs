using System.Collections.Generic;
using UnityEngine;

namespace RuneUI
{
    /// <summary>
    /// On death, puts the quick and gear slots into the tombstone with the rest, and marks what was in
    /// a quick slot or worn. When the player picks those items up again, they go back into their quick
    /// slot and are worn again, as long as nothing has taken that place in the meantime.
    /// </summary>
    internal static class DeathKeeper
    {
        // Item custom data, saved with the item so it survives the tombstone being unloaded:
        // "quick:<slot>:<player id>" for the quick slot an item was in, the player id for worn items.
        private const string Key = "ithilias.runeui.restore";
        private const string EquipKey = "ithilias.runeui.equip";

        private static bool _dirty;
        private static Inventory _watched;
        private static int _grownFrom = -1;
        private static bool _suspended;
        // Gear kept on death, which vanilla's death still takes off.
        private static readonly List<ItemDrop.ItemData> KeptWorn = new List<ItemDrop.ItemData>();

        /// <summary>Player.CreateTombStone prefix.</summary>
        public static void BeforeTombstone(Player player)
        {
            _grownFrom = -1;
            KeptWorn.Clear();
            if (!ReferenceEquals(player, Player.m_localPlayer)) return;
            // Vanilla takes everything off on death; kept gear must not move out of its slot for it.
            GearSlots.Suspend();
            _suspended = true;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.DeathKeepInventory)) return;
            bool keepEquipped = ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.DeathKeepEquip);
            bool keepGear = keepEquipped || Plugin.KeepGearOnDeath.Value;
            bool keepQuick = Plugin.KeepQuickOnDeath.Value;

            Inventory main = player.GetInventory();
            string id = player.GetPlayerID().ToString();
            if (!keepEquipped)
                foreach (var item in main.GetAllItems())
                    if (item.m_equipped) item.m_customData[EquipKey] = id;

            var moving = new List<KeyValuePair<Inventory, ItemDrop.ItemData>>();
            Inventory quick = QuickSlots.Current;
            if (quick != null && !keepQuick)
                foreach (var item in quick.GetAllItems())
                {
                    // Worn items stay with the player like vanilla's, when the world keeps equipment.
                    if (keepEquipped && item.m_equipped) continue;
                    item.m_customData[Key] = "quick:" + item.m_gridPos.x + ":" + id;
                    if (item.m_equipped) item.m_customData[EquipKey] = id;
                    moving.Add(new KeyValuePair<Inventory, ItemDrop.ItemData>(quick, item));
                }
            Inventory gear = GearSlots.Current;
            if (gear != null && keepGear)
                foreach (var item in gear.GetAllItems())
                    if (item.m_equipped) KeptWorn.Add(item);
            if (gear != null && !keepGear)
                foreach (var item in gear.GetAllItems())
                {
                    if (item.m_equipped) item.m_customData[EquipKey] = id;
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
            if (_suspended)
            {
                _suspended = false;
                GearSlots.Resume();
            }
            // Saved as worn, so the respawned character wears it again on load.
            foreach (var item in KeptWorn) item.m_equipped = true;
            KeptWorn.Clear();
            if (!ReferenceEquals(player, Player.m_localPlayer)) return;
            Inventory main = player.GetInventory();
            foreach (var item in main.GetAllItems())
            {
                item.m_customData.Remove(Key);
                item.m_customData.Remove(EquipKey);
            }
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
                bool marked = item.m_customData.TryGetValue(Key, out string mark);
                bool worn = item.m_customData.TryGetValue(EquipKey, out string wornBy);
                if (!marked && !worn) continue;
                item.m_customData.Remove(Key);
                item.m_customData.Remove(EquipKey);

                Inventory from = main;
                string[] parts = marked ? mark.Split(':') : new string[0];
                // "food" is what the quick slots were called before.
                if (parts.Length == 3 && parts[2] == id && (parts[0] == "quick" || parts[0] == "food")
                    && int.TryParse(parts[1], out int slot))
                {
                    Inventory quick = QuickSlots.Get();
                    if (quick != null && slot >= 0 && slot < QuickSlots.Count && quick.GetItemAt(slot, 0) == null
                        && SavedInventory.Move(main, quick, item, new Vector2i(slot, 0)))
                        from = quick;
                }
                bool wear = GearSlots.SlotFor(item) >= 0 ? Plugin.ReequipArmour.Value : Plugin.ReequipWeapons.Value;
                if (worn && wear && wornBy == id && !item.m_equipped && from.ContainsItem(item) && PlaceFree(player, item))
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
            if (gearSlot == GearSlots.UtilitySlot) return MultiUtility.WornCount(player) < 1 + MultiUtility.Allowed;
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
