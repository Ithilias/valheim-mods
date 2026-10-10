using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;

namespace RuneUI
{
    /// <summary>
    /// Better Archery (2.x) keeps its quiver in two rows it adds below the player inventory, hidden
    /// from the grid, with the arrows in the first three cells of the last one. It shows those cells
    /// and a quiver HUD itself. Rune UI only has to leave the two rows alone: never treat them as free
    /// space or as stray items, and keep them when it changes the row count.
    /// </summary>
    internal static class BetterArcheryCompat
    {
        private const string Guid = "ishid4.mods.betterarchery";
        private const int QuiverRows = 2;

        private static bool _looked;
        private static ConfigEntry<bool> _quiverEnabled;
        private static Func<bool> _expansionModPresent;
        private static FieldInfo _quiverRowIndex;

        private static void Look()
        {
            if (_looked) return;
            _looked = true;
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out var plugin) || plugin.Instance == null) return;
            try
            {
                Type type = plugin.Instance.GetType();
                _quiverEnabled = AccessTools.Field(type, "ConfigQuiverEnabled")?.GetValue(null) as ConfigEntry<bool>;
                MethodInfo expansion = AccessTools.PropertyGetter(type, "isInventoryExpansionModPresent");
                if (expansion != null) _expansionModPresent = AccessTools.MethodDelegate<Func<bool>>(expansion);
                _quiverRowIndex = AccessTools.Field(type, "QuiverRowIndex");
                if (_quiverEnabled == null || _quiverRowIndex == null)
                    Plugin.Log.LogWarning("Better Archery found, but not the version Rune UI knows; its quiver rows may not be respected.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Reading Better Archery's quiver settings failed: {e}");
            }
        }

        /// <summary>Rows Better Archery keeps below the player inventory: 2 while its quiver is on, else 0.</summary>
        public static int ReservedRows
        {
            get
            {
                Look();
                if (_quiverEnabled == null || !_quiverEnabled.Value) return 0;
                if (_expansionModPresent != null && _expansionModPresent()) return 0;
                return _quiverRowIndex != null && (int)_quiverRowIndex.GetValue(null) > 0 ? QuiverRows : 0;
            }
        }

        /// <summary>Rows of <paramref name="inventory"/> free for items: all, minus Better Archery's for the player's.</summary>
        public static int UsableRows(Inventory inventory) =>
            inventory.GetHeight() - (IsPlayerInventory(inventory) ? ReservedRows : 0);

        private static bool IsPlayerInventory(Inventory inventory)
        {
            if (Player.m_localPlayer != null && ReferenceEquals(Player.m_localPlayer.GetInventory(), inventory)) return true;
            foreach (var store in SavedInventory.All)
                if (store.IsOwnersInventory(inventory)) return true;
            return false;
        }
    }
}
