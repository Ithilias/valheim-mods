using UnityEngine;

namespace RoundMinimap
{
    /// <summary>
    /// Keeps HUD panels that vanilla places just under the minimap clear of a bigger one: the
    /// sailing display (sail and rudder status, wind direction) and the box naming who built the
    /// piece you look at. When the map grows downwards, each panel moves down by as much as the
    /// map's lower edge did, keeping the gap vanilla leaves. The sailing display also moves
    /// sideways with the map's centre, so it stays centred under it.
    ///
    /// The game never positions these panels, only turns their contents and switches them on and
    /// off. The rudder controls next to the ship are placed in the world every frame and are left
    /// alone.
    /// </summary>
    internal static class HudClearance
    {
        // The sailing display and the author box are run and fail separately (see Patches), so a
        // game update breaking one leaves the other working.
        private static Hud _shipHud;
        private static Hud _authorHud;
        private static readonly MovedPanel _wind = new MovedPanel(followSideways: true);
        private static readonly MovedPanel _sail = new MovedPanel(followSideways: true);
        private static readonly MovedPanel _author = new MovedPanel();
        private static readonly ScaledPanel _sailSize = new ScaledPanel();

        public static void ApplyShip(Minimap map)
        {
            var hud = Hud.instance;
            if (hud == null) return;

            if (!ReferenceEquals(hud, _shipHud))
            {
                _shipHud = hud;
                FindShipPanels(hud, out var wind, out var sail);
                _wind.Track(wind);
                _sail.Track(sail);
                _sailSize.Track(sail);
            }

            // A resized sail icon grows downwards only, keeping its gap to the map, and the wind
            // display below moves down by as much, keeping its gap to the icon.
            float factor = Plugin.SailIconSize.Value;
            _sailSize.Update(factor);
            float growth = _sailSize.WorldHeightGrowth(factor);

            bool showing = hud.m_shipHudRoot != null && hud.m_shipHudRoot.activeInHierarchy;
            _wind.Update(map, Plugin.MoveShipDisplay.Value, showing, growth);
            _sail.Update(map, Plugin.MoveShipDisplay.Value, showing, growth / 2f);
        }

        public static void ApplyAuthor(Minimap map)
        {
            var hud = Hud.instance;
            if (hud == null) return;

            if (!ReferenceEquals(hud, _authorHud))
            {
                _authorHud = hud;
                _author.Track(hud.m_hoveredPieceAuthorWindow != null
                    ? hud.m_hoveredPieceAuthorWindow.transform as RectTransform
                    : null);
            }

            _author.Update(map, Plugin.MoveAuthorBox.Value, _author.IsShowing);
        }

        public static void RestoreShip()
        {
            _wind.Restore();
            _sail.Restore();
            _sailSize.Restore();
        }

        public static void RestoreAuthor() => _author.Restore();

        /// <summary>
        /// The children of the ship HUD that hold the wind indicator and the sail and rudder status.
        /// </summary>
        private static void FindShipPanels(Hud hud, out RectTransform wind, out RectTransform sail)
        {
            wind = sail = null;
            if (hud.m_shipHudRoot == null) return;
            var root = hud.m_shipHudRoot.transform;
            var controls = hud.m_shipControlsRoot != null ? hud.m_shipControlsRoot.transform : null;

            wind = TopLevelChild(root, hud.m_shipWindIndicatorRoot, controls);
            // Whichever of the sail and rudder parts sits in its own panel, not with the controls.
            var sailParts = new Transform[]
            {
                hud.m_shipRudderIndicator != null ? hud.m_shipRudderIndicator.transform : null,
                hud.m_halfSail != null ? hud.m_halfSail.transform : null,
                hud.m_fullSail != null ? hud.m_fullSail.transform : null,
                hud.m_rudder != null ? hud.m_rudder.transform : null,
            };
            foreach (var part in sailParts)
            {
                var candidate = TopLevelChild(root, part, controls);
                if (candidate == null || candidate == wind) continue;
                sail = candidate;
                break;
            }

            if (wind == null || sail == null)
                Plugin.Log.LogWarning("Sailing display layout not fully recognised; the parts not found will " +
                                      "not be moved below the map.");
            Plugin.Log.LogInfo($"Sailing display panels: wind '{(wind != null ? wind.name : "none")}', " +
                               $"sail '{(sail != null ? sail.name : "none")}' under '{root.name}'.");
        }

