using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RuneUI
{
    /// <summary>
    /// A second hotbar showing inventory row 2, used with the modifier key plus 1 to 8, with the
    /// modifier shown once on its left. The food slots extend it on the right.
    /// </summary>
    internal static class QuickBar2
    {
        private const int Row = 1;
        private const float ExtensionGap = 14f;

        private static readonly Func<Player, bool> TakeInputMethod =
            AccessTools.MethodDelegate<Func<Player, bool>>(AccessTools.Method(typeof(Player), "TakeInput"));

        private static RectTransform _root;
        private static readonly HotbarSlot[] Slots = new HotbarSlot[Hotbar.SlotCount];
        private static readonly HotbarSlot[] FoodSlots = new HotbarSlot[FoodPouch.Size];
        private static TMP_Text _modifierLabel;
        private static int _version = -1;
        private static bool _builtWithFood;

        public static bool ModifierHeld =>
            Plugin.ModEnabled.Value && Plugin.QuickBarEnabled.Value && ZInput.GetKey(Plugin.QuickBarModifier.Value, false);

        /// <summary>The game's own check for whether the player may act on key presses right now.</summary>
        public static bool CanTakeInput(Player player) =>
            TakeInputMethod(player) && !Hud.IsPieceSelectionVisible() && !Hud.InRadial();

        public static void Update(Hud hud, Player player)
        {
            if (!Plugin.QuickBarEnabled.Value || player == null)
            {
                Remove();
                return;
            }
            var bar = Hotbar.Find(hud);
            if (bar == null) return;
            if (_root == null || _version != Theme.Version || _builtWithFood != Plugin.FoodSlotsEnabled.Value)
                Build(bar);

            if (Hotbar.Stacking && Hotbar.TryEdges(_root.parent, out float hotbarBottom, out _))
            {
                // Under the hotbar; this bar's own offsets only nudge it from there.
                float top = Theme.Bounds((RectTransform)Slots[0].Go.transform, _root.parent).yMax;
                Theme.PlaceStacked(_root, Plugin.HotbarOffsetX.Value + Plugin.QuickBarOffsetX.Value, top,
                    hotbarBottom - Plugin.StackGap.Value + Plugin.QuickBarOffsetY.Value, Hotbar.BarScale);
            }
            else
            {
                Theme.Place(_root, Plugin.QuickBarAnchor.Value, Plugin.QuickBarOffsetX.Value,
                    Plugin.QuickBarOffsetY.Value, Hotbar.BarScale);
            }
            bool show = !player.IsDead();
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            if (!show) return;

            string modifier = KeyLabel(Plugin.QuickBarModifier.Value);
            if (_modifierLabel.text != modifier) _modifierLabel.text = modifier;

            Inventory inventory = player.GetInventory();
            for (int x = 0; x < Hotbar.SlotCount; x++) Slots[x].Fill(inventory.GetItemAt(x, Row), player);

            if (_builtWithFood)
            {
                Inventory pouch = FoodPouch.Get();
                for (int i = 0; i < FoodPouch.Size; i++)
                {
                    FoodSlots[i].SetBinding(FoodPouch.KeyLabel(i));
                    FoodSlots[i].Fill(pouch?.GetItemAt(i, 0), player);
                }
            }
        }

        /// <summary>Called after Player.Update for the local player.</summary>
        public static void HandleInput(Player player)
        {
            if (!ModifierHeld || !CanTakeInput(player)) return;
            for (int i = 0; i < Hotbar.SlotCount; i++)
            {
                if (!ZInput.GetKeyDown(KeyCode.Alpha1 + i, false) && !ZInput.GetKeyDown(KeyCode.Keypad1 + i, false)) continue;
                ItemDrop.ItemData item = player.GetInventory().GetItemAt(i, Row);
                if (item != null) player.UseItem(null, item, false);
            }
        }

        private static void Build(HotkeyBar bar)
        {
            Remove();
            _version = Theme.Version;
            _builtWithFood = Plugin.FoodSlotsEnabled.Value;
            Hotbar.SlotGeometry(bar, out Vector2 size, out Vector2 pivot, out float span);
            _root = Theme.NewRect("RuneUI_QuickBar", bar.transform.parent);
            _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, span);
            _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            // The root's pivot follows its anchor; this is where slot 0 sits so all eight fill the root.
            Vector2 anchor = Theme.AnchorPoint(Hotbar.Stacking ? Plugin.HotbarAnchor.Value : Plugin.QuickBarAnchor.Value);
            var origin = new Vector2(pivot.x * size.x - anchor.x * span, pivot.y * size.y - anchor.y * size.y);

            for (int x = 0; x < Hotbar.SlotCount; x++)
            {
                Slots[x] = new HotbarSlot(bar, _root, "QuickSlot" + x);
                Slots[x].PlaceAt(bar, x, origin);
                Slots[x].SetBinding((x + 1).ToString());
            }

            if (_builtWithFood)
            {
                var foodOrigin = origin + new Vector2(ExtensionGap, 0f);
                for (int i = 0; i < FoodPouch.Size; i++)
                {
                    FoodSlots[i] = new HotbarSlot(bar, _root, "FoodSlot" + i);
                    FoodSlots[i].PlaceAt(bar, Hotbar.SlotCount + i, foodOrigin);
                }
            }

            _modifierLabel = Theme.NewText("Modifier", _root, 18f, TextAlignmentOptions.Right);
            _modifierLabel.color = Plugin.AccentColor.Value;
            _modifierLabel.fontStyle = FontStyles.Bold;
            var labelRt = (RectTransform)_modifierLabel.transform;
            // Centre it on the first slot as drawn, left of it.
            Rect first = Theme.Bounds((RectTransform)Slots[0].Go.transform, _root);
            labelRt.anchorMin = labelRt.anchorMax = Vector2.zero;
            labelRt.pivot = new Vector2(1f, 0.5f);
            labelRt.anchoredPosition = new Vector2(first.xMin - _root.rect.xMin - 8f, first.center.y - _root.rect.yMin);
            labelRt.sizeDelta = new Vector2(70f, first.height);
        }

        private static string KeyLabel(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                    return "Alt";
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    return "Ctrl";
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                    return "Shift";
                default:
                    return key.ToString();
            }
        }

        public static void Remove()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            ResetState();
        }

        public static void ResetState()
        {
            _root = null;
            _modifierLabel = null;
            Array.Clear(Slots, 0, Slots.Length);
            Array.Clear(FoodSlots, 0, FoodSlots.Length);
        }
    }
}
