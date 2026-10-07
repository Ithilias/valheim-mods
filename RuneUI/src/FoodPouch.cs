using UnityEngine.InputSystem;

namespace RuneUI
{
    /// <summary>
    /// Three extra inventory slots that only take food, saved with the character (see
    /// <see cref="SavedInventory"/>). They go into the tombstone on death; see <see cref="DeathKeeper"/>.
    /// </summary>
    internal static class FoodPouch
    {
        public const int Size = 3;

        private static readonly SavedInventory Store =
            new SavedInventory("$item_food", "food slots", "ithilias.runeui.foodslots", Size);

        private static readonly Key[] OtherKeys = { Key.V, Key.B };

        /// <summary>The local player's food slots, or null before a player exists.</summary>
        public static Inventory Get() => Store.Get();

        /// <summary>The slots as they are, without attaching to a new player.</summary>
        public static Inventory Current => Store.Current;

        public static bool Owns(Inventory inventory) => Store.Owns(inventory);

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

        public static void OnLoad(Player player) => Store.OnLoad(player);

        public static void OnSave(Player player) => Store.OnSave(player);

        public static float ExtraWeight(Inventory inventory) => Store.ExtraWeight(inventory);

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

        public static void ResetState() => Store.ResetState();
    }
}
