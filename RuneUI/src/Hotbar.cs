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

        public static HotkeyBar Find(Hud hud)
        {
            if (_bar == null) _bar = hud.GetComponentInChildren<HotkeyBar>(true);
            return _bar;
        }

        /// <summary>The bar's scale before this mod touched it.</summary>
        public static float BaseScale => _moved ? _origScale.x : _bar != null ? _bar.transform.localScale.x : 1f;

        /// <summary>Size and pivot of one slot, from the prefab every slot is cloned from.</summary>
        public static void SlotGeometry(HotkeyBar bar, out Vector2 size, out Vector2 pivot, out float span)
        {
            var rt = bar.m_elementPrefab.transform as RectTransform;
            size = rt != null ? rt.rect.size : new Vector2(64f, 64f);
            pivot = rt != null ? rt.pivot : new Vector2(0.5f, 0.5f);
            span = (SlotCount - 1) * bar.m_elementSpace + size.x;
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
            Vector2 anchor = Theme.AnchorPoint(Plugin.HotbarAnchor.Value);
            float scale = Plugin.HudScale.Value * _origScale.x;
            var boxPivot = new Vector2(anchor.x * span - pivot.x * size.x, anchor.y * size.y - pivot.y * size.y);
            var pos = new Vector2(Plugin.HotbarOffsetX.Value, Plugin.HotbarOffsetY.Value) - boxPivot * scale;

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
            _styleVersion = -1;
        }

        public static void ResetState()
        {
            _bar = null;
            _moved = false;
            _styleVersion = -1;
            StyledImages.Clear();
        }
    }
}
