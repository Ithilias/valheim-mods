using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RuneUI
{
    /// <summary>
    /// Moves the vanilla hotbar and draws its slots in the theme. Remembers the vanilla placement
    /// and slot images so turning either setting off restores exactly those.
    /// </summary>
    internal static class Hotbar
    {
        public const int SlotCount = 8;
        private const string SlotName = "RuneUI_Slot";

        private static HotkeyBar _bar;
        private static bool _moved;
        private static Vector2 _origAnchorMin, _origAnchorMax, _origPos;
        private static Vector3 _origScale;
        private static int _styleVersion = -1;
        private static readonly List<Image> StyledImages = new List<Image>();
        private static readonly HotbarSlot[] EmptySlots = new HotbarSlot[SlotCount];
        private static readonly bool[] Taken = new bool[SlotCount];
        private static int _emptyVersion = -1;

        public static HotkeyBar Find(Hud hud)
        {
            if (_bar == null) _bar = hud.GetComponentInChildren<HotkeyBar>(true);
            return _bar;
        }

        /// <summary>The bar's scale before this mod touched it.</summary>
        public static float BaseScale => _moved ? _origScale.x : _bar != null ? _bar.transform.localScale.x : 1f;

        /// <summary>Base scale for the hotbar and quick bar, with their own size setting applied.</summary>
        public static float BarScale => BaseScale * Plugin.HotbarScale.Value;

        /// <summary>Size and pivot of one slot, from the prefab every slot is cloned from.</summary>
        public static void SlotGeometry(HotkeyBar bar, out Vector2 size, out Vector2 pivot, out float span)
        {
            if (!ReferenceEquals(bar, _measuredBar)) Measure(bar);
            size = _slotSize;
            pivot = _slotPivot;
            span = (SlotCount - 1) * bar.m_elementSpace + size.x;
        }

        private static HotkeyBar _measuredBar;
        private static Vector2 _slotSize = new Vector2(64f, 64f);
        private static Vector2 _slotPivot = new Vector2(0.5f, 0.5f);

        /// <summary>
        /// The prefab's own rect is not what a slot gets once it sits in the hotbar (its size depends
        /// on the parent), so measure a real one: a throwaway clone under the hotbar.
        /// </summary>
        private static void Measure(HotkeyBar bar)
        {
            _measuredBar = bar;
            var go = Object.Instantiate(bar.m_elementPrefab, bar.transform);
            try
            {
                var rt = (RectTransform)go.transform;
                Vector3 scale = rt.localScale;
                _slotSize = new Vector2(rt.rect.width * scale.x, rt.rect.height * scale.y);
                _slotPivot = rt.pivot;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
            if (_slotSize.x < 1f || _slotSize.y < 1f) _slotSize = new Vector2(bar.m_elementSpace, bar.m_elementSpace);
            Plugin.Log.LogInfo($"Hotbar slot size {_slotSize}, pivot {_slotPivot}, spacing {bar.m_elementSpace}.");
        }

        public static void Update(Hud hud, Player player)
        {
            var bar = Find(hud);
            if (bar == null) return;

            if (Plugin.MoveHotbar.Value) Move(bar);
            else Restore();

            if (Plugin.StyleHotbar.Value)
            {
                if (_styleVersion != Theme.Version) Unstyle();
                _styleVersion = Theme.Version;
                // Vanilla rebuilds every slot when the number of bound slots changes, so check each frame.
                foreach (Transform slot in bar.transform) StyleSlot(slot, StyledImages);
            }
            else
            {
                Unstyle();
            }

            if (player != null) UpdateRings(bar, player);
            if (Plugin.ShowEmptySlots.Value) UpdateEmptySlots(bar, player);
            else RemoveEmptySlots();
            PowerSlot.Update(hud, bar, player);
        }

        /// <summary>
        /// Vanilla only builds slots up to the last bound item, and none at all after a death empties
        /// the inventory. Fill every slot vanilla leaves out with an empty one, so all eight always show.
        /// </summary>
        private static void UpdateEmptySlots(HotkeyBar bar, Player player)
        {
            if (EmptySlots[0] == null || EmptySlots[0].Go == null || _emptyVersion != Theme.Version)
            {
                RemoveEmptySlots();
                _emptyVersion = Theme.Version;
                for (int x = 0; x < SlotCount; x++)
                {
                    EmptySlots[x] = new HotbarSlot(bar, bar.transform, "RuneUI_EmptySlot" + x);
                    EmptySlots[x].PlaceAt(bar, x, Vector2.zero);
                    EmptySlots[x].Clear();
                    EmptySlots[x].Go.transform.SetAsFirstSibling();
                }
            }

            System.Array.Clear(Taken, 0, Taken.Length);
            foreach (Transform slot in bar.transform)
            {
                if (slot.name.StartsWith("RuneUI_")) continue;
                int x = Mathf.RoundToInt(slot.localPosition.x / bar.m_elementSpace);
                if (x >= 0 && x < SlotCount) Taken[x] = true;
            }
            bool alive = player != null && !player.IsDead();
            // Vanilla leaves the numbers off when a gamepad is used.
            bool numbers = !ZInput.IsGamepadActive();
            for (int x = 0; x < SlotCount; x++)
            {
                HotbarSlot.SetActive(EmptySlots[x].Go, alive && !Taken[x]);
                EmptySlots[x].SetBinding(numbers ? (x + 1).ToString() : "");
            }
        }

        private static void RemoveEmptySlots()
        {
            for (int x = 0; x < SlotCount; x++)
            {
                if (EmptySlots[x] != null && EmptySlots[x].Go != null) Object.Destroy(EmptySlots[x].Go);
                EmptySlots[x] = null;
            }
            _emptyVersion = -1;
        }

        /// <summary>Vanilla places hotbar slot x at x times m_elementSpace, which tells each slot's item.</summary>
        private static void UpdateRings(HotkeyBar bar, Player player)
        {
            Inventory inventory = player.GetInventory();
            foreach (Transform slot in bar.transform)
            {
                if (slot.name.StartsWith("RuneUI_")) continue;
                int x = Mathf.RoundToInt(slot.localPosition.x / bar.m_elementSpace);
                QualityRing.Update(slot, inventory.GetItemAt(x, 0), true);
            }
        }

        /// <summary>The hotbar is the base of the stack and always uses its own settings.</summary>
        public static void HotbarPlacement(out HudAnchor anchor, out float x, out float y)
        {
            anchor = Plugin.HotbarAnchor.Value;
            x = Plugin.HotbarOffsetX.Value;
            y = Plugin.HotbarOffsetY.Value;
        }

        /// <summary>Whether the quick bar and bars should be stacked on the hotbar this frame.</summary>
        public static bool Stacking => Plugin.StackBars.Value && Plugin.MoveHotbar.Value && _bar != null;

        /// <summary>
        /// Bottom and top of the hotbar's slots as drawn, in <paramref name="space"/>. Measured from the
        /// slots themselves (and the forsaken power slot) so stacking never depends on pivot maths.
        /// </summary>
        public static bool TryEdges(Transform space, out float bottom, out float top)
        {
            bottom = float.MaxValue;
            top = float.MinValue;
            if (_bar == null) return false;
            foreach (Transform child in _bar.transform)
            {
                if (!child.gameObject.activeInHierarchy || !(child is RectTransform rt)) continue;
                Rect r = Theme.Bounds(rt, space);
                bottom = Mathf.Min(bottom, r.yMin);
                top = Mathf.Max(top, r.yMax);
            }
            return top > bottom;
        }

        private static void Move(HotkeyBar bar)
        {
            var rt = (RectTransform)bar.transform;
            if (!_moved)
            {
                _origAnchorMin = rt.anchorMin;
                _origAnchorMax = rt.anchorMax;
                _origPos = rt.anchoredPosition;
                _origScale = rt.localScale;
                _moved = true;
            }

            // Vanilla lays slots out from the bar's pivot at multiples of m_elementSpace. Place the
            // bar so the box around all eight slots sits at the configured anchor point.
            SlotGeometry(bar, out Vector2 size, out Vector2 pivot, out float span);
            HotbarPlacement(out HudAnchor hudAnchor, out float x, out float y);
            Vector2 anchor = Theme.AnchorPoint(hudAnchor);
            float scale = Plugin.HudScale.Value * Plugin.HotbarScale.Value * _origScale.x;
            var boxPivot = new Vector2(anchor.x * span - pivot.x * size.x, anchor.y * size.y - pivot.y * size.y);
            var pos = new Vector2(x, y) - boxPivot * scale;

            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            if ((rt.anchoredPosition - pos).sqrMagnitude > 0.01f) rt.anchoredPosition = pos;
            if (Mathf.Abs(rt.localScale.x - scale) > 0.001f) rt.localScale = new Vector3(scale, scale, 1f);
        }

        private static void Restore()
        {
            if (!_moved) return;
            _moved = false;
            if (_bar == null) return;
            var rt = (RectTransform)_bar.transform;
            rt.anchorMin = _origAnchorMin;
            rt.anchorMax = _origAnchorMax;
            rt.anchoredPosition = _origPos;
            rt.localScale = _origScale;
        }

        /// <summary>
        /// Puts a themed panel behind a hotbar slot's contents and hides the slot's own background
        /// image. <paramref name="hidden"/> collects the hidden images so they can be shown again.
        /// </summary>
        public static void StyleSlot(Transform slot, List<Image> hidden)
        {
            if (slot.Find(SlotName) != null) return;
            var panel = Theme.NewPanel(SlotName, slot);
            Theme.Stretch(panel.rectTransform);
            panel.transform.SetAsFirstSibling();
            var own = slot.GetComponent<Image>();
            if (own != null && own.enabled)
            {
                own.enabled = false;
                hidden?.Add(own);
            }
        }

        private static void Unstyle()
        {
            foreach (var img in StyledImages)
                if (img != null) img.enabled = true;
            StyledImages.Clear();
            if (_bar == null) return;
            foreach (Transform slot in _bar.transform)
            {
                var panel = slot.Find(SlotName);
                if (panel == null) continue;
                // Destroy waits for the end of the frame; rename so a restyle this frame adds a new one.
                panel.name = "RuneUI_Removed";
                Object.Destroy(panel.gameObject);
            }
        }

        public static void Remove()
        {
            Restore();
            Unstyle();
            RemoveEmptySlots();
            PowerSlot.Remove();
            if (_bar != null)
                foreach (Transform slot in _bar.transform) QualityRing.Remove(slot);
            _styleVersion = -1;
        }

        public static void ResetState()
        {
            PowerSlot.ResetState();
            _bar = null;
            _measuredBar = null;
            _moved = false;
            _styleVersion = -1;
            StyledImages.Clear();
            System.Array.Clear(EmptySlots, 0, EmptySlots.Length);
            _emptyVersion = -1;
        }
    }
}