        /// <summary>
        /// The direct child of <paramref name="root"/> holding <paramref name="node"/>, unless that
        /// child also holds the controls the game positions itself.
        /// </summary>
        private static RectTransform TopLevelChild(Transform root, Transform node, Transform controls)
        {
            while (node != null && node.parent != root) node = node.parent;
            var panel = node as RectTransform;
            if (panel == null || (controls != null && controls.IsChildOf(panel))) return null;
            return panel;
        }

        /// <summary>
        /// World-space bottom of the biome label while it sits centred under the round map, with
        /// its box and shadow, or +infinity otherwise. Vanilla keeps the label inside the square,
        /// so below the circle it counts as part of the map. Worked out from the label's settings
        /// rather than its drawn size, which pulses when you enter a new biome and would make the
        /// panels below twitch.
        /// </summary>
        private static float BiomeLabelBottom(Minimap map)
        {
            var label = map.m_biomeNameSmall;
            if (!MinimapShape.IsApplied || !Plugin.CenterBiomeLabel.Value || label == null
                || !label.isActiveAndEnabled)
                return float.PositiveInfinity;

            var rect = label.rectTransform;
            var parent = rect.parent;
            float scale = parent != null ? parent.lossyScale.y : 1f;

            // In the parent's units, measured down from the label's own rect.
            float below = 0f;
            if (BiomeLabelStyle.Panel != null && BiomeLabelStyle.Panel.gameObject.activeInHierarchy)
            {
                below += Mathf.Max(0f, Plugin.BiomePaddingBottom.Value);
                // Same rule as BiomeLabelStyle for whether the shadow is drawn.
                if (Plugin.BiomeShadowSize.Value > 0f && Plugin.BiomeShadowColor.Value.a > 0.001f)
                    below += Mathf.Max(0f, Plugin.BiomeShadowSize.Value + Plugin.BiomeShadowOffset.Value);
            }

            // Glyphs can reach below the rect (TMP overflows rather than clips). The text bounds are
            // in the label's own space, which the pulse scales without changing them.
            float bottom = Mathf.Min(rect.rect.yMin, label.textBounds.min.y);
            return rect.position.y + (bottom - below) * scale;
        }

        /// <summary>One panel drawn at a multiple of its size, remembering the size it had.</summary>
        private sealed class ScaledPanel
        {
            private RectTransform _panel;
            private Vector3 _original;
            private Vector3 _lastWritten;

            public void Track(RectTransform panel)
            {
                _panel = panel;
                if (_panel == null) return;
                _original = _panel.localScale;
                _lastWritten = _original;
            }

            public void Update(float factor)
            {
                if (_panel == null) return;
                // Something else (a HUD mod) resized the panel: that is the new baseline.
                if (_panel.localScale != _lastWritten) _original = _lastWritten = _panel.localScale;
                Set(_original * factor);
            }

            public void Restore()
            {
                if (_panel != null) Set(_original);
            }

            /// <summary>How much taller, in world space, the panel is at <paramref name="factor"/>.</summary>
            public float WorldHeightGrowth(float factor)
            {
                if (_panel == null) return 0f;
                var parent = _panel.parent;
                float parentScale = parent != null ? parent.lossyScale.y : 1f;
                return _panel.rect.height * _original.y * parentScale * (factor - 1f);
            }

            private void Set(Vector3 scale)
            {
                if (_panel.localScale != scale) _panel.localScale = scale;
                _lastWritten = scale;
            }
        }

        /// <summary>
        /// One panel moved down by the map's growth, and optionally sideways to stay centred under
        /// it, remembering where it was.
        /// </summary>
        private sealed class MovedPanel
        {
            // anchoredPosition does not read back exactly what was written.
            private const float Tolerance = 0.5f;

            private readonly bool _followSideways;
            private RectTransform _panel;
            private Vector2 _original;
            private Vector2 _lastWritten;
            // World space: x to the right, y downwards.
            private Vector2 _appliedShift;

            public MovedPanel(bool followSideways = false) => _followSideways = followSideways;

            public bool IsShowing => _panel != null && _panel.gameObject.activeInHierarchy;

            /// <summary>Starts following <paramref name="panel"/>, after the HUD was (re)built.</summary>
            public void Track(RectTransform panel)
            {
                _panel = panel;
                _appliedShift = Vector2.zero;
                if (_panel == null) return;
                _original = _panel.anchoredPosition;
                _lastWritten = _original;
            }

