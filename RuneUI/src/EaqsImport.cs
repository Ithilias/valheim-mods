using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneUI
{
    /// <summary>
    /// Takes over a character from Equipment and Quick Slots. That mod kept its slots as hidden rows
    /// below the inventory; without it, vanilla drops everything outside the grid on the ground when
    /// the player spawns. This runs just before that and moves those items into the quick and gear
    /// slots, or into free inventory cells.
    /// </summary>
    internal static class EaqsImport
    {
        // Written by Equipment and Quick Slots on every save to each item in one of its slots.
        private const string SlotKey = "eaqs_slot";
        private const string PlayerKey = "eaqs_player";
        private const string BackupKey = "eaqs_backup";
        private const int Width = 8;

        private static readonly string[] GearIds = { "Helmet", "Chest", "Legs", "Shoulder", "Utility", "Trinket" };

        /// <summary>Player.OnSpawned prefix: <paramref name="rows"/> is the inventory height vanilla is about to set.</summary>
        public static void Run(Player player, int rows)
        {
            Inventory main = player.GetInventory();
            var stray = new List<ItemDrop.ItemData>();
            foreach (var item in main.GetAllItems())
                if (item.m_gridPos.x < 0 || item.m_gridPos.y < 0 || item.m_gridPos.x >= main.GetWidth() || item.m_gridPos.y >= rows)
                    stray.Add(item);
            if (stray.Count == 0) return;

            int slotRow = BackupRows(player) ?? rows;
            string id = player.GetPlayerID().ToString();
            Inventory quick = null, gear = null;
            foreach (Inventory slots in SavedInventory.OwnedBy(main))
            {
                if (QuickSlots.Owns(slots)) quick = slots;
                if (GearSlots.Owns(slots)) gear = slots;
            }

            int moved = 0, left = 0;
            GearSlots.Suspend();
            try
            {
                foreach (var item in stray)
                {
                    string slot = SlotOf(item, id, slotRow);
                    item.m_customData.Remove(SlotKey);
                    item.m_customData.Remove(PlayerKey);
                    if (Place(player, main, quick, gear, item, slot, rows)) moved++;
                    else left++;
                }
            }
            finally
            {
                GearSlots.Resume();
            }
            Plugin.Log.LogInfo($"Took over {moved} items from Equipment and Quick Slots' hidden rows.");
            if (left > 0)
                Plugin.Log.LogWarning($"{left} items from Equipment and Quick Slots did not fit; the game drops them where you spawn.");
        }

        /// <summary>The slot an item was in: from its marker, else from where it sits below the grid.</summary>
        private static string SlotOf(ItemDrop.ItemData item, string playerId, int slotRow)
        {
            if (item.m_customData.TryGetValue(SlotKey, out string slot) &&
                item.m_customData.TryGetValue(PlayerKey, out string owner) && owner == playerId)
                return slot;
            int index = (item.m_gridPos.y - slotRow) * Width + item.m_gridPos.x;
            if (item.m_gridPos.y < slotRow) return null;
            if (index >= 0 && index < 6) return "Quick" + (index + 1);
            if (index >= 8 && index < 8 + GearIds.Length) return GearIds[index - 8];
            return null;
        }

        private static bool Place(Player player, Inventory main, Inventory quick, Inventory gear,
            ItemDrop.ItemData item, string slot, int rows)
        {
            if (slot != null && quick != null && slot.StartsWith("Quick") && int.TryParse(slot.Substring(5), out int n)
                && n >= 1 && n <= QuickSlots.MaxSize && QuickSlots.Fits(item) && quick.GetItemAt(n - 1, 0) == null
                && SavedInventory.Move(main, quick, item, new Vector2i(n - 1, 0)))
                return true;

            int gearSlot = slot != null ? Array.IndexOf(GearIds, slot) : -1;
            if (gearSlot >= 0 && gear != null && GearSlots.SlotFor(item) == gearSlot && gear.GetItemAt(gearSlot, 0) == null
                && SavedInventory.Move(main, gear, item, new Vector2i(gearSlot, 0)))
            {
                // What is in a gear slot is worn.
                if (!item.m_equipped) player.EquipItem(item, false);
                return true;
            }

            Vector2i at = item.m_gridPos;
            if (at.x >= 0 && at.y >= 0 && at.x < main.GetWidth() && at.y < rows) return true;
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < main.GetWidth(); x++)
            {
                if (main.GetItemAt(x, y) != null) continue;
                main.RemoveItem(item);
                main.AddItem(item, new Vector2i(x, y));
                return true;
            }
            return false;
        }

        /// <summary>Where Equipment and Quick Slots' rows began, from the header of its backup.</summary>
        private static int? BackupRows(Player player)
        {
            if (!player.m_customData.TryGetValue(BackupKey, out string data) || string.IsNullOrEmpty(data)) return null;
            try
            {
                var pkg = new ZPackage(data);
                if (pkg.ReadInt() != 1) return null;
                pkg.ReadString();
                pkg.ReadString();
                pkg.ReadInt();
                pkg.ReadInt();
                pkg.ReadInt();
                int rows = pkg.ReadInt();
                return rows > 0 ? rows : (int?)null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Puts the items from Equipment and Quick Slots' backup into the slots or free cells, then
        /// forgets the backup. For items vanilla dropped or lost before this mod took over; it can
        /// duplicate items that still exist, so it is a cheat command.
        /// </summary>
        public static string RestoreBackup(Player player)
        {
            if (!player.m_customData.TryGetValue(BackupKey, out string data) || string.IsNullOrEmpty(data))
                return "No Equipment and Quick Slots backup on this character.";
            var pkg = new ZPackage(data);
            if (pkg.ReadInt() != 1) return "Unknown Equipment and Quick Slots backup format.";
            pkg.ReadString();
            pkg.ReadString();
            pkg.ReadInt();
            int width = pkg.ReadInt();
            int height = pkg.ReadInt();
            pkg.ReadInt();
            var backup = new Inventory(BackupKey, null, width, height);
            backup.Load(pkg.ReadCompressedPackage());

            Inventory main = player.GetInventory();
            int restored = 0, rows = main.GetHeight();
            Inventory quick = QuickSlots.Get(), gear = GearSlots.Get();
            string id = player.GetPlayerID().ToString();
            foreach (var original in backup.GetAllItems())
            {
                ItemDrop.ItemData item = original.Clone();
                item.m_equipped = false;
                if (!SavedInventory.FindEmpty(main, out Vector2i pos)) break;
                main.AddItem(item, pos);
                string slot = SlotOf(original, id, 0);
                item.m_customData.Remove(SlotKey);
                item.m_customData.Remove(PlayerKey);
                GearSlots.Suspend();
                try { Place(player, main, quick, gear, item, slot, rows); }
                finally { GearSlots.Resume(); }
                restored++;
            }
            if (restored == backup.NrOfItems()) player.m_customData.Remove(BackupKey);
            return $"Restored {restored} of {backup.NrOfItems()} items from the Equipment and Quick Slots backup.";
        }
    }
}
