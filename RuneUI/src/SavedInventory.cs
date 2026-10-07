using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneUI
{
    /// <summary>
    /// A small inventory of the local player's that this mod keeps outside the vanilla one, saved in
    /// the character's custom data, so removing the mod leaves its items in the save rather than
    /// losing them. Its weight counts toward the player's.
    /// </summary>
    internal sealed class SavedInventory
    {
        private readonly string _name;
        private readonly string _label;
        private readonly string _saveKey;
        private readonly int _size;

        private Player _owner;
        private Inventory _inventory;
        // If the saved items could not be read, never write over them.
        private bool _loadFailed;

        /// <summary>Runs after the items were read for a player.</summary>
        public Action<Player, Inventory> Loaded;

        /// <summary>Every one of these inventories, for hooks that treat them all as the player's.</summary>
        public static readonly List<SavedInventory> All = new List<SavedInventory>();

        public SavedInventory(string name, string label, string saveKey, int size)
        {
            All.Add(this);
            _name = name;
            _label = label;
            _saveKey = saveKey;
            _size = size;
        }

        /// <summary>The local player's inventory, or null before a player exists.</summary>
        public Inventory Get()
        {
            var player = Player.m_localPlayer;
            if (player == null) return null;
            if (!ReferenceEquals(player, _owner)) Attach(player);
            return _inventory;
        }

        /// <summary>The inventory as it is, without attaching to a new player.</summary>
        public Inventory Current => _inventory;

        public Player Owner => _owner;

        public bool Owns(Inventory inventory) => inventory != null && ReferenceEquals(inventory, _inventory);

        /// <summary>Whether <paramref name="inventory"/> is the vanilla inventory of this one's player.</summary>
        public bool IsOwnersInventory(Inventory inventory) =>
            inventory != null && _owner != null && ReferenceEquals(inventory, _owner.GetInventory());

        private void Attach(Player player)
        {
            _owner = player;
            _inventory = new Inventory(_name, null, _size, 1);
            _loadFailed = false;
            if (player.m_customData.TryGetValue(_saveKey, out string data) && !string.IsNullOrEmpty(data))
            {
                try
                {
                    _inventory.Load(new ZPackage(data));
                }
                catch (Exception e)
                {
                    _loadFailed = true;
                    Plugin.Log.LogError($"Could not read the saved {_label}; they are left untouched in the save: {e}");
                    return;
                }
            }
            Loaded?.Invoke(player, _inventory);
        }

        /// <summary>Player.Load postfix: read the items from the freshly loaded custom data.</summary>
        public void OnLoad(Player player) => Attach(player);

        /// <summary>Player.Save prefix: write the items into custom data, which vanilla then saves.</summary>
        public void OnSave(Player player)
        {
            if (!ReferenceEquals(player, _owner) || _inventory == null || _loadFailed) return;
            var pkg = new ZPackage();
            _inventory.Save(pkg);
            player.m_customData[_saveKey] = pkg.GetBase64();
        }

        /// <summary>Inventory.GetTotalWeight postfix: the items weigh on the player like the inventory does.</summary>
        public float ExtraWeight(Inventory inventory) =>
            _inventory != null && IsOwnersInventory(inventory) ? _inventory.GetTotalWeight() : 0f;

        /// <summary>
        /// The inventory of these that holds <paramref name="item"/> for the player owning
        /// <paramref name="playerInventory"/>, or null.
        /// </summary>
        public static Inventory Holding(Inventory playerInventory, ItemDrop.ItemData item)
        {
            foreach (var store in All)
                if (store._inventory != null && store.IsOwnersInventory(playerInventory) && store._inventory.ContainsItem(item))
                    return store._inventory;
            return null;
        }

        /// <summary>This player's inventories of these, as they are.</summary>
        public static IEnumerable<Inventory> OwnedBy(Inventory playerInventory)
        {
            foreach (var store in All)
                if (store._inventory != null && store.IsOwnersInventory(playerInventory))
                    yield return store._inventory;
        }

        /// <summary>
        /// Moves <paramref name="item"/> between inventories as the same object, unlike vanilla's
        /// MoveItemToThis, which clones it and so breaks the player's reference to equipped items.
        /// </summary>
        public static bool Move(Inventory from, Inventory to, ItemDrop.ItemData item, Vector2i pos)
        {
            if (to.GetItemAt(pos.x, pos.y) != null || !from.ContainsItem(item)) return false;
            from.RemoveItem(item);
            if (to.AddItem(item, pos)) return true;
            from.AddItem(item, item.m_gridPos);
            return false;
        }

        /// <summary>The first empty cell of <paramref name="inventory"/>, top row first.</summary>
        public static bool FindEmpty(Inventory inventory, out Vector2i pos)
        {
            for (int y = 0; y < inventory.GetHeight(); y++)
            for (int x = 0; x < inventory.GetWidth(); x++)
            {
                if (inventory.GetItemAt(x, y) != null) continue;
                pos = new Vector2i(x, y);
                return true;
            }
            pos = new Vector2i(-1, -1);
            return false;
        }

        public void ResetState()
        {
            _owner = null;
            _inventory = null;
            _loadFailed = false;
        }
    }
}
