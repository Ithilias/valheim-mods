using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RuneUI
{
    /// <summary>
    /// Three extra inventory slots that only take food. They live in their own inventory, saved in the
    /// character's custom data, so removing the mod leaves them in the save rather than losing them.
    /// Their weight counts toward the player's, and they go into the tombstone on death.
    /// </summary>
    internal static class FoodPouch
    {
        public const int Size = 3;
        private const string SaveKey = "ithilias.runeui.foodslots";

        private static Player _owner;
        private static Inventory _pouch;
        // If the saved slots could not be read, never write over them.
        private static bool _loadFailed;

        private static readonly Key[] OtherKeys = { Key.U, Key.B };

        /// <summary>The local player's food slots, or null before a player exists.</summary>
        public static Inventory Get()
        {
            var player = Player.m_localPlayer;
            if (player == null) return null;
            if (!ReferenceEquals(player, _owner)) Attach(player);
            return _pouch;
        }

        public static bool Owns(Inventory inventory) => inventory != null && ReferenceEquals(inventory, _pouch);

        public static bool IsFood(ItemDrop.ItemData item) =>
            item != null && (item.m_shared.m_food > 0f || item.m_shared.m_foodStamina > 0f || item.m_shared.m_foodEitr > 0f);

        public static Key KeyFor(int slot) =>
            slot == 0 ? (Plugin.FoodFirstKey.Value == FirstFoodKey.Y ? Key.Y : Key.Z) : OtherKeys[slot - 1];

        /// <summary>The key's label on the player's keyboard layout.</summary>
        public static string KeyLabel(int slot)
        {
            Key key = KeyFor(slot);
            var keyboard = Keyboard.current;
            string name = keyboard != null ? keyboard[key].displayName : null;
            return string.IsNullOrEmpty(name) ? key.ToString() : name.ToUpperInvariant();
        }

        private static void Attach(Player player)
        {
            _owner = player;
            _pouch = new Inventory("$item_food", null, Size, 1);
            _loadFailed = false;
            if (!player.m_customData.TryGetValue(SaveKey, out string data) || string.IsNullOrEmpty(data)) return;
            try
            {
                _pouch.Load(new ZPackage(data));
            }
            catch (Exception e)
            {
                _loadFailed = true;
                Plugin.Log.LogError($"Could not read the saved food slots; they are left untouched in the save: {e}");
            }
        }

        /// <summary>Player.Load postfix: read the slots from the freshly loaded custom data.</summary>
        public static void OnLoad(Player player) => Attach(player);

        /// <summary>Player.Save prefix: write the slots into custom data, which vanilla then saves.</summary>
        public static void OnSave(Player player)
        {
            if (!ReferenceEquals(player, _owner) || _pouch == null || _loadFailed) return;
            var pkg = new ZPackage();
            _pouch.Save(pkg);
            player.m_customData[SaveKey] = pkg.GetBase64();
        }

        /// <summary>Inventory.GetTotalWeight postfix: the slots weigh on the player like the inventory does.</summary>
        public static float ExtraWeight(Inventory inventory)
        {
            if (_owner == null || _pouch == null || !ReferenceEquals(inventory, _owner.GetInventory())) return 0f;
            return _pouch.GetTotalWeight();
        }

        /// <summary>
        /// Player.CreateTombStone prefix: move the food into the inventory so vanilla's death rules
        /// (tombstone, keep or delete) apply to it. Whatever does not fit is dropped where the player died.
        /// </summary>
        public static void OnDeath(Player player)
        {
            if (!ReferenceEquals(player, _owner) || _pouch == null || _pouch.NrOfItems() == 0) return;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.DeathKeepInventory)) return;

            Inventory inventory = player.GetInventory();
            foreach (var item in _pouch.GetAllItems().ToArray())
            {
                inventory.MoveItemToThis(_pouch, item);
                if (!_pouch.ContainsItem(item)) continue;
                _pouch.RemoveItem(item);
                ItemDrop.DropItem(item, item.m_stack, player.GetCenterPoint(), Quaternion.identity);
            }
        }

        /// <summary>Called after Player.Update for the local player.</summary>
        public static void HandleInput(Player player)
        {
            if (!Plugin.FoodSlotsEnabled.Value) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            Inventory pouch = Get();
            if (pouch == null) return;
            for (int i = 0; i < Size; i++)
            {
                if (!keyboard[KeyFor(i)].wasPressedThisFrame) continue;
                ItemDrop.ItemData item = pouch.GetItemAt(i, 0);
                if (item != null) player.UseItem(pouch, item, false);
            }
        }

        public static void ResetState()
        {
            _owner = null;
            _pouch = null;
            _loadFailed = false;
        }
    }
}
