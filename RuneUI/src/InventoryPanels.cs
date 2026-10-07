using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RuneUI
{
    /// <summary>
    /// The quick and gear slots in the inventory screen: clones of the player's inventory grid bound to
    /// those slots, so dragging, splitting, eating and tooltips all work through vanilla's own handlers.
    /// </summary>
    internal static class InventoryPanels
    {
        private const float Padding = 6f;
        private const float ChestGap = 12f;
        private const float PanelGap = 6f;

        private static readonly AccessTools.FieldRef<InventoryGui, ItemDrop.ItemData> DragItem =
            AccessTools.FieldRefAccess<InventoryGui, ItemDrop.ItemData>("m_dragItem");
        private static readonly AccessTools.FieldRef<InventoryGui, Inventory> DragInventory =
            AccessTools.FieldRefAccess<InventoryGui, Inventory>("m_dragInventory");
        private static readonly AccessTools.FieldRef<InventoryGui, Container> CurrentContainer =
            AccessTools.FieldRefAccess<InventoryGui, Container>("m_currentContainer");
        private static readonly AccessTools.FieldRef<InventoryGrid, List<InventoryElement>> Elements =
            AccessTools.FieldRefAccess<InventoryGrid, List<InventoryElement>>("m_elements");

        private static readonly SlotPanel Quick = new SlotPanel("RuneUI_QuickSlots");
        private static readonly SlotPanel Gear = new SlotPanel("RuneUI_GearSlots");
        private static InventoryGui _gui;

        // The panel being dragged with the drag key, and the settings that hold its position.
        private static SlotPanel _dragging;
        private static ConfigEntry<float> _dragX, _dragY;
        private static Vector2 _dragStartMouse, _dragStartOffset, _dragOffset;

        /// <summary>InventoryGui.UpdateInventory postfix, which runs while the inventory is open.</summary>
        public static void Update(InventoryGui gui, Player player)
        {
            if (!ReferenceEquals(gui, _gui))
            {
                ResetState();
                _gui = gui;
            }
            bool on = Plugin.ModEnabled.Value && player != null;
            Inventory quick = on && QuickSlots.Count > 0 ? QuickSlots.Get() : null;
            Inventory gear = on && Plugin.GearSlotsEnabled.Value ? GearSlots.Get() : null;

            // Right of the inventory, one slot further out to clear the armour value at its right edge.
            var right = new Vector2(1f, 1f);
            var besideInventory = new Vector2(ChestGap + gui.m_playerGrid.m_elementSpace, 0f);

            // Under the inventory, unless a chest is open: it opens below the inventory, so move right of it.
            bool chestOpen = gui.m_container != null && gui.m_container.gameObject.activeInHierarchy;
            ConfigEntry<float> quickX = chestOpen ? Plugin.QuickChestOffsetX : Plugin.QuickInventoryOffsetX;
            ConfigEntry<float> quickY = chestOpen ? Plugin.QuickChestOffsetY : Plugin.QuickInventoryOffsetY;
            if (quick != null)
            {
                Vector2 corner = chestOpen ? right : Vector2.zero;
                Vector2 spot = chestOpen ? besideInventory : Vector2.zero;
                Quick.Update(gui, quick, player, corner, spot + Offset(Quick, quickX, quickY), QuickSlots.KeyLabel, null);
                // The gear stays put under the spot the quick slots take beside the inventory.
                besideInventory.y -= Quick.Height + PanelGap;
            }
            else
            {
                Quick.Remove();
            }
            if (gear != null)
                Gear.Update(gui, gear, player, right, besideInventory + Offset(Gear, Plugin.GearOffsetX, Plugin.GearOffsetY),
                    null, GearSlots.Placeholder);
            else Gear.Remove();

            UpdateDrag(gui, quick != null ? quickX : null, quick != null ? quickY : null);
        }

        /// <summary>A panel's offset: from its settings, or where it is being dragged to.</summary>
        private static Vector2 Offset(SlotPanel panel, ConfigEntry<float> x, ConfigEntry<float> y) =>
            ReferenceEquals(panel, _dragging) ? _dragOffset : new Vector2(x.Value, y.Value);

        /// <summary>Whether the panel drag key is held, so clicks on the panels move them instead.</summary>
        public static bool DragKeyHeld => QuickKeys.Check(Plugin.PanelDragKey.Value, true);

        /// <summary>
        /// Holding the drag key, a panel follows the mouse while the left button is held. The settings
        /// are written once on release, since every setting change rebuilds what this mod draws.
        /// </summary>
        private static void UpdateDrag(InventoryGui gui, ConfigEntry<float> quickX, ConfigEntry<float> quickY)
        {
            var space = (RectTransform)gui.m_player;
            Vector2 mouse = ZInput.pointerPosition;
            Camera camera = null;
            Canvas canvas = space.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) camera = canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(space, mouse, camera, out Vector2 local);

            if (_dragging != null)
            {
                if (ZInput.GetMouseButton(0) && DragKeyHeld)
                {
                    _dragOffset = _dragStartOffset + (local - _dragStartMouse);
                    return;
                }
                _dragX.Value = Mathf.Round(_dragOffset.x);
                _dragY.Value = Mathf.Round(_dragOffset.y);
                _dragging = null;
                return;
            }

            if (!ZInput.GetMouseButtonDown(0) || !DragKeyHeld) return;
            if (quickX != null && Quick.Contains(mouse, camera)) Start(Quick, quickX, quickY, local);
            else if (Gear.Contains(mouse, camera)) Start(Gear, Plugin.GearOffsetX, Plugin.GearOffsetY, local);
        }

        private static void Start(SlotPanel panel, ConfigEntry<float> x, ConfigEntry<float> y, Vector2 mouse)
        {
            _dragging = panel;
            _dragX = x;
            _dragY = y;
            _dragStartMouse = mouse;
            _dragStartOffset = _dragOffset = new Vector2(x.Value, y.Value);
        }

        /// <summary>InventoryGui.OnSelectedItem prefix: with the drag key held, clicks on the panels only move them.</summary>
        public static bool Dragging(InventoryGrid grid) => Ours(grid.GetInventory()) && DragKeyHeld;

        private static bool Ours(Inventory inventory) => QuickSlots.Owns(inventory) || GearSlots.Owns(inventory);

        /// <summary>Whether <paramref name="item"/> may go to <paramref name="pos"/> in <paramref name="inventory"/>.</summary>
        private static bool Fits(Inventory inventory, Vector2i pos, ItemDrop.ItemData item)
        {
            if (QuickSlots.Owns(inventory)) return QuickSlots.Fits(item);
            if (GearSlots.Owns(inventory)) return GearSlots.Fits(item, pos.x);
            return true;
        }

        /// <summary>
        /// InventoryGui.OnSelectedItem prefix. Keeps each slot to what belongs there, and handles the
        /// one move vanilla gets wrong for a third inventory.
        /// </summary>
        public static bool AllowSelect(InventoryGui gui, InventoryGrid grid, ItemDrop.ItemData item, Vector2i pos,
            InventoryGrid.Modifier mod, out Inventory dragFrom, out ItemDrop.ItemData dragged)
        {
            Inventory target = grid.GetInventory();
            dragged = DragItem(gui);
            dragFrom = DragInventory(gui);
            if (dragged != null)
            {
                if (!Fits(target, pos, dragged)) return false;
                // Dropping onto another item swaps it back to where the dragged one came from.
                if (item != null && !ReferenceEquals(item, dragged) && !Fits(dragFrom, dragged.m_gridPos, item))
                    return false;
                return true;
            }

            // Ctrl click with a chest open: vanilla moves from the player inventory, not from these slots,
            // which would copy the item. Move it from the slots instead.
            if (mod == InventoryGrid.Modifier.Move && item != null && Ours(target))
            {
                Container container = CurrentContainer(gui);
                if (container == null) return true;
                if (item.m_equipped) Player.m_localPlayer?.UnequipItem(item);
                container.GetInventory().MoveItemToThis(target, item);
                return false;
            }
            return true;
        }

        /// <summary>
        /// InventoryGui.UpdateContainer prefix. With no chest open, vanilla cancels every drag that does
        /// not come from the player inventory, which drops items picked up from the slots the next frame.
        /// Shows vanilla the player inventory for that check; returns the real one to restore.
        /// </summary>
        public static Inventory HideDragFromSlots(InventoryGui gui)
        {
            Inventory dragged = DragInventory(gui);
            if (!Ours(dragged) || Player.m_localPlayer == null) return null;
            DragInventory(gui) = Player.m_localPlayer.GetInventory();
            return dragged;
        }

        /// <summary>InventoryGui.UpdateContainer finalizer: undoes <see cref="HideDragFromSlots"/>.</summary>
        public static void RestoreDragFromSlots(InventoryGui gui, Inventory dragged)
        {
            if (dragged != null && DragItem(gui) != null) DragInventory(gui) = dragged;
        }

        public static void Remove()
        {
            Quick.Remove();
            Gear.Remove();
        }

        public static void ResetState()
        {
            _gui = null;
            _dragging = null;
            Quick.Forget();
            Gear.Forget();
        }

        /// <summary>One row of slots: a panel with a clone of the player's inventory grid in it.</summary>
        private sealed class SlotPanel
        {
            private readonly string _name;
            private int _size;
            private RectTransform _panel;
            private InventoryGrid _grid;
            private int _version = -1;

            public SlotPanel(string name)
            {
                _name = name;
            }

            public float Height => _panel != null ? _panel.sizeDelta.y : 0f;

            public bool Contains(Vector2 screen, Camera camera) =>
                _panel != null && _panel.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(_panel, screen, camera);

            /// <param name="label">Text in each slot's corner, or null for none.</param>
            /// <param name="placeholder">Icon shown in each empty slot, or null for none.</param>
            public void Update(InventoryGui gui, Inventory inventory, Player player, Vector2 corner, Vector2 pos,
                Func<int, string> label, Func<int, Sprite> placeholder)
            {
                if (_grid == null || _version != Theme.Version || _size != inventory.GetWidth())
                {
                    _size = inventory.GetWidth();
                    Build(gui);
                }
                if (_panel.anchorMin != corner) _panel.anchorMin = _panel.anchorMax = corner;
                if ((_panel.anchoredPosition - pos).sqrMagnitude > 0.01f) _panel.anchoredPosition = pos;

                ItemDrop.ItemData dragged = DragItem(gui);
                _grid.UpdateInventory(inventory, player, dragged);
                var elements = Elements(_grid);
                for (int i = 0; i < elements.Count && i < _size; i++)
                {
                    Transform slot = elements[i].transform;
                    var binding = slot.Find("binding")?.GetComponent<TMPro.TMP_Text>();
                    if (binding != null)
                    {
                        binding.enabled = label != null;
                        string text = label != null ? label(i) : "";
                        if (binding.text != text) binding.text = text;
                    }

                    Sprite icon = placeholder != null && inventory.GetItemAt(i, 0) == null ? placeholder(i) : null;
                    Image empty = Child(slot, PlaceholderName, icon != null, BuildPlaceholder);
                    if (empty != null && empty.sprite != icon) empty.sprite = icon;

                    // Every slot the item in hand may go into lights up while it is held.
                    bool fits = dragged != null && Fits(inventory, new Vector2i(i, 0), dragged);
                    Child(slot, HighlightName, fits, BuildHighlight);
                }
            }

            private const string PlaceholderName = "RuneUI_Placeholder";
            private const string HighlightName = "RuneUI_Highlight";

            /// <summary>The named child of a slot, built when first needed, shown or hidden.</summary>
            private static Image Child(Transform slot, string name, bool show, Func<Transform, Image> build)
            {
                Transform child = slot.Find(name);
                if (child == null)
                {
                    if (!show) return null;
                    child = build(slot).transform;
                }
                if (child.gameObject.activeSelf != show) child.gameObject.SetActive(show);
                return child.GetComponent<Image>();
            }

            /// <summary>A faint icon in the middle of the slot, under where the item's icon would be.</summary>
            private static Image BuildPlaceholder(Transform slot)
            {
                var rt = Theme.NewRect(PlaceholderName, slot);
                rt.anchorMin = new Vector2(0.22f, 0.22f);
                rt.anchorMax = new Vector2(0.78f, 0.78f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var img = rt.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                img.preserveAspect = true;
                img.color = new Color(1f, 1f, 1f, 0.25f);
                Transform icon = slot.Find("icon");
                if (icon != null) rt.SetSiblingIndex(icon.GetSiblingIndex());
                return img;
            }

            private static Image BuildHighlight(Transform slot)
            {
                var img = Theme.NewImage(HighlightName, slot, Theme.RingSprite, Plugin.AccentColor.Value);
                Theme.Stretch(img.rectTransform);
                img.transform.SetAsLastSibling();
                return img;
            }

            private void Build(InventoryGui gui)
            {
                Remove();
                _version = Theme.Version;
                InventoryGrid source = gui.m_playerGrid;
                float space = source.m_elementSpace;

                _panel = Theme.NewPanel(_name, gui.m_player).rectTransform;
                _panel.anchorMin = _panel.anchorMax = Vector2.zero;
                _panel.pivot = new Vector2(0f, 1f);
                _panel.sizeDelta = new Vector2(_size * space + Padding * 2f, space + Padding * 2f);

                var go = Object.Instantiate(source.gameObject, _panel);
                go.name = "Grid";
                _grid = go.GetComponent<InventoryGrid>();
                // The clone copies the player grid's slot objects but not its list of them; clear them so
                // the grid builds its own.
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

            public void Remove()
            {
                if (_panel != null) Object.Destroy(_panel.gameObject);
                _panel = null;
                _grid = null;
            }

            public void Forget()
            {
                _panel = null;
                _grid = null;
            }
        }
    }
}
