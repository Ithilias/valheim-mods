using UnityEngine;

namespace RoundMinimap
{
    /// <summary>
    /// Keeps the sailing display (sail setting and wind direction, shown while steering a ship)
    /// clear of a bigger minimap. It sits under the map, so when the map grows downwards the panel
    /// moves down by as much as the map's lower edge did, keeping the gap vanilla leaves.
    ///
    /// Only the panel holding the wind indicator is moved. The game never positions it, only turns
    /// the indicator inside it; the rudder controls next to the ship are placed in the world every
    /// frame and are left alone.
    /// </summary>
    internal static class ShipHudClearance
    {
        // anchoredPosition does not read back exactly what was written.
        private const float Tolerance = 0.5f;

        private static Hud _hud;
        private static RectTransform _panel;
        private static Vector2 _original;
        private static Vector2 _lastWritten;
        private static float _appliedShift;

        public static void Apply(Minimap map)
        {
            var hud = Hud.instance;
            if (hud == null || hud.m_shipHudRoot == null) return;

            if (!ReferenceEquals(hud, _hud))
            {
                _hud = hud;
                _panel = FindPanel(hud);
                _appliedShift = 0f;
                if (_panel != null)
                {
                    _original = _panel.anchoredPosition;
                    _lastWritten = _original;
                }
            }
            if (_panel == null) return;

            if ((_panel.anchoredPosition - _lastWritten).sqrMagnitude > Tolerance * Tolerance)
            {
                // Something else (a HUD mod) moved the panel. Its position is the new baseline, and
                // the drop below is worked out again from there.
                _original = _panel.anchoredPosition;
                _lastWritten = _original;
                _appliedShift = 0f;
            }

            if (!Plugin.MoveShipDisplay.Value)
            {
                Restore();
                return;
            }

            // Only measured while it shows; the large map covers the HUD anyway.
            if (!hud.m_shipHudRoot.activeInHierarchy || map.m_mode == Minimap.MapMode.Large) return;
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

        public static void Restore()
        {
            if (_panel != null && _appliedShift != 0f) Place(0f);
        }

        /// <summary>The child of the ship HUD that holds the wind indicator.</summary>
        private static RectTransform FindPanel(Hud hud)
        {
            var root = hud.m_shipHudRoot.transform;
            Transform node = hud.m_shipWindIndicatorRoot;
            while (node != null && node.parent != root) node = node.parent;

            var panel = node as RectTransform;
            var controls = hud.m_shipControlsRoot != null ? hud.m_shipControlsRoot.transform : null;
            if (panel == null || (controls != null && controls.IsChildOf(panel)))
            {
                Plugin.Log.LogWarning("Sailing display layout not recognised; it will not be moved below the map.");
                return null;
            }

            Plugin.Log.LogInfo($"Sailing display panel: '{panel.name}' under '{root.name}', " +
                               $"anchors {panel.anchorMin}..{panel.anchorMax}, position {panel.anchoredPosition}.");
            return panel;
        }

        private static void Place(float worldDrop)
        {
            _panel.anchoredPosition = _original + ShiftVector(worldDrop);
            _lastWritten = _panel.anchoredPosition;
            _appliedShift = worldDrop;
        }

        /// <summary>A downward world-space drop as an anchoredPosition offset.</summary>
        private static Vector2 ShiftVector(float worldDrop)
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
