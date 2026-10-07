using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoundMinimap
{
    /// <summary>
    /// Makes the small minimap circular via a <see cref="CircleMeshEffect"/> on the map image, then
    /// tidies up the surrounding HUD: the square backing panel that now sticks out past the circle,
    /// the biome label, and the wind marker.
    ///
    /// Nothing is reparented and no material is replaced, so Valheim's own per-frame writes to the
    /// minimap material keep working and every child marker stays where vanilla put it.
    /// </summary>
    internal static class MinimapShape
    {
        private const string BorderName = "RoundMinimapBorder";
        private const int TextureSize = 512;
        private const string VanillaFrameSprite = "InputFieldBackground";

        private static CircleMeshEffect _effect;
        private static GameObject _border;
        private static float _appliedSizeMultiplier;
        private static bool _sizeCaptured;
        private static Vector2 _originalMapSize;
        private static float _appliedCircleScale;
        private static float _appliedBorderThickness;
        private static float _appliedBorderShadow;
        private static string _appliedLayout;

        private static readonly List<Graphic> _disabledGraphics = new List<Graphic>();
        private static bool _windMarkerSaved;
        private static RectPlacement _windMarkerOriginal;
        private static bool _biomeLabelSaved;
        private static RectPlacement _biomeLabelOriginal;
        private static TextAlignmentOptions _biomeAlignmentOriginal;

        private static RectTransform _root;
        private static bool _rootCaptured;
        private static Vector2 _rootOriginalPosition;
        private static Vector2 _growDirection;
        private static string _appliedPlacement;

        public static bool IsApplied => _effect != null;

        /// <summary>Circle radius in the map image's own units, or 0 when not applied.</summary>
        public static float Radius { get; private set; }

        private struct RectPlacement
        {
            public Vector2 AnchorMin, AnchorMax, Pivot, AnchoredPosition;

            public static RectPlacement Capture(RectTransform rt) => new RectPlacement
            {
                AnchorMin = rt.anchorMin,
                AnchorMax = rt.anchorMax,
                Pivot = rt.pivot,
                AnchoredPosition = rt.anchoredPosition,
            };

            public void Restore(RectTransform rt)
            {
                rt.anchorMin = AnchorMin;
                rt.anchorMax = AnchorMax;
                rt.pivot = Pivot;
                rt.anchoredPosition = AnchoredPosition;
            }
        }

        public static void Apply(Minimap map)
        {
            var image = map.m_mapImageSmall;
            if (image == null) return;
            var mapRect = image.rectTransform;

            float circleScale = Mathf.Clamp(Plugin.CircleScale.Value, 0.25f, 1f);
            float thickness = Mathf.Max(0f, Plugin.BorderThickness.Value);
            float shadow = Mathf.Max(0f, Plugin.BorderShadow.Value);
            float side = Mathf.Min(mapRect.rect.width, mapRect.rect.height);
            if (side <= 1f)
            {
                Plugin.Log.LogWarning($"Small map image has no usable size ({mapRect.rect.size}); skipping round minimap.");
                return;
            }

            Radius = side * 0.5f * circleScale;

            bool freshEffect = _effect == null;
            if (freshEffect)
            {
                _effect = image.gameObject.GetComponent<CircleMeshEffect>()
                          ?? image.gameObject.AddComponent<CircleMeshEffect>();
                Plugin.Log.LogInfo($"Round minimap applied (diameter {Radius * 2f:F0}px).");
            }
            _effect.RadiusScale = circleScale;
            _effect.Segments = Plugin.CircleSegments.Value;

            bool wantBorder = thickness > 0f || shadow > 0f;
            bool borderChanged = !Mathf.Approximately(_appliedCircleScale, circleScale)
                                 || !Mathf.Approximately(_appliedBorderThickness, thickness)
                                 || !Mathf.Approximately(_appliedBorderShadow, shadow)
                                 || (wantBorder && _border == null);
            if (borderChanged)
            {
                RemoveBorder();
                if (thickness > 0f || shadow > 0f) BuildBorder(mapRect, Radius, thickness, shadow);
                _appliedBorderThickness = thickness;
                _appliedBorderShadow = shadow;
            }
            if (_border != null)
            {
                var borderImage = _border.GetComponent<Image>();
                if (borderImage != null) borderImage.color = BorderColour(map);
            }

            ApplyLayout(map, mapRect, freshEffect || circleScale != _appliedCircleScale);
            _appliedCircleScale = circleScale;
        }

        /// <summary>
        /// Size and screen position of the whole small map. The offset applies to the square map as
        /// well, since it moves the panel and everything on it together. The size multiplier only
        /// applies to the round one: it resizes the map image alone, and on the square map the
        /// vanilla frame, biome label and wind marker would stay behind at their original size.
        /// </summary>
        public static void ApplyPlacement(Minimap map, bool round)
        {
            var image = map.m_mapImageSmall;
            if (image == null) return;
            var mapRect = image.rectTransform;

            ApplySize(map, mapRect, round ? Plugin.MapSizeMultiplier.Value : 1f);
            ApplyPosition(map, mapRect);
        }

        /// <summary>Puts the small map back to its vanilla size and position.</summary>
        public static void RestorePlacement(Minimap map)
        {
            if (_appliedPlacement != null && _root != null) _root.anchoredPosition = _rootOriginalPosition;
            _appliedPlacement = null;

            if (_sizeCaptured && !Mathf.Approximately(_appliedSizeMultiplier, 1f)
                && map.m_mapImageSmall != null)
            {
                Resize(map.m_mapImageSmall.rectTransform, _originalMapSize);
                Resize(map.m_pinRootSmall, _originalMapSize);
                Resize(map.m_pinNameRootSmall, _originalMapSize);
                _appliedSizeMultiplier = 1f;
            }
        }

        /// <summary>
        /// Scales the small map up or down. The pin roots have to be resized identically: pin
        /// positions are computed against the map image's rect but applied inside the pin roots, so
        /// if the two rects disagree every pin lands at the wrong offset.
        /// </summary>
        private static void ApplySize(Minimap map, RectTransform mapRect, float wantedMultiplier)
        {
            float multiplier = Mathf.Clamp(wantedMultiplier, 0.5f, 3f);

            if (!_sizeCaptured)
            {
                _originalMapSize = mapRect.rect.size;
                _sizeCaptured = true;
                _appliedSizeMultiplier = 1f;
            }

            if (Mathf.Approximately(_appliedSizeMultiplier, multiplier)) return;
            if (_originalMapSize.x <= 1f || _originalMapSize.y <= 1f) return;

            Vector2 wanted = _originalMapSize * multiplier;

            Resize(mapRect, wanted);
            Resize(map.m_pinRootSmall, wanted);
            Resize(map.m_pinNameRootSmall, wanted);

            _appliedSizeMultiplier = multiplier;

            // Everything downstream is measured from the rect, so force the border and the layout
            // (biome label, wind marker) to be rebuilt against the new size.
            RemoveBorder();
            _appliedBorderThickness = -1f;
            _appliedLayout = null;

            Plugin.Log.LogInfo($"Minimap size multiplier {multiplier:F2} ({wanted.x:F0}x{wanted.y:F0}px).");
        }

        /// <summary>
        /// Moves the whole small map by shifting its root panel, which vanilla never positions
        /// itself (it only switches it on and off). Everything on the map moves with it, so the
        /// markers, pins and compass cannot drift apart.
        ///
        /// A bigger map grows equally in every direction, which in a screen corner pushes it off the
        /// edge. Growing towards the screen centre instead keeps the corner nearest the edge where
        /// vanilla had it. Which corner that is gets measured rather than assumed, so a HUD mod that
        /// moves the minimap elsewhere is handled too.
        /// </summary>
        private static void ApplyPosition(Minimap map, RectTransform mapRect)
        {
            var root = map.m_smallRoot != null ? map.m_smallRoot.transform as RectTransform : null;
            if (root == null || !_sizeCaptured) return;
            if (_originalMapSize.x <= 1f || _originalMapSize.y <= 1f) return;

            if (!_rootCaptured)
            {
                _root = root;
                _rootOriginalPosition = root.anchoredPosition;
                _growDirection = TowardsScreenCentre(mapRect);
                _rootCaptured = true;
            }

            bool grow = Plugin.GrowTowardsCentre.Value;
            var offset = new Vector2(Plugin.MapOffsetX.Value, Plugin.MapOffsetY.Value);
            string signature = string.Join("|", _appliedSizeMultiplier.ToString("F3"), grow.ToString(),
                offset.x.ToString("F1"), offset.y.ToString("F1"));
            if (signature == _appliedPlacement) return;

            Vector2 shift = Vector2.zero;
            if (grow)
            {
                Vector2 growth = _originalMapSize * (_appliedSizeMultiplier - 1f);
                shift = ToParentSpace(mapRect.parent, root.parent, Vector2.Scale(_growDirection, growth * 0.5f));
            }

            root.anchoredPosition = _rootOriginalPosition + shift + offset;
            _appliedPlacement = signature;

            // The border, biome label and wind marker are placed from the map's position when they
            // are built, so they have to be rebuilt where the map now is.
            RemoveBorder();
            _appliedBorderThickness = -1f;
            _appliedLayout = null;
        }

        /// <summary>
        /// Where the visible map is now and where vanilla had it, as world-space centres and half
        /// extents. The vanilla footprint is the original square at the original root position; the
        /// current one is the circle when round, or the map image when square.
        /// </summary>
        public static bool TryGetFootprint(Minimap map, out Vector3 vanillaCentre, out Vector2 vanillaHalf,
            out Vector3 currentCentre, out Vector2 currentHalf)
        {
            vanillaCentre = currentCentre = Vector3.zero;
            vanillaHalf = currentHalf = Vector2.zero;

            var image = map.m_mapImageSmall;
            if (image == null || !_sizeCaptured || !_rootCaptured || _root == null) return false;
            var mapRect = image.rectTransform;

            currentCentre = mapRect.TransformPoint(mapRect.rect.center);
            Vector2 rootShift = _root.anchoredPosition - _rootOriginalPosition;
            vanillaCentre = currentCentre - (_root.parent != null
                ? _root.parent.TransformVector(rootShift)
                : (Vector3)rootShift);

            vanillaHalf = WorldSize(mapRect, _originalMapSize * 0.5f);
            if (IsApplied && Radius > 0f)
            {
                // Compass letters can sit outside the circle, and count as part of the map then.
                float extent = Radius;
                if (MinimapCompass.IsApplied)
                {
                    float letters = Radius * Mathf.Clamp(Plugin.CompassDistance.Value, 0.1f, 1.5f)
                                    + Plugin.CompassFontSize.Value * 0.5f;
                    extent = Mathf.Max(extent, letters);
                }
                currentHalf = WorldSize(mapRect, new Vector2(extent, extent));
            }
            else
            {
                currentHalf = WorldSize(mapRect, mapRect.rect.size * 0.5f);
            }
            return true;
        }

        private static Vector2 WorldSize(Transform space, Vector2 size)
        {
            Vector3 x = space.TransformVector(new Vector3(size.x, 0f, 0f));
            Vector3 y = space.TransformVector(new Vector3(0f, size.y, 0f));
            return new Vector2(Mathf.Abs(x.x), Mathf.Abs(y.y));
        }

        /// <summary>For each axis, +1 or -1 towards the screen centre, or 0 if already roughly central.</summary>
        private static Vector2 TowardsScreenCentre(RectTransform mapRect)
        {
            var canvas = mapRect.GetComponentInParent<Canvas>();
            Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
            Camera camera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;

            Vector2 onScreen = RectTransformUtility.WorldToScreenPoint(camera, mapRect.TransformPoint(mapRect.rect.center));
            return new Vector2(Side(Screen.width * 0.5f - onScreen.x, Screen.width),
                               Side(Screen.height * 0.5f - onScreen.y, Screen.height));
        }

        private static float Side(float towardsCentre, float extent) =>
            Mathf.Abs(towardsCentre) < extent * 0.1f ? 0f : Mathf.Sign(towardsCentre);

        /// <summary>Converts a vector between two transforms' local spaces, through world space.</summary>
        private static Vector2 ToParentSpace(Transform from, Transform to, Vector2 vector)
        {
            Vector3 world = from != null ? from.TransformVector(vector) : (Vector3)vector;
            return to != null ? (Vector2)to.InverseTransformVector(world) : (Vector2)world;
        }

        /// <summary>Resizes about the rect's current centre, whatever its anchors were.</summary>
        private static void Resize(RectTransform rect, Vector2 size)
        {
            if (rect == null) return;
            var parent = rect.parent as RectTransform;
            if (parent == null) return;

            Vector3 worldCentre = rect.TransformPoint(rect.rect.center);

            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;

            Vector2 local = parent.InverseTransformPoint(worldCentre);
            rect.anchoredPosition = local - parent.rect.center;
        }

        public static void Remove(Minimap map)
        {
            RestoreLayout(map);

            if (_effect != null)
            {
                // Disabling first lets the graphic rebuild its normal quad before the component goes.
                _effect.enabled = false;
                Object.Destroy(_effect);
                _effect = null;
            }
            RemoveBorder();

            Radius = 0f;
            _appliedCircleScale = 0f;
            _appliedBorderThickness = -1f;
            _appliedBorderShadow = -1f;
        }

        /// <summary>
        /// Drops every cached reference without touching the objects, for when the minimap HUD has
        /// been destroyed and rebuilt (logging out to the menu and rejoining). Restoring is neither
        /// possible nor needed at that point: the old objects are gone.
        /// </summary>
        public static void ResetState()
        {
            _sizeCaptured = false;
            _appliedSizeMultiplier = 0f;
            _root = null;
            _rootCaptured = false;
            _appliedPlacement = null;
            _effect = null;
            _border = null;
            _disabledGraphics.Clear();
            _windMarkerSaved = false;
            _biomeLabelSaved = false;
            _appliedLayout = null;
            _appliedCircleScale = 0f;
            _appliedBorderThickness = -1f;
            _appliedBorderShadow = -1f;
            Radius = 0f;
        }

        /// <summary>Turns the sampled map content counter-clockwise by <paramref name="degrees"/>.</summary>
        public static void SetAngle(float degrees)
        {
            if (_effect != null) _effect.Angle = degrees;
        }

        // ---- border ----

        private static void BuildBorder(RectTransform mapRect, float radius, float thickness, float shadow)
        {
            var parent = mapRect.parent as RectTransform;
            if (parent == null) return;

            // The sprite has to extend past the circle to hold the shadow.
            float spriteRadius = radius + shadow;
            float texelsPerPixel = TextureSize * 0.5f / spriteRadius;

            _border = new GameObject(BorderName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)_border.transform;
            rect.SetParent(parent, false);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(spriteRadius * 2f, spriteRadius * 2f);
            rect.anchoredPosition = UiTools.CenterOnAnchoredPosition(parent, mapRect);
            rect.localScale = Vector3.one;
            rect.SetSiblingIndex(parent.childCount - 1);

            var borderImage = _border.GetComponent<Image>();
            borderImage.sprite = UiTools.CreateBorderSprite(
                TextureSize,
                radius * texelsPerPixel,
                thickness * texelsPerPixel,
                shadow * texelsPerPixel,
                Mathf.Clamp01(Plugin.BorderShadowStrength.Value));
            borderImage.raycastTarget = false;
        }

        /// <summary>
        /// The configured border colour, or, while its colour is left at the default, the colour of
        /// the vanilla frame the round map replaces, as long as that frame still has its vanilla sprite. Mods that tint that frame, such as Seasons,
        /// then carry over. The alpha always comes from the setting: the frame is a faint backing
        /// panel, and its alpha would make a thin rim almost invisible.
        /// </summary>
        private static Color BorderColour(Minimap map)
        {
            Color configured = Plugin.BorderColor.Value;
            if (!SameRgb(configured, (Color)Plugin.BorderColor.DefaultValue)) return configured;
            if (map.m_smallRoot == null || !map.m_smallRoot.TryGetComponent(out Image frame)) return configured;
            // Only the plain vanilla panel: a UI mod's textured frame is usually left white and
            // would turn the rim white too.
            if (frame.sprite == null || frame.sprite.name != VanillaFrameSprite) return configured;

            Color tint = frame.color;
            return new Color(tint.r, tint.g, tint.b, configured.a);
        }

        // The config file stores colours as bytes, so a default read back from it is not exactly
        // the float default.
        private static bool SameRgb(Color a, Color b)
        {
            Color32 x = a, y = b;
            return x.r == y.r && x.g == y.g && x.b == y.b;
        }

        private static void RemoveBorder()
        {
            if (_border == null) return;
            Object.Destroy(_border);
            _border = null;
        }

        // ---- surrounding HUD layout ----

        private static void ApplyLayout(Minimap map, RectTransform mapRect, bool geometryChanged)
        {
            string signature = string.Join("|",
                Plugin.HideSquareBackground.Value.ToString(),
                Plugin.CenterBiomeLabel.Value.ToString(),
                Plugin.BiomeLabelGap.Value.ToString("F2"),
                Plugin.PullWindMarkerIn.Value.ToString(),
                Plugin.WindMarkerDistance.Value.ToString("F2"));
            if (signature == _appliedLayout && !geometryChanged) return;

            RestoreLayout(map);
            _appliedLayout = signature;

            if (Plugin.HideSquareBackground.Value) HideBackingPanels(map, mapRect);
            if (Plugin.CenterBiomeLabel.Value) CenterBiomeLabel(map, mapRect);
            if (Plugin.PullWindMarkerIn.Value) PullWindMarkerIn(map, mapRect);
        }

        private static void RestoreLayout(Minimap map)
        {
            foreach (var graphic in _disabledGraphics)
            {
                if (graphic != null) graphic.enabled = true;
            }
            _disabledGraphics.Clear();

            if (_windMarkerSaved && map.m_windMarker != null)
            {
                _windMarkerOriginal.Restore(map.m_windMarker);
                _windMarkerSaved = false;
            }

            if (_biomeLabelSaved && map.m_biomeNameSmall != null)
            {
                _biomeLabelOriginal.Restore(map.m_biomeNameSmall.rectTransform);
                map.m_biomeNameSmall.alignment = _biomeAlignmentOriginal;
                _biomeLabelSaved = false;
            }

            _appliedLayout = null;
        }

        /// <summary>
        /// Disables the graphics of panels bigger than the circle (the square backing and frame),
        /// which would otherwise stick out past the round edge. Only the graphic is switched off, not
        /// the object, so anything parented to a panel keeps rendering.
        /// </summary>
        private static void HideBackingPanels(Minimap map, RectTransform mapRect)
        {
            if (map.m_smallRoot == null) return;

            var keep = new List<Transform>
            {
                mapRect,
                map.m_pinRootSmall,
                map.m_pinNameRootSmall,
                map.m_smallMarker,
                map.m_smallShipMarker,
                map.m_windMarker,
                map.m_gamepadCrosshair,
                map.m_biomeNameSmall != null ? map.m_biomeNameSmall.transform : null,
                BiomeLabelStyle.Panel,
                map.m_sharedMapHint != null ? map.m_sharedMapHint.transform : null,
                _border != null ? _border.transform : null,
            };

            // Measured against the map's vanilla size rather than the scaled circle. Scaling the map
            // up must not make a backing panel "small enough" to stop counting as backing and come
            // back into view.
            float threshold = _sizeCaptured
                ? Mathf.Min(_originalMapSize.x, _originalMapSize.y)
                : Radius * 2f;
            foreach (var graphic in map.m_smallRoot.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null || !graphic.enabled) continue;
                if (graphic is TMP_Text) continue;

                bool functional = false;
                foreach (var protectedRoot in keep)
                {
                    if (protectedRoot != null && graphic.transform.IsChildOf(protectedRoot))
                    {
                        functional = true;
                        break;
                    }
                }
                if (functional) continue;

                var rect = graphic.rectTransform.rect;
                if (rect.width <= threshold + 1f && rect.height <= threshold + 1f) continue;

                graphic.enabled = false;
                _disabledGraphics.Add(graphic);
            }

            if (_disabledGraphics.Count > 0)
            {
                var names = new string[_disabledGraphics.Count];
                for (int i = 0; i < _disabledGraphics.Count; i++) names[i] = _disabledGraphics[i].name;
                Plugin.Log.LogInfo($"Hid {_disabledGraphics.Count} backing graphic(s) over {threshold:F0}px: {string.Join(", ", names)}. " +
                                   "Turn off 'Hide square background' if one of these was wanted.");
            }
        }

        private static void CenterBiomeLabel(Minimap map, RectTransform mapRect)
        {
            var label = map.m_biomeNameSmall;
            if (label == null) return;
            var rect = label.rectTransform;
            var parent = rect.parent as RectTransform;
            if (parent == null) return;

            _biomeLabelOriginal = RectPlacement.Capture(rect);
            _biomeAlignmentOriginal = label.alignment;
            _biomeLabelSaved = true;

            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = UiTools.CenterOnAnchoredPosition(parent, mapRect)
                                    + new Vector2(0f, -(Radius + Plugin.BiomeLabelGap.Value));
            label.alignment = TextAlignmentOptions.Top;
        }

        /// <summary>
        /// Keeps the wind arrow in the corner vanilla chose, but pulls it in along that same
        /// direction until it sits inside the circle.
        /// </summary>
        private static void PullWindMarkerIn(Minimap map, RectTransform mapRect)
        {
            var marker = map.m_windMarker;
            if (marker == null) return;
            var parent = marker.parent as RectTransform;
            if (parent == null) return;

            Vector2 mapCentre = UiTools.CenterOnAnchoredPosition(parent, mapRect);

            _windMarkerOriginal = RectPlacement.Capture(marker);
            _windMarkerSaved = true;

            // Its current offset from the map centre gives us the direction to keep.
            Vector2 currentCentre = marker.anchoredPosition
                                    + (new Vector2(0.5f, 0.5f) - marker.pivot) * marker.rect.size;
            Vector2 direction = currentCentre - mapCentre;
            if (direction.sqrMagnitude < 0.01f) direction = new Vector2(0f, -1f);

            float distance = Radius * Mathf.Clamp01(Plugin.WindMarkerDistance.Value);
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.pivot = new Vector2(0.5f, 0.5f);
            marker.anchoredPosition = mapCentre + direction.normalized * distance;
        }

        /// <summary>Hides named descendants of the small map root, for anything the heuristics miss.</summary>
        public static void ApplyHiddenElements(Minimap map, string[] names, bool hidden)
        {
            if (map.m_smallRoot == null || names == null) return;
            foreach (var raw in names)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                string name = raw.Trim();
                if (name.Length == 0) continue;

                var target = FindDescendant(map.m_smallRoot.transform, name);
                if (target == null)
                {
                    Plugin.Log.LogWarning($"Hidden elements: no object named '{name}' under {map.m_smallRoot.name}.");
                    continue;
                }
                target.gameObject.SetActive(!hidden);
            }
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDescendant(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
