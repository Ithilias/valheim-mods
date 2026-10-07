using System.Collections.Generic;
using UnityEngine;

namespace RuneUI
{
    /// <summary>Console commands for the quick and gear slots.</summary>
    internal static class Commands
    {
        private static bool _registered;

        /// <summary>Terminal.InitTerminal postfix, which runs on every terminal; register once.</summary>
        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            new Terminal.ConsoleCommand("runeui_slots", "Lists what is in Rune UI's quick and gear slots", args =>
            {
                Player player = Player.m_localPlayer;
                if (player == null)
                {
                    args.Context.AddString("No local player.");
                    return;
                }
                List("Quick slots", QuickSlots.Get(), args.Context);
                List("Gear slots", GearSlots.Get(), args.Context);
            });

            new Terminal.ConsoleCommand("runeui_emptyslots",
                "Takes off and moves everything in Rune UI's quick and gear slots into the inventory, for example before removing the mod",
                args =>
                {
                    Player player = Player.m_localPlayer;
                    if (player == null)
                    {
                        args.Context.AddString("No local player.");
                        return;
                    }
                    int left = Empty(player, QuickSlots.Get()) + Empty(player, GearSlots.Get());
                    args.Context.AddString(left == 0
                        ? "The quick and gear slots are empty."
                        : $"{left} items did not fit into the inventory and stay in their slots.");
                });

            new Terminal.ConsoleCommand("runeui_eaqs_restore",
                "Restores the items in Equipment and Quick Slots' backup on this character. Can duplicate items that still exist",
                args =>
                {
                    Player player = Player.m_localPlayer;
                    args.Context.AddString(player == null ? "No local player." : EaqsImport.RestoreBackup(player));
                }, isCheat: true);
        }

        private static void List(string title, Inventory slots, Terminal context)
        {
            if (slots == null) return;
            context.AddString($"{title}:");
            foreach (var item in slots.GetAllItems())
                context.AddString($"  {item.m_gridPos.x + 1}: {Localization.instance.Localize(item.m_shared.m_name)} x{item.m_stack}" +
                    (item.m_equipped ? " (worn)" : ""));
        }

        /// <summary>Moves every item out of <paramref name="slots"/>; returns how many did not fit.</summary>
        private static int Empty(Player player, Inventory slots)
        {
            if (slots == null) return 0;
            Inventory main = player.GetInventory();
            int left = 0;
            GearSlots.Suspend();
            try
            {
                foreach (var item in new List<ItemDrop.ItemData>(slots.GetAllItems()))
                {
                    if (!SavedInventory.FindEmpty(main, out Vector2i pos))
                    {
                        left++;
                        continue;
                    }
                    if (item.m_equipped) player.UnequipItem(item);
                    SavedInventory.Move(slots, main, item, pos);
                }
            }
            finally
            {
                GearSlots.Resume();
            }
            return left;
        }
    }
}
