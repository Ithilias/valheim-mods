using UnityEngine;

namespace RuneUI
{
    /// <summary>
    /// Extra inventory rows and base carry weight. Both are only applied, never saved: vanilla's own
    /// row count stays what the game set, and removing the mod gives the vanilla values back.
    /// </summary>
    internal static class Balance
    {
        private const float VanillaCarryWeight = 300f;

        private static Player _player;
        private static float _ownCarryWeight;
        private static bool _carryApplied;
        private static int _appliedRows = -1;
        private static int _baseRows = -1;

        /// <summary>The row count vanilla has saved for the player, 4 unless the game changed it.</summary>
        public static int VanillaRows(Player player) =>
            player.TryGetUniqueKeyValue(Player.InventoryRowsKey, out string value) && int.TryParse(value, out int rows)
                ? Mathf.Clamp(rows, 0, 9)
                : 4;

        /// <summary>Rows shown for a vanilla row count: the extra rows on top, at most the game's 9.</summary>
        public static int Rows(int vanillaRows) => Mathf.Clamp(vanillaRows + Plugin.ExtraInventoryRows.Value, 0, 9);

        /// <summary>
        /// Player.SetInventorySize prefix: add the extra rows. Items in rows that go away move to free
        /// cells first; vanilla drops what is left on the ground.
        /// </summary>
        public static void BeforeSetSize(Player player, ref int rows)
        {
            _baseRows = Mathf.Clamp(rows, 0, 9);
            rows = Rows(_baseRows);
            Inventory inventory = player.GetInventory();
            // Better Archery keeps its quiver rows below whatever count it is given.
            int height = rows + BetterArcheryCompat.ReservedRows;
            if (height < inventory.GetHeight()) DeathKeeper.ShrinkTo(inventory, height);
            _appliedRows = rows;
        }

        /// <summary>Player.SetInventorySize postfix: vanilla saved the total; save its own count instead.</summary>
        public static void AfterSetSize(Player player)
        {
            if (_baseRows >= 0) player.AddUniqueKeyValue(Player.InventoryRowsKey, _baseRows.ToString());
            _baseRows = -1;
        }

        /// <summary>Called after Player.Update for the local player: follow setting changes.</summary>
        public static void Update(Player player)
        {
            if (!ReferenceEquals(player, _player))
            {
                _player = player;
                _carryApplied = false;
                _appliedRows = player.GetInventory().GetHeight();
            }

            int rows = Rows(VanillaRows(player));
            if (rows != _appliedRows && InventoryGui.instance != null) player.SetInventorySize(VanillaRows(player));

            // At the vanilla value, leave the carry weight to the game and other mods.
            float wanted = Plugin.BaseCarryWeight.Value;
            if (!Mathf.Approximately(wanted, VanillaCarryWeight))
            {
                if (!_carryApplied)
                {
                    _ownCarryWeight = player.m_maxCarryWeight;
                    _carryApplied = true;
                }
                if (!Mathf.Approximately(player.m_maxCarryWeight, wanted)) player.m_maxCarryWeight = wanted;
            }
            else if (_carryApplied)
            {
                player.m_maxCarryWeight = _ownCarryWeight;
                _carryApplied = false;
            }
        }

        public static void ResetState()
        {
            _player = null;
            _carryApplied = false;
            _appliedRows = -1;
        }
    }
}
