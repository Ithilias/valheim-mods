using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RuneUI
{
    /// <summary>
    /// The food slots in the inventory screen: a clone of the player's inventory grid bound to the food
    /// slots, so dragging, splitting, eating and tooltips all work through vanilla's own handlers.
    /// </summary>
    internal static class FoodPouchGui
    {
        private const float Padding = 6f;
        private const float ChestGap = 12f;

        private static readonly AccessTools.FieldRef<InventoryGui, ItemDrop.ItemData> DragItem =
            AccessTools.FieldRefAccess<InventoryGui, ItemDrop.ItemData>("m_dragItem");
        private static readonly AccessTools.FieldRef<InventoryGui, Inventory> DragInventory =
            AccessTools.FieldRefAccess<InventoryGui, Inventory>("m_dragInventory");
        private static readonly AccessTools.FieldRef<InventoryGui, Container> CurrentContainer =
            AccessTools.FieldRefAccess<InventoryGui, Container>("m_currentContainer");
        private static readonly AccessTools.FieldRef<InventoryGrid, List<InventoryElement>> Elements =
            AccessTools.FieldRefAccess<InventoryGrid, List<InventoryElement>>("m_elements");

        private static InventoryGui _gui;
        private static RectTransform _panel;
        private static InventoryGrid _grid;
        private static int _version = -1;

        /// <summary>InventoryGui.UpdateInventory postfix, which runs while the inventory is open.</summary>
        public static void Update(InventoryGui gui, Player player)
        {
            if (!ReferenceEquals(gui, _gui))
            {
                ResetState();
                _gui = gui;
            }
            Inventory pouch = Plugin.ModEnabled.Value && Plugin.FoodSlotsEnabled.Value ? FoodPouch.Get() : null;
            if (pouch == null || player == null)
            {
                Remove();
                return;
            }
            if (_grid == null || _version != Theme.Version) Build(gui);

            // Under the inventory, unless a chest is open: it opens below the inventory, so move right of it.
            bool chestOpen = gui.m_container != null && gui.m_container.gameObject.activeInHierarchy;
            Vector2 corner = chestOpen ? new Vector2(1f, 1f) : Vector2.zero;
            var pos = chestOpen
                // One slot further out, to clear the armour value at the inventory's right edge.
                ? new Vector2(ChestGap + gui.m_playerGrid.m_elementSpace, 0f)
                : new Vector2(Plugin.FoodInventoryOffsetX.Value, Plugin.FoodInventoryOffsetY.Value);
            if (_panel.anchorMin != corner) _panel.anchorMin = _panel.anchorMax = corner;
            if ((_panel.anchoredPosition - pos).sqrMagnitude > 0.01f) _panel.anchoredPosition = pos;

            _grid.UpdateInventory(pouch, player, DragItem(gui));
            var elements = Elements(_grid);
            for (int i = 0; i < elements.Count && i < FoodPouch.Size; i++)
            {
                var binding = elements[i].transform.Find("binding")?.GetComponent<TMPro.TMP_Text>();
                if (binding == null) continue;
                binding.enabled = true;
                string label = FoodPouch.KeyLabel(i);
                if (binding.text != label) binding.text = label;
            }
        }

        /// <summary>
        /// InventoryGui.OnSelectedItem prefix. Keeps everything but food out of the slots, and handles
        /// the one move vanilla gets wrong for a third inventory.
        /// </summary>
        public static bool AllowSelect(InventoryGui gui, InventoryGrid grid, ItemDrop.ItemData item, InventoryGrid.Modifier mod)
        {
            Inventory target = grid.GetInventory();
            ItemDrop.ItemData dragged = DragItem(gui);
            if (dragged != null)
            {
                // Dropping a non-food item onto the slots.
                if (FoodPouch.Owns(target) && !FoodPouch.IsFood(dragged)) return false;
                // Dropping food from the slots onto a non-food item would swap that item into them.
                if (FoodPouch.Owns(DragInventory(gui)) && !FoodPouch.Owns(target) && item != null && !FoodPouch.IsFood(item))
                    return false;
                return true;
            }

            // Shift click with a chest open: vanilla moves from the player inventory, not from the slots,
            // which would copy the food. Move it from the slots instead.
            if (mod == InventoryGrid.Modifier.Move && item != null && FoodPouch.Owns(target))
            {
                Container container = CurrentContainer(gui);
                if (container == null) return true;
                container.GetInventory().MoveItemToThis(target, item);
                return false;
            }
            return true;
        }

        private static void Build(InventoryGui gui)
        {
            Remove();
            _version = Theme.Version;
            InventoryGrid source = gui.m_playerGrid;
            float space = source.m_elementSpace;

            _panel = Theme.NewPanel("RuneUI_FoodSlots", gui.m_player).rectTransform;
            _panel.anchorMin = _panel.anchorMax = Vector2.zero;
            _panel.pivot = new Vector2(0f, 1f);
            _panel.sizeDelta = new Vector2(FoodPouch.Size * space + Padding * 2f, space + Padding * 2f);

            var go = Object.Instantiate(source.gameObject, _panel);
            go.name = "Grid";
            _grid = go.GetComponent<InventoryGrid>();
            // The clone copies the player grid's slot objects but not its list of them; clear them so
            // the grid builds its own three.
            foreach (Transform child in _grid.m_gridRoot)
            {
                child.name = "RuneUI_Removed";
                Object.Destroy(child.gameObject);
            }
            Theme.Stretch((RectTransform)go.transform, Padding);

            _grid.m_uiGroup = source.m_uiGroup;
            _grid.m_onSelected = Handler<Action<InventoryGrid, ItemDrop.ItemData, Vector2i, InventoryGrid.Modifier>>(gui, "OnSelectedItem");
            _grid.m_onReleased = Handler<Action<InventoryGrid, ItemDrop.ItemData, Vector2i>>(gui, "OnReleasedItem");
            _grid.m_onRightClick = Handler<Action<InventoryGrid, ItemDrop.ItemData, Vector2i>>(gui, "OnRightClickItem");
            _grid.m_onEnter = Handler<Action<InventoryGrid, Vector2i>>(gui, "OnEnterElement");
            _grid.CanDropDragOntoItem = Handler<Func<ItemDrop.ItemData, bool>>(gui, "CanDropDragOntoItem");
            _grid.OnMoveToUpperInventoryGrid = null;
            _grid.OnMoveToLowerInventoryGrid = null;
            _grid.ResetView();
        }

        private static T Handler<T>(InventoryGui gui, string method) where T : Delegate =>
            AccessTools.MethodDelegate<T>(AccessTools.Method(typeof(InventoryGui), method), gui);

        public static void Remove()
        {
            if (_panel != null) Object.Destroy(_panel.gameObject);
            _panel = null;
            _grid = null;
        }

        public static void ResetState()
        {
            _gui = null;
            _panel = null;
            _grid = null;
        }
    }
}
