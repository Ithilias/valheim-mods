using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace RoundMinimap
{
    /// <summary>
    /// Arranges the status effect icons (Rested, Wet, Cold and so on). Only the icons inside
    /// <see cref="Hud.m_statusEffectListRoot"/> are moved, never the root: other mods place their own
    /// HUD pieces from the root's position (the ValheimPlus clock takes its height), and those
    /// should stay where they are.
    ///
    /// When a bigger or moved map reaches the icons they move aside, keeping the gap vanilla leaves.
    /// If that would run them off the screen or into something else on the HUD, fewer icons go in
    /// each row so the list grows downwards instead. Only the map, the screen edge, hotbars and the
    /// ValheimPlus clock are avoided automatically; anything else is for the manual settings.
    /// </summary>
    internal static class StatusEffectClearance
    {
        // anchoredPosition does not read back exactly what was written.
        private const float Tolerance = 0.5f;
        private const float ObstacleRefreshSeconds = 2f;

        // Vanilla's own layout, from Hud.UpdateStatusEffects.
        private const float FirstIconX = -4f;

        private static readonly AccessTools.FieldRef<Hud, List<RectTransform>> IconsRef =
            AccessTools.FieldRefAccess<Hud, List<RectTransform>>("m_statusEffects");

        private static Hud _hud;
        private static RectTransform _root;
        private static Vector2 _appliedShift;
        private static int _gamePerRow;
        private static int _appliedPerRow;
        private static float _gameSpacing;
        private static float _appliedSpacing;
        private static float _appliedRowSpacing;

        private static readonly List<HotkeyBar> _hotbars = new List<HotkeyBar>();
        private static float _nextObstacleRefresh;
        private static readonly List<Rect> _obstacles = new List<Rect>();

        private static bool _clockLookedUp;
        private static FieldInfo _clockField;

        private static readonly Vector3[] Corners = new Vector3[4];

        public static void Apply(Minimap map)
        {
            var hud = Hud.instance;
            var root = hud != null ? hud.m_statusEffectListRoot : null;
            if (root == null) return;

            if (!ReferenceEquals(hud, _hud) || !ReferenceEquals(root, _root))
            {
                _hud = hud;
                _root = root;
                _appliedShift = Vector2.zero;
                _gamePerRow = Mathf.Max(1, hud.m_effectsPerRow);
                _appliedPerRow = _gamePerRow;
                _gameSpacing = hud.m_statusEffectSpacing;
                _appliedSpacing = _gameSpacing;
                _appliedRowSpacing = _gameSpacing;
                _hotbars.Clear();
                _nextObstacleRefresh = 0f;
                StatusNameStyle.Attach(hud);
            }

            StatusNameStyle.Apply(hud, Plugin.StatusLineUpNames.Value);

            var manual = new Vector2(Plugin.StatusOffsetX.Value, Plugin.StatusOffsetY.Value);
            int basePerRow = Plugin.StatusIconsPerRow.Value > 0 ? Plugin.StatusIconsPerRow.Value : _gamePerRow;
            float spacingLocal = Plugin.StatusIconSpacing.Value > 0f ? Plugin.StatusIconSpacing.Value : _gameSpacing;
            float rowSpacingLocal = Plugin.StatusRowSpacing.Value > 0f ? Plugin.StatusRowSpacing.Value : spacingLocal;

            var icons = IconsRef(hud);
            if (!Plugin.MakeRoomForStatusEffects.Value || icons == null)
            {
                Place(manual, basePerRow, spacingLocal, rowSpacingLocal);
                return;
            }

            // Row length and spacing go in first, so what gets measured below is the layout the
            // shift is being worked out for.
            Place(_appliedShift, _appliedPerRow, spacingLocal, rowSpacingLocal);

            // With no icons showing there is nothing to measure, so keep the last placement rather
            // than snapping back and forth as effects come and go.
            if (!Measure(icons, out float right, out float top, out Vector2 iconSize, out int count)) return;
            if (!MinimapShape.TryGetFootprint(map, out Vector3 vanillaCentre, out Vector2 vanillaHalf,
                    out Vector3 currentCentre, out Vector2 currentHalf))
                return;

            // Undo our own shift so everything is measured from where vanilla put the icons. The
            // first icon of each row stays put whatever the row length, so the right and top edges
            // do not depend on how the rows are currently wrapped.
            Vector2 appliedWorld = LocalToWorld(root, _appliedShift);
            Vector2 manualWorld = LocalToWorld(root, manual);
            right -= appliedWorld.x;
            top -= appliedWorld.y;
            float spacing = LocalToWorld(root, new Vector2(spacingLocal, 0f)).x;
            float rowSpacing = LocalToWorld(root, new Vector2(0f, rowSpacingLocal)).y;

            // -1 when the icons sit left of the map, +1 when right.
            float side = right < vanillaCentre.x ? -1f : 1f;
            float vanillaMapEdge = vanillaCentre.x + side * vanillaHalf.x;
            float currentMapEdge = currentCentre.x + side * currentHalf.x;
            float gameSpacing = LocalToWorld(root, new Vector2(_gameSpacing, 0f)).x;
            float vanillaGap = side < 0f
                ? vanillaMapEdge - right
                : RowLeft(right, _gamePerRow, count, gameSpacing, iconSize.x) - vanillaMapEdge;

            GetScreenBounds(root, out float screenLeft, out float screenRight);
            float margin = Mathf.Max(0f, Plugin.StatusEdgeMargin.Value);
            int minPerRow = Plugin.StatusWrapRows.Value
                ? Mathf.Clamp(Plugin.StatusMinPerRow.Value, 1, basePerRow)
                : basePerRow;
            CollectObstacles();

            // Try the full row first and shorten it only as far as needed. If nothing fits, the
            // shortest allowed row still gets pushed clear of the map: covering something else on
            // the HUD is the lesser evil compared to covering the map.
            int chosenPerRow = basePerRow;
            float chosenShift = 0f;
            for (int perRow = basePerRow; perRow >= minPerRow; perRow--)
            {
                int rows = Mathf.CeilToInt(count / (float)perRow);
                float bandTop = top + manualWorld.y;
                float bandBottom = bandTop - (rows - 1) * rowSpacing - iconSize.y;
                float left = RowLeft(right, perRow, count, spacing, iconSize.x);

                float shift = 0f;
                bool besideMap = currentCentre.y - currentHalf.y < bandTop
                                 && currentCentre.y + currentHalf.y > bandBottom;
                if (besideMap)
                {
                    float nearEdge = (side < 0f ? right : left) + manualWorld.x;
                    float wanted = currentMapEdge + side * vanillaGap;
                    if ((nearEdge - wanted) * side < 0f) shift = wanted - nearEdge;
                }

                chosenPerRow = perRow;
                chosenShift = shift;

                float offsetX = shift + manualWorld.x;
                if (Fits(side, left + offsetX, right + offsetX, bandBottom, bandTop, screenLeft, screenRight, margin))
                    break;
            }

            Vector2 autoLocal = WorldToLocal(root, new Vector2(chosenShift, 0f));
            Place(manual + autoLocal, chosenPerRow, spacingLocal, rowSpacingLocal);
        }

        public static void Restore()
        {
            if (_root == null || _hud == null) return;
            Place(Vector2.zero, _gamePerRow, _gameSpacing, _gameSpacing);
            StatusNameStyle.Restore();
        }

        /// <summary>Left edge of the icon block for a row length, measured from its right edge.</summary>
        private static float RowLeft(float right, int perRow, int count, float spacing, float iconWidth) =>
            right - (Mathf.Min(count, perRow) - 1) * spacing - iconWidth;

        /// <summary>
        /// True when the far edge of the icons (the one away from the map) stays inside the screen
        /// and clear of every obstacle sharing their rows.
        /// </summary>
        private static bool Fits(float side, float left, float right, float bottom, float top,
            float screenLeft, float screenRight, float margin)
        {
            if (side < 0f && left < screenLeft + margin) return false;
            if (side > 0f && right > screenRight - margin) return false;

            foreach (var bounds in _obstacles)
            {
                if (bounds.yMax <= bottom || bounds.yMin >= top) continue;

                // Only obstacles on the far side matter; one between the icons and the map would
                // already be under the map.
                if (side < 0f && bounds.center.x < right && left < bounds.xMax + margin) return false;
                if (side > 0f && bounds.center.x > left && right > bounds.xMin - margin) return false;
            }
            return true;
        }

        private static void Place(Vector2 shift, int perRow, float spacing, float rowSpacing)
        {
            if (perRow == _appliedPerRow && Mathf.Approximately(spacing, _appliedSpacing)
                && Mathf.Approximately(rowSpacing, _appliedRowSpacing)
                && (shift - _appliedShift).sqrMagnitude < Tolerance * Tolerance)
                return;

            // Row length and icon spacing also go into the game's own fields, so its next rebuild
            // starts from them.
            _hud.m_effectsPerRow = perRow;
            _hud.m_statusEffectSpacing = spacing;
            _appliedPerRow = perRow;
            _appliedSpacing = spacing;
            _appliedRowSpacing = rowSpacing;
            _appliedShift = shift;
            Relayout();
        }

        /// <summary>
        /// Called right after the game rebuilds the icons, which happens whenever the number of
        /// effects changes. The game knows nothing of the shift or a separate row spacing, so the
        /// icons are put back where this layout wants them before the frame is drawn.
        /// </summary>
        public static void OnIconsRebuilt(Hud hud)
        {
            if (!ReferenceEquals(hud, _hud) || IsVanillaLayout) return;
            Relayout();
        }

        private static bool IsVanillaLayout =>
            _appliedPerRow == _gamePerRow
            && Mathf.Approximately(_appliedSpacing, _gameSpacing)
            && Mathf.Approximately(_appliedRowSpacing, _gameSpacing)
            && _appliedShift.sqrMagnitude < Tolerance * Tolerance;

        /// <summary>The game's own layout formula, plus the shift and the separate row spacing.</summary>
        private static void Relayout()
        {
            var icons = IconsRef(_hud);
            if (icons == null) return;
            for (int i = 0; i < icons.Count; i++)
            {
                if (icons[i] == null) continue;
                int row = i / _appliedPerRow;
                int col = i - row * _appliedPerRow;
                icons[i].anchoredPosition = _appliedShift
                    + new Vector2(FirstIconX - col * _appliedSpacing, -row * _appliedRowSpacing);
            }
        }

        /// <summary>
        /// World-space right and top edges of the icon block, the size of the largest icon, and the
        /// count. Each icon counts with its name and timer text, since those are wider than the
        /// icon itself and are what actually runs into things.
        /// </summary>
        private static bool Measure(List<RectTransform> icons, out float right, out float top,
            out Vector2 iconSize, out int count)
        {
            right = top = float.MinValue;
            iconSize = Vector2.zero;
            count = 0;
            foreach (var icon in icons)
            {
                if (icon == null || !icon.gameObject.activeSelf) continue;
                Rect bounds = ContentBounds(icon);
                right = Mathf.Max(right, bounds.xMax);
                top = Mathf.Max(top, bounds.yMax);
                iconSize = Vector2.Max(iconSize, bounds.size);
                count++;
            }
            return count > 0;
        }

        private static Rect ContentBounds(RectTransform icon)
        {
            icon.GetWorldCorners(Corners);
            float xMin = Corners[0].x, yMin = Corners[0].y, xMax = Corners[2].x, yMax = Corners[1].y;
            foreach (var text in icon.GetComponentsInChildren<TMP_Text>(false))
            {
                if (!TryGetTextBounds(text, out Rect t)) continue;
                xMin = Mathf.Min(xMin, t.xMin);
                yMin = Mathf.Min(yMin, t.yMin);
                xMax = Mathf.Max(xMax, t.xMax);
                yMax = Mathf.Max(yMax, t.yMax);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>World-space bounds of the text actually drawn, which can spill past its rect.</summary>
        private static bool TryGetTextBounds(TMP_Text text, out Rect bounds)
        {
            bounds = default;
            if (text == null || !text.isActiveAndEnabled || string.IsNullOrEmpty(text.text)) return false;
            Bounds local = text.textBounds;
            if (local.size.x <= 0f || local.size.y <= 0f) return false;
            Vector3 min = text.transform.TransformPoint(local.min);
            Vector3 max = text.transform.TransformPoint(local.max);
            bounds = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y),
                                     Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
            return true;
        }

        /// <summary>
        /// Everything the icons must not be pushed into: hotbars and the ValheimPlus clock. The
        /// hotbar lookup is refreshed every couple of seconds; the bounds themselves are read every
        /// frame, since these can move.
        /// </summary>
        private static void CollectObstacles()
        {
            if (Time.unscaledTime >= _nextObstacleRefresh)
            {
                _nextObstacleRefresh = Time.unscaledTime + ObstacleRefreshSeconds;
                _hotbars.Clear();
                // EquipmentAndQuickSlots adds its quick slots as a second HotkeyBar.
                _hotbars.AddRange(Object.FindObjectsByType<HotkeyBar>(FindObjectsSortMode.None));
            }

            _obstacles.Clear();
            if (Plugin.StatusAvoidHotbars.Value)
            {
                foreach (var bar in _hotbars)
                {
                    if (bar != null && bar.isActiveAndEnabled && TryGetBarBounds(bar, out Rect b)) _obstacles.Add(b);
                }
            }
            if (Plugin.StatusAvoidValheimPlusClock.Value && TryGetValheimPlusClock(out Rect clock))
                _obstacles.Add(clock);
        }

        /// <summary>
        /// ValheimPlus draws its clock at the screen's centre line, at the height of the status list,
        /// on an unnamed object. Its own static field is the only reliable way to find it, so it is
        /// read by reflection and ValheimPlus stays optional.
        /// </summary>
        private static bool TryGetValheimPlusClock(out Rect bounds)
        {
            bounds = default;
            if (!_clockLookedUp)
            {
                _clockLookedUp = true;
                var type = AccessTools.TypeByName("ValheimPlus.GameClasses.Player_Update_Patch");
                _clockField = type != null ? AccessTools.Field(type, "timeObj") : null;
                if (_clockField != null) Plugin.Log.LogInfo("Found the ValheimPlus clock; status effects will keep clear of it.");
            }
            if (_clockField == null) return false;

            var clock = _clockField.GetValue(null) as GameObject;
            if (clock == null || !clock.activeInHierarchy) return false;
            return TryGetTextBounds(clock.GetComponent<TMP_Text>(), out bounds);
        }

        /// <summary>
        /// The bar's own rect does not match what it draws, since slots are placed along it by
        /// spacing. The union of its active slots does.
        /// </summary>
        private static bool TryGetBarBounds(HotkeyBar bar, out Rect bounds)
        {
            bounds = default;
            bool any = false;
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            var transform = bar.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                if (!(transform.GetChild(i) is RectTransform slot) || !slot.gameObject.activeInHierarchy) continue;
                slot.GetWorldCorners(Corners);
                xMin = Mathf.Min(xMin, Corners[0].x);
                yMin = Mathf.Min(yMin, Corners[0].y);
                xMax = Mathf.Max(xMax, Corners[2].x);
                yMax = Mathf.Max(yMax, Corners[1].y);
                any = true;
            }
            if (any) bounds = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return any;
        }

        private static void GetScreenBounds(RectTransform root, out float left, out float right)
        {
            var canvas = root.GetComponentInParent<Canvas>();
            var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (canvasRect == null)
            {
                left = 0f;
                right = Screen.width;
                return;
            }
            canvasRect.GetWorldCorners(Corners);
            left = Corners[0].x;
            right = Corners[2].x;
        }

        private static Vector2 LocalToWorld(Transform space, Vector2 local) =>
            space != null ? (Vector2)space.TransformVector(local) : local;

        private static Vector2 WorldToLocal(Transform space, Vector2 world) =>
            space != null ? (Vector2)space.InverseTransformVector(world) : world;
    }
}