            /// <param name="extraDrop">
            /// World-space distance to move the panel down on top of following the map, whether
            /// that is enabled or not, to make room for a resized panel above it.
            /// </param>
            public void Update(Minimap map, bool enabled, bool showing, float extraDrop = 0f)
            {
                if (_panel == null) return;

                if ((_panel.anchoredPosition - _lastWritten).sqrMagnitude > Tolerance * Tolerance)
                {
                    // Something else (a HUD mod) moved the panel. Its position is the new baseline,
                    // and the shift is worked out again from there.
                    _original = _panel.anchoredPosition;
                    _lastWritten = _original;
                    _appliedShift = Vector2.zero;
                }

                var extra = new Vector2(0f, extraDrop);
                if (!enabled)
                {
                    PlaceIfMoved(extra);
                    return;
                }

                // Only measured while it shows; the large map covers the HUD anyway.
                if (!showing || map.m_mode == Minimap.MapMode.Large) return;
                if (map.m_smallRoot == null || !map.m_smallRoot.activeInHierarchy)
                {
                    PlaceIfMoved(extra);
                    return;
                }
                if (!MinimapShape.TryGetFootprint(map, out Vector3 vanillaCentre, out Vector2 vanillaHalf,
                        out Vector3 currentCentre, out Vector2 currentHalf))
                    return;

                // Where the panel would be without our shift.
                Rect panel = WorldBounds(_panel);
                panel.x -= _appliedShift.x;
                panel.y += _appliedShift.y;

                float vanillaBottom = vanillaCentre.y - vanillaHalf.y;
                float currentBottom = Mathf.Min(currentCentre.y - currentHalf.y, BiomeLabelBottom(map));
                // A panel that follows the map sideways belongs to it wherever it goes, so that is
                // decided by where vanilla put the map. Otherwise by where the map is now.
                Vector3 centre = _followSideways ? vanillaCentre : currentCentre;
                Vector2 half = _followSideways ? vanillaHalf : currentHalf;
                bool underMap = panel.yMax <= vanillaCentre.y
                                && panel.xMax > centre.x - half.x
                                && panel.xMin < centre.x + half.x;

                var shift = extra;
                if (underMap)
                {
                    shift.y += Mathf.Max(0f, vanillaBottom - currentBottom);
                    // Vanilla centres the sailing display under the map, so it keeps doing so as the
                    // map grows towards the screen centre or is moved.
                    if (_followSideways) shift.x = currentCentre.x - vanillaCentre.x;
                }
                PlaceIfMoved(shift);
            }

            public void Restore() => PlaceIfMoved(Vector2.zero);

            private void PlaceIfMoved(Vector2 worldShift)
            {
                if (_panel == null) return;
                if (Mathf.Abs(worldShift.x - _appliedShift.x) < Tolerance
                    && Mathf.Abs(worldShift.y - _appliedShift.y) < Tolerance
                    && (worldShift != Vector2.zero || _appliedShift == Vector2.zero))
                    return;
                _panel.anchoredPosition = _original + ShiftVector(worldShift);
                _lastWritten = _panel.anchoredPosition;
                _appliedShift = worldShift;
            }

            /// <summary>A world-space shift (y downwards) as an anchoredPosition offset.</summary>
            private Vector2 ShiftVector(Vector2 worldShift)
            {
                var world = new Vector3(worldShift.x, -worldShift.y, 0f);
                var parent = _panel.parent;
                return parent != null ? (Vector2)parent.InverseTransformVector(world) : (Vector2)world;
            }

            /// <summary>
            /// World-space bounds of the panel's rect, ignoring its rotation. The game turns the wind
            /// indicator with the ship's heading, and bounds that turned with it would change size as
            /// the ship turns, making the panel jump up and down near the edge of the map.
            /// </summary>
            private static Rect WorldBounds(RectTransform panel)
            {
                Vector3 pivot = panel.position;
                Vector3 scale = panel.lossyScale;
                Rect rect = panel.rect;
                return Rect.MinMaxRect(pivot.x + rect.xMin * scale.x, pivot.y + rect.yMin * scale.y,
                                       pivot.x + rect.xMax * scale.x, pivot.y + rect.yMax * scale.y);
            }
        }
    }
}
