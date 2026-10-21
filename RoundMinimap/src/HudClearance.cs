using UnityEngine;

namespace RoundMinimap
{
    /// <summary>
    /// Keeps HUD panels that vanilla places just under the minimap clear of a bigger one: the
    /// sailing display (sail and rudder status, wind direction) and the box naming who built the
    /// piece you look at. When the map grows downwards, each panel moves down by as much as the
    /// map's lower edge did, keeping the gap vanilla leaves.
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
        private static readonly MovedPanel _wind = new MovedPanel();
        private static readonly MovedPanel _sail = new MovedPanel();
        private static readonly MovedPanel _author = new MovedPanel();

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
            }

            bool showing = hud.m_shipHudRoot != null && hud.m_shipHudRoot.activeInHierarchy;
            _wind.Update(map, Plugin.MoveShipDisplay.Value, showing);
            _sail.Update(map, Plugin.MoveShipDisplay.Value, showing);
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

        /// <summary>One panel moved down by the map's growth, remembering where it was.</summary>
        private sealed class MovedPanel
        {
            // anchoredPosition does not read back exactly what was written.
            private const float Tolerance = 0.5f;

            private RectTransform _panel;
            private Vector2 _original;
            private Vector2 _lastWritten;
            private float _appliedShift;

            public bool IsShowing => _panel != null && _panel.gameObject.activeInHierarchy;

            /// <summary>Starts following <paramref name="panel"/>, after the HUD was (re)built.</summary>
            public void Track(RectTransform panel)
            {
                _panel = panel;
                _appliedShift = 0f;
                if (_panel == null) return;
                _original = _panel.anchoredPosition;
                _lastWritten = _original;
            }

            public void Update(Minimap map, bool enabled, bool showing)
            {
                if (_panel == null) return;

                if ((_panel.anchoredPosition - _lastWritten).sqrMagnitude > Tolerance * Tolerance)
                {
                    // Something else (a HUD mod) moved the panel. Its position is the new baseline,
                    // and the drop below is worked out again from there.
                    _original = _panel.anchoredPosition;
                    _lastWritten = _original;
                    _appliedShift = 0f;
                }

                if (!enabled)
                {
                    Restore();
                    return;
                }

                // Only measured while it shows; the large map covers the HUD anyway.
                if (!showing || map.m_mode == Minimap.MapMode.Large) return;
                if (map.m_smallRoot == null || !map.m_smallRoot.activeInHierarchy)
                {
                    Restore();
                    return;
                }
                if (!MinimapShape.TryGetFootprint(map, out Vector3 vanillaCentre, out Vector2 vanillaHalf,
                        out Vector3 currentCentre, out Vector2 currentHalf))
                    return;

                // Where the panel would be without our shift.
                Rect panel = WorldBounds(_panel);
                panel.y += _appliedShift;

                float vanillaBottom = vanillaCentre.y - vanillaHalf.y;
                float currentBottom = currentCentre.y - currentHalf.y;
                bool underMap = panel.yMax <= vanillaCentre.y
                                && panel.xMax > currentCentre.x - currentHalf.x
                                && panel.xMin < currentCentre.x + currentHalf.x;

                float drop = underMap ? Mathf.Max(0f, vanillaBottom - currentBottom) : 0f;
                if (drop == 0f) Restore();
                else if (Mathf.Abs(drop - _appliedShift) >= Tolerance) Place(drop);
            }

            public void Restore()
            {
                if (_panel != null && _appliedShift != 0f) Place(0f);
            }

            private void Place(float worldDrop)
            {
                _panel.anchoredPosition = _original + ShiftVector(worldDrop);
                _lastWritten = _panel.anchoredPosition;
                _appliedShift = worldDrop;
            }

            /// <summary>A downward world-space drop as an anchoredPosition offset.</summary>
            private Vector2 ShiftVector(float worldDrop)
            {
                var world = new Vector3(0f, -worldDrop, 0f);
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
