using UnityEngine;
using UnityEngine.UI;

namespace RoundMinimap
{
    /// <summary>
    /// Optional box behind the small map's biome name, so it stays readable over bright ground.
    ///
    /// The box is a sibling placed directly behind the label rather than a child of it, since a
    /// child would draw on top of the text. It holds an optional soft drop shadow, the fill and the
    /// border, in that order. It follows the rendered text rather than the label's
    /// rect, which is much wider than the words, so it hugs whatever biome name is showing. It also
    /// follows the label's scale and opacity, so vanilla's pulse when you enter a new biome carries
    /// the box along with it.
    /// </summary>
    internal static class BiomeLabelStyle
    {
        private const string PanelName = "RoundMinimapBiomeBackground";

        private static RectTransform _panel;
        private static RectTransform _shadowRect;
        private static Image _shadow;
        private static Image _fill;
        private static Image _ring;
        private static string _appliedShape;

        /// <summary>The box, so the backing-panel cleanup knows to leave it alone.</summary>
        public static Transform Panel => _panel;

        public static void Apply(Minimap map)
        {
            var label = map.m_biomeNameSmall;
            Color fillColour = Plugin.BiomeBackgroundColor.Value;
            Color ringColour = Plugin.BiomeBorderColor.Value;
            if (Plugin.BiomeBorderFollowsMap.Value)
            {
                Color mapBorder = MinimapShape.BorderColour(map);
                ringColour = new Color(mapBorder.r, mapBorder.g, mapBorder.b, ringColour.a);
            }
            Color shadowColour = Plugin.BiomeShadowColor.Value;
            float thickness = Mathf.Max(0f, Plugin.BiomeBorderThickness.Value);
            float shadowSize = Mathf.Max(0f, Plugin.BiomeShadowSize.Value);
            bool drawRing = thickness > 0f && ringColour.a > 0.001f;
            bool drawShadow = shadowSize > 0f && shadowColour.a > 0.001f;

            if (label == null || (fillColour.a <= 0.001f && !drawRing && !drawShadow))
            {
                Remove();
                return;
            }

            var labelRect = label.rectTransform;
            var parent = labelRect.parent as RectTransform;
            if (parent == null)
            {
                Remove();
                return;
            }

            if (_panel == null || _panel.parent != parent)
            {
                Remove();
                Build(parent);
            }

            float radius = Mathf.Max(0f, Plugin.BiomeCornerRadius.Value);
            float drawnThickness = drawRing ? thickness : 0f;
            float drawnShadow = drawShadow ? shadowSize : 0f;
            string shape = radius.ToString("F1") + "|" + drawnThickness.ToString("F1") + "|" +
                           drawnShadow.ToString("F1");
            if (shape != _appliedShape)
            {
                ReplaceSprites(radius, drawnThickness, drawnShadow);
                _appliedShape = shape;
            }

            // Directly behind the label: after it, the box would cover the text, and further back
            // something else, such as the map when the label is moved behind it, could come between.
            int labelIndex = labelRect.GetSiblingIndex();
            int panelIndex = _panel.GetSiblingIndex();
            if (panelIndex > labelIndex) _panel.SetSiblingIndex(labelIndex);
            else if (panelIndex < labelIndex - 1) _panel.SetSiblingIndex(labelIndex - 1);

            Bounds text = label.textBounds;
            float opacity = label.color.a * label.canvasRenderer.GetAlpha();
            bool visible = label.isActiveAndEnabled && !string.IsNullOrEmpty(label.text)
                           && text.size.x > 0.01f && opacity > 0.01f;
            if (_panel.gameObject.activeSelf != visible) _panel.gameObject.SetActive(visible);
            if (!visible) return;

            var padding = new Vector2(Plugin.BiomePaddingX.Value, Plugin.BiomePaddingY.Value);
            Vector2 local = parent.InverseTransformPoint(labelRect.TransformPoint(text.center));
            Vector2 position = local - parent.rect.center;
            Vector2 size = Vector2.Scale(text.size, labelRect.localScale) + padding * 2f;
            if (_panel.anchoredPosition != position) _panel.anchoredPosition = position;
            if (_panel.sizeDelta != size) _panel.sizeDelta = size;

            _fill.color = new Color(fillColour.r, fillColour.g, fillColour.b, fillColour.a * opacity);
            _ring.enabled = drawRing;
            _ring.color = new Color(ringColour.r, ringColour.g, ringColour.b, ringColour.a * opacity);
            _shadow.enabled = drawShadow;
            _shadow.color = new Color(shadowColour.r, shadowColour.g, shadowColour.b, shadowColour.a * opacity);
            var shadowOffset = new Vector2(0f, -Plugin.BiomeShadowOffset.Value);
            if (_shadowRect.anchoredPosition != shadowOffset) _shadowRect.anchoredPosition = shadowOffset;
            var shadowGrow = new Vector2(drawnShadow * 2f, drawnShadow * 2f);
            if (_shadowRect.sizeDelta != shadowGrow) _shadowRect.sizeDelta = shadowGrow;

            // One texture pixel per UI unit, so the corner radius and border are in the same pixels
            // as the padding.
            var canvas = label.canvas;
            float perUnit = canvas != null ? canvas.referencePixelsPerUnit / 100f : 1f;
            _fill.pixelsPerUnitMultiplier = perUnit;
            _ring.pixelsPerUnitMultiplier = perUnit;
            _shadow.pixelsPerUnitMultiplier = perUnit;
        }

        public static void Remove()
        {
            DestroySprite(_shadow);
            DestroySprite(_fill);
            DestroySprite(_ring);
            if (_panel != null) Object.Destroy(_panel.gameObject);
            DropReferences();
        }

        /// <summary>Drops references after the minimap HUD has been destroyed and rebuilt.</summary>
        public static void ResetState() => DropReferences();

        private static void DropReferences()
        {
            _panel = null;
            _shadowRect = null;
            _shadow = null;
            _fill = null;
            _ring = null;
            _appliedShape = null;
        }

        private static void Build(RectTransform parent)
        {
            var go = new GameObject(PanelName, typeof(RectTransform));
            _panel = (RectTransform)go.transform;
            _panel.SetParent(parent, false);
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.localScale = Vector3.one;

            // Children draw in order: shadow, then fill, then border on top.
            _shadow = AddLayer("Shadow");
            _shadowRect = _shadow.rectTransform;
            _fill = AddLayer("Fill");
            _ring = AddLayer("Border");
        }

        /// <summary>A sliced image stretched over the box.</summary>
        private static Image AddLayer(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_panel, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.type = Image.Type.Sliced;
            return image;
        }

        private static void ReplaceSprites(float radius, float thickness, float shadow)
        {
            DestroySprite(_shadow);
            DestroySprite(_fill);
            DestroySprite(_ring);
            _shadow.sprite = shadow > 0f ? UiTools.CreateRoundedShadowSprite(radius, shadow) : null;
            _fill.sprite = UiTools.CreateRoundedRectSprite(radius, thickness, ring: false);
            _ring.sprite = UiTools.CreateRoundedRectSprite(radius, thickness, ring: true);
        }

        private static void DestroySprite(Image image)
        {
            if (image == null || image.sprite == null) return;
            if (image.sprite.texture != null) Object.Destroy(image.sprite.texture);
            Object.Destroy(image.sprite);
            image.sprite = null;
        }
    }
}
