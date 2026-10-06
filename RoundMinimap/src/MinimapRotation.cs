using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RoundMinimap
{
    /// <summary>
    /// Rotates the small minimap so the direction the player is looking points up.
    ///
    /// The map content is turned by <see cref="CircleMeshEffect"/> rather than by rotating a
    /// RectTransform, because Valheim writes shader parameters to the minimap material every frame
    /// and because the markers are children of the map image and would orbit its pivot. It is
    /// therefore only available on the round map.
    ///
    /// Pins are separate UI children positioned by anchoredPosition, so their positions are turned
    /// about the map centre while the icons themselves stay upright. They are recomputed every
    /// frame because vanilla only refreshes them when the map centre *moves*, which would leave
    /// them stale while the player turns in place.
    /// </summary>
    internal static class MinimapRotation
    {
        private static readonly AccessTools.FieldRef<Minimap, List<Minimap.PinData>> PinsRef =
            AccessTools.FieldRefAccess<Minimap, List<Minimap.PinData>>("m_pins");

        private static float _angle;
        private static bool _angleInitialised;
        private static bool _applied;

        /// <summary>
        /// An icon another mod put on the small map itself, outside the game's pin list. The owner
        /// only places it north-up, and only when the game refreshes pins, so its north-up position
        /// is kept here and the icon is turned from that every frame.
        /// </summary>
        private sealed class ForeignIcon
        {
            public Vector2 NorthUp;
            public Vector2 Written;
            public Vector3 Scale;
        }

        private static readonly Dictionary<RectTransform, ForeignIcon> _foreignIcons =
            new Dictionary<RectTransform, ForeignIcon>();
        private static readonly HashSet<Transform> _gamePins = new HashSet<Transform>();

        /// <summary>
        /// Game pins hidden for hanging over the round edge, with the scale to give back. Only these
        /// are ever shown again: map filter mods hide pins by switching them off, and turning a pin
        /// on just because it is inside the circle would undo their filter.
        /// </summary>
        private static readonly Dictionary<RectTransform, Vector3> _clippedPins =
            new Dictionary<RectTransform, Vector3>();
        private static readonly List<RectTransform> _unclip = new List<RectTransform>();
        private static readonly List<RectTransform> _gone = new List<RectTransform>();

        public static float Angle => _angle;

        public static void Apply(Minimap map, float deltaTime)
        {
            var image = map.m_mapImageSmall;
            if (image == null) return;

            AdvanceAngle(deltaTime);
            MinimapShape.SetAngle(_angle);

            RotatePins(map, image.uvRect, image.rectTransform.rect);
            FixMarkers(map);
            _applied = true;
        }

        /// <summary>Puts the minimap back to north-up. Safe to call when nothing was applied.</summary>
        public static void Restore(Minimap map)
        {
            if (!_applied) return;
            _applied = false;
            _angleInitialised = false;
            _angle = 0f;

            MinimapShape.SetAngle(0f);
            UnrotatePins(map);
            UnclipAllPins();
            RestoreForeignIcons();

            // Vanilla recomputes pin positions and every marker rotation on its next pass.
            Patches.RequestPinUpdate(map);
        }

        /// <summary>Drops cached state after the minimap HUD has been destroyed and rebuilt.</summary>
        public static void ResetState()
        {
            _applied = false;
            _angleInitialised = false;
            _angle = 0f;
            _foreignIcons.Clear();
            _clippedPins.Clear();
        }

        private static void AdvanceAngle(float deltaTime)
        {
            float target = NormaliseAngle(TargetAngle());
            if (!_angleInitialised)
            {
                _angle = target;
                _angleInitialised = true;
            }
            else
            {
                float smoothing = Plugin.RotationSmoothing.Value;
                _angle = smoothing <= 0f
                    ? target
                    : Mathf.LerpAngle(_angle, target, 1f - Mathf.Exp(-deltaTime / smoothing));
            }
            _angle = NormaliseAngle(_angle);
        }

        private static void RotatePins(Minimap map, Rect uvRect, Rect mapRect)
        {
            var pins = PinsRef(map);
            if (pins == null) return;
            if (uvRect.width <= 0f || uvRect.height <= 0f) return;

            int textureSize = map.m_textureSize;
            float pixelSize = map.m_pixelSize;
            if (textureSize <= 0 || pixelSize <= 0f) return;
            float half = textureSize / 2;

            // MapPointToLocalGuiPos maps the visible map area onto [0..width] x [0..height], so the
            // player (always at the centre of the small map) sits at half the rect size.
            Vector2 centre = new Vector2(mapRect.width, mapRect.height) * 0.5f;
            float clipRadius = MinimapShape.IsApplied ? MinimapShape.Radius : 0f;

            for (int i = 0; i < pins.Count; i++)
            {
                var pin = pins[i];
                if (pin == null || pin.m_uiElement == null) continue;

                // Inlined Minimap.WorldToMapPoint.
                float mx = (pin.m_pos.x / pixelSize + half) / textureSize;
                float my = (pin.m_pos.z / pixelSize + half) / textureSize;

                Vector2 local = new Vector2(
                    (mx - uvRect.xMin) / uvRect.width * mapRect.width,
                    (my - uvRect.yMin) / uvRect.height * mapRect.height);

                Vector2 offset = Rotate(local - centre, _angle);
                pin.m_uiElement.anchoredPosition = centre + offset;

                if (Plugin.KeepPinsUpright.Value)
                    pin.m_uiElement.localRotation = Quaternion.identity;

                // Nothing clips the pins, so drop the ones that would hang over the round edge.
                float iconRadius = pin.m_uiElement.rect.width * 0.5f;
                bool outside = clipRadius > 0f && offset.magnitude > Mathf.Max(0f, clipRadius - iconRadius);
                ClipPin(pin.m_uiElement, outside);

                var name = pin.m_NamePinData;
                if (name != null && name.PinNameRectTransform != null)
                    name.PinNameRectTransform.anchoredPosition = centre + offset;
            }

            ForgetDestroyedPins();

            if (Plugin.RotateOtherModIcons.Value)
                RotateForeignIcons(map, pins, centre, clipRadius);
            else
                RestoreForeignIcons();
        }

        /// <summary>
        /// Turns icons that other mods add straight under the small pin root, such as HUDCompass's
        /// cart, ship and portal markers. They are positioned in the same space as the game's pins
        /// (MapPointToLocalGuiPos on the small map image), so the same turn about the centre applies.
        ///
        /// A position clearly different from the one written last frame means the owner has just
        /// placed the icon again, north up, and that becomes the position to turn from.
        /// </summary>
        private static void RotateForeignIcons(Minimap map, List<Minimap.PinData> pins, Vector2 centre,
            float clipRadius)
        {
            var root = map.m_pinRootSmall;
            if (root == null) return;

            _gamePins.Clear();
            for (int i = 0; i < pins.Count; i++)
            {
                var element = pins[i]?.m_uiElement;
                if (element != null) _gamePins.Add(element);
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var icon = root.GetChild(i) as RectTransform;
                if (icon == null || _gamePins.Contains(icon)) continue;

                if (!_foreignIcons.TryGetValue(icon, out var state))
                {
                    state = new ForeignIcon
                    {
                        NorthUp = icon.anchoredPosition,
                        Scale = icon.localScale,
                    };
                    _foreignIcons[icon] = state;
                }
                else if ((icon.anchoredPosition - state.Written).sqrMagnitude > 0.25f)
                {
                    // Moved by more than half a pixel since we wrote it: the owner placed it again.
                    // Reading back our own write is not exact (anchoredPosition is recomputed from
                    // localPosition), and treating that rounding as a new placement would turn the
                    // icon again on every frame, spinning it once the map stops moving.
                    state.NorthUp = icon.anchoredPosition;
                }

                Vector2 offset = Rotate(state.NorthUp - centre, _angle);
                state.Written = centre + offset;
                icon.anchoredPosition = state.Written;
                if (Plugin.KeepPinsUpright.Value) icon.localRotation = Quaternion.identity;

                bool outside = clipRadius > 0f
                               && offset.magnitude > Mathf.Max(0f, clipRadius - icon.rect.width * 0.5f);
                SetHidden(icon, state, outside);
            }

            // Forget icons their owner destroyed or moved elsewhere.
            _gone.Clear();
            foreach (var entry in _foreignIcons)
            {
                if (entry.Key == null || entry.Key.parent != root) _gone.Add(entry.Key);
            }
            foreach (var icon in _gone) _foreignIcons.Remove(icon);
        }

        /// <summary>Puts other mods' icons back where their owner placed them, and visible.</summary>
        private static void RestoreForeignIcons()
        {
            foreach (var entry in _foreignIcons)
            {
                if (entry.Key == null) continue;
                entry.Key.anchoredPosition = entry.Value.NorthUp;
                SetHidden(entry.Key, entry.Value, false);
            }
            _foreignIcons.Clear();
        }

        /// <summary>
        /// Hides by scaling to nothing, reapplied every frame. Switching the object off would fight
        /// the owner, which turns its icons back on, and the renderer's cull flag is overwritten each
        /// frame by Unity's own clipping under a RectMask2D. Nothing else touches an icon's scale.
        /// </summary>
        private static void SetHidden(RectTransform icon, ForeignIcon state, bool hidden)
        {
            Vector3 wanted = hidden ? Vector3.zero : state.Scale;
            if (icon.localScale != wanted) icon.localScale = wanted;
        }

        private static void UnrotatePins(Minimap map)
        {
            var pins = PinsRef(map);
            if (pins == null) return;
            for (int i = 0; i < pins.Count; i++)
            {
                var element = pins[i]?.m_uiElement;
                if (element != null) element.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// Hides a pin by scaling it to nothing rather than switching it off, so whether it is on
        /// stays entirely up to the game and to filter mods such as TheGreatestMap. That mod also
        /// sets pin scales for its marker sizes whenever the game refreshes pins; a non-zero scale
        /// on a hidden pin is such a write, and becomes the scale to give back.
        /// </summary>
        private static void ClipPin(RectTransform pin, bool outside)
        {
            if (outside)
            {
                if (pin.localScale != Vector3.zero)
                {
                    _clippedPins[pin] = pin.localScale;
                    pin.localScale = Vector3.zero;
                }
            }
            else if (_clippedPins.TryGetValue(pin, out Vector3 scale))
            {
                pin.localScale = scale;
                _clippedPins.Remove(pin);
            }
        }

        private static void UnclipAllPins()
        {
            foreach (var entry in _clippedPins)
            {
                if (entry.Key != null) entry.Key.localScale = entry.Value;
            }
            _clippedPins.Clear();
        }

        /// <summary>The game destroys a pin's marker when it leaves the map or is filtered out.</summary>
        private static void ForgetDestroyedPins()
        {
            _unclip.Clear();
            foreach (var entry in _clippedPins)
            {
                if (entry.Key == null) _unclip.Add(entry.Key);
            }
            foreach (var pin in _unclip) _clippedPins.Remove(pin);
        }

        /// <summary>
        /// Re-points the markers that vanilla aligned against a north-up map: the player arrow
        /// becomes fixed (the map turns instead), and wind/ship become relative to the map rotation.
        /// </summary>
        private static void FixMarkers(Minimap map)
        {
            if (map.m_smallMarker != null)
            {
                // The arrow shows where the player is looking relative to where the map points.
                map.m_smallMarker.rotation = Plugin.FixPlayerMarker.Value
                    ? Quaternion.identity
                    : Quaternion.Euler(0f, 0f, _angle - CameraYaw());
            }

            if (map.m_windMarker != null && EnvMan.instance != null)
            {
                float windYaw = Quaternion.LookRotation(EnvMan.instance.GetWindDir()).eulerAngles.y;
                map.m_windMarker.rotation = Quaternion.Euler(0f, 0f, _angle - windYaw);
            }

            var player = Player.m_localPlayer;
            if (map.m_smallShipMarker != null && map.m_smallShipMarker.gameObject.activeSelf)
            {
                var ship = player != null ? player.GetControlledShip() : null;
                if (ship != null)
                {
                    float shipYaw = ship.transform.rotation.eulerAngles.y;
                    map.m_smallShipMarker.rotation = Quaternion.Euler(0f, 0f, _angle - shipYaw);
                }
            }
        }

        private static float CameraYaw()
        {
            var camera = Utils.GetMainCamera();
            if (camera != null) return camera.transform.rotation.eulerAngles.y;
            var player = Player.m_localPlayer;
            return player != null ? player.transform.rotation.eulerAngles.y : 0f;
        }

        private static float TargetAngle()
        {
            if (Plugin.RotationSource.Value == RotationMode.PlayerBody)
            {
                var player = Player.m_localPlayer;
                if (player != null) return player.transform.rotation.eulerAngles.y;
            }
            return CameraYaw();
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private static float NormaliseAngle(float degrees)
        {
            degrees %= 360f;
            return degrees < 0f ? degrees + 360f : degrees;
        }
    }
}
