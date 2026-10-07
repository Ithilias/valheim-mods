using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RuneUI
{
    internal enum HudAnchor
    {
        TopLeft,
        Top,
        TopRight,
        Left,
        Center,
        Right,
        BottomLeft,
        Bottom,
        BottomRight,
    }

    /// <summary>
    /// Shared look of everything this mod draws: generated sprites, font and the helpers that build
    /// themed objects. <see cref="Version"/> changes whenever a setting does, so features rebuild.
    /// </summary>
    internal static class Theme
    {
        public static int Version { get; private set; }

        private static Sprite _fill;
        private static Sprite _border;
        private static Sprite _ring;
        private static float _spriteRadius = -1f;
        private static float _spriteBorder = -1f;
        private static float _spriteRing = -1f;

        private static TMP_FontAsset _font;
        private static string _fontName;
        private static TMP_FontAsset _fallbackFont;

        public static void Invalidate() => Version++;

        /// <summary>Font of the vanilla HUD, used when no font is configured or it is not found.</summary>
        public static void SetFallbackFont(TMP_FontAsset font)
        {
            if (font != null) _fallbackFont = font;
        }

        public static TMP_FontAsset Font
        {
            get
            {
                // Inventory grids can build text before the HUD's first update hands over its font.
                if (_fallbackFont == null && Hud.instance != null && Hud.instance.m_healthText != null)
                    _fallbackFont = Hud.instance.m_healthText.font;
                string name = Plugin.FontName.Value.Trim();
                if (name.Length == 0) return _fallbackFont;
                if (_font == null || _fontName != name)
                {
                    _fontName = name;
                    _font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f.name == name);
                    if (_font == null) Plugin.Log.LogWarning($"Font '{name}' not found, using the vanilla font.");
                }
                return _font != null ? _font : _fallbackFont;
            }
        }

        public static Sprite FillSprite
        {
            get { EnsureSprites(); return _fill; }
        }

        public static Sprite BorderSprite
        {
            get { EnsureSprites(); return _border; }
        }

        public static Sprite RingSprite
        {
            get { EnsureSprites(); return _ring; }
        }

        private static void EnsureSprites()
        {
            float radius = Plugin.CornerRadius.Value;
            float border = Plugin.BorderWidth.Value;
            float ring = Plugin.QualityRingWidth.Value;
            if (_fill != null && Mathf.Approximately(radius, _spriteRadius) && Mathf.Approximately(border, _spriteBorder)
                && Mathf.Approximately(ring, _spriteRing))
                return;
            DestroySprites();
            _spriteRadius = radius;
            _spriteBorder = border;
            _spriteRing = ring;
            _ring = UiTools.CreateRoundedRectSprite(radius, ring, true);
            // The fill reaches under the border so a thin or missing border leaves no gap.
            _fill = UiTools.CreateRoundedRectSprite(radius, 0f, false);
            _border = UiTools.CreateRoundedRectSprite(radius, Mathf.Max(border, 0.01f), true);
        }

        public static void DestroySprites()
        {
            DestroySprite(ref _fill);
            DestroySprite(ref _border);
            DestroySprite(ref _ring);
        }

        private static void DestroySprite(ref Sprite sprite)
        {
            if (sprite == null) return;
            Object.Destroy(sprite.texture);
            Object.Destroy(sprite);
            sprite = null;
        }

        public static Vector2 AnchorPoint(HudAnchor anchor)
        {
            int i = (int)anchor;
            return new Vector2(i % 3 * 0.5f, 1f - i / 3 * 0.5f);
        }

        /// <summary>Anchors and pivots <paramref name="rt"/> at the same screen point, then offsets it.</summary>
        public static void Place(RectTransform rt, HudAnchor anchor, float x, float y, float baseScale = 1f)
        {
            Vector2 point = AnchorPoint(anchor);
            rt.anchorMin = point;
            rt.anchorMax = point;
            rt.pivot = point;
            var pos = new Vector2(x, y);
            if ((rt.anchoredPosition - pos).sqrMagnitude > 0.01f) rt.anchoredPosition = pos;
            float scale = Plugin.HudScale.Value * baseScale;
            if (Mathf.Abs(rt.localScale.x - scale) > 0.001f) rt.localScale = new Vector3(scale, scale, 1f);
        }

        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary><paramref name="target"/>'s rect as drawn, in <paramref name="space"/>'s local units.</summary>
        public static Rect Bounds(RectTransform target, Transform space)
        {
            target.GetWorldCorners(Corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in Corners)
            {
                Vector2 p = space.InverseTransformPoint(corner);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>
        /// Places a stacked block: on the hotbar's anchor, moved so its measured edge lands where wanted.
        /// <paramref name="measured"/> and <paramref name="wanted"/> are in the block's parent space.
        /// </summary>
        public static void PlaceStacked(RectTransform rt, float x, float measured, float wanted, float baseScale = 1f)
        {
            Place(rt, Plugin.HotbarAnchor.Value, x, rt.anchoredPosition.y, baseScale);
            float delta = wanted - measured;
            if (Mathf.Abs(delta) > 0.5f) rt.anchoredPosition += new Vector2(0f, delta);
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            SetSliced(img, sprite, color);
            return img;
        }

        public static void SetSliced(Image img, Sprite sprite, Color color)
        {
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            // Map one sprite pixel to one UI unit, whatever the canvas reference resolution.
            var canvas = img.canvas;
            img.pixelsPerUnitMultiplier = canvas != null ? canvas.referencePixelsPerUnit / 100f : 1f;
        }

        /// <summary>A rounded panel: fill on the object itself and a border child on top.</summary>
        public static Image NewPanel(string name, Transform parent)
        {
            var fill = NewImage(name, parent, FillSprite, Plugin.PanelColor.Value);
            AddBorder(fill.rectTransform);
            return fill;
        }

        public static Image AddBorder(RectTransform target)
        {
            var border = NewImage("RuneUI_Border", target, BorderSprite, Plugin.BorderColor.Value);
            Stretch(border.rectTransform);
            border.enabled = Plugin.BorderWidth.Value > 0f;
            return border;
        }

        public static TextMeshProUGUI NewText(string name, Transform parent, float size, TextAlignmentOptions align)
        {
            var rt = NewRect(name, parent);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = Font;
            if (font != null) text.font = font;
            text.fontSize = size * Plugin.FontScale.Value;
            text.color = Plugin.TextColor.Value;
            text.alignment = align;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }
    }
}
