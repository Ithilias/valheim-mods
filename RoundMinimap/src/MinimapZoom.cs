using UnityEngine;

namespace RoundMinimap
{
    /// <summary>
    /// Keyboard zoom for the small map, and remembering the zoom level across sessions.
    ///
    /// The game already zooms the small map through its own MapZoomIn / MapZoomOut bindings, and
    /// Minimap.SmallZoom clamps itself to m_minZoom..m_maxZoom. This does not replace that: it adds
    /// bindings that do not depend on how those actions happen to be bound, and persists whatever
    /// zoom you end up at, however you changed it.
    ///
    /// SmallZoom is the size of the sampled uv rect, so a smaller value means a closer view.
    /// </summary>
    internal static class MinimapZoom
    {
        private static float _lastSeenZoom = -1f;
        private static bool _restored;

        public static void ResetState()
        {
            _lastSeenZoom = -1f;
            _restored = false;
        }

        public static void Update(Minimap map)
        {
            if (map == null) return;

            // Put the remembered zoom back once the map is live, before reacting to any input.
            if (!_restored)
            {
                _restored = true;
                float remembered = Plugin.ZoomLevel.Value;
                if (remembered > 0f) map.SmallZoom = remembered;
                _lastSeenZoom = map.SmallZoom;
            }

            float step = Mathf.Max(1.01f, Plugin.ZoomStep.Value);

            if (Plugin.ZoomInKey.Value.IsDown())
            {
                map.SmallZoom /= step;
            }
            else if (Plugin.ZoomOutKey.Value.IsDown())
            {
                map.SmallZoom *= step;
            }

            // SmallZoom clamps on assignment, so read it back rather than trusting what we wrote,
            // and persist changes made by the game's own zoom bindings too.
            float current = map.SmallZoom;
            if (!Mathf.Approximately(current, _lastSeenZoom))
            {
                _lastSeenZoom = current;
                Plugin.ZoomLevel.Value = current;
            }
        }
    }
}
