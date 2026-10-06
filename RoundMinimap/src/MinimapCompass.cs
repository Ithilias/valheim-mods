using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RoundMinimap
{
    internal enum CompassMode
    {
        Off,
        /// <summary>A single north marker.</summary>
        NorthOnly,
        /// <summary>North, east, south and west.</summary>
        Cardinal,
    }

    /// <summary>
    /// Draws cardinal letters around the edge of the round minimap.
    ///
    /// The map content is turned counter-clockwise by the rotation angle, so a heading that sat at
    /// some angle on a north-up map now sits at that angle plus the rotation. North starts at the top
    /// (90 degrees measured counter-clockwise from the right), which puts each letter at its base
    /// angle plus the current map rotation. The letters themselves are never rotated, so they stay
    /// upright and readable whichever way the map is facing.
    /// </summary>
    internal static class MinimapCompass
    {
        private const string LabelPrefix = "RoundMinimapCompass";

        // North, east, south, west as angles counter-clockwise from the right, matching the order
        // letters are read out of the config.
        private static readonly float[] BaseAngles = { 90f, 0f, 270f, 180f };

        private static readonly List<RectTransform> _labels = new List<RectTransform>();
        private static readonly List<float> _angles = new List<float>();

        private static string _appliedSignature;
        private static float _radius;

        public static bool IsApplied => _labels.Count > 0;

        public static void Apply(Minimap map, float radius)
        {
            var mode = Plugin.CompassModeSetting.Value;

            string signature = string.Join("|",
                mode.ToString(),
                Plugin.CompassLetters.Value ?? string.Empty,
                Plugin.CompassFontSize.Value.ToString("F1"),
                radius.ToString("F1"));

            if (signature == _appliedSignature && (IsApplied || mode == CompassMode.Off))
            {
                RefreshColour();
                return;
            }

            Remove();
            _appliedSignature = signature;
            _radius = radius;

            if (mode == CompassMode.Off || radius <= 0f) return;

            var source = map.m_biomeNameSmall;
            if (source == null)
            {
                Plugin.Log.LogWarning("No biome label to copy a font from; skipping the compass.");
                return;
            }

            var parent = map.m_mapImageSmall != null ? map.m_mapImageSmall.rectTransform.parent as RectTransform : null;
            if (parent == null) return;

            var letters = SplitLetters(Plugin.CompassLetters.Value);
            int wanted = mode == CompassMode.NorthOnly ? 1 : BaseAngles.Length;

            for (int i = 0; i < wanted && i < letters.Count; i++)
            {
                if (string.IsNullOrEmpty(letters[i])) continue;

                // Cloning the biome label is the simplest way to inherit a working TMP font and
                // material rather than trying to build one at runtime.
                var go = Object.Instantiate(source.gameObject, parent);
                go.name = $"{LabelPrefix}_{letters[i]}";

                var text = go.GetComponent<TMP_Text>();
                if (text == null)
                {
                    Object.Destroy(go);
                    continue;
                }

                text.text = letters[i];
                text.alignment = TextAlignmentOptions.Center;
                text.fontSize = Plugin.CompassFontSize.Value;
                text.raycastTarget = false;

                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(Plugin.CompassFontSize.Value * 2f, Plugin.CompassFontSize.Value * 1.6f);
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                rect.SetAsLastSibling();
                go.SetActive(true);

                _labels.Add(rect);
                _angles.Add(BaseAngles[i]);
            }

            RefreshColour();
            Plugin.Log.LogInfo($"Compass applied ({_labels.Count} marker(s)).");
        }

        /// <summary>Places each letter around the edge for the current map rotation.</summary>
        public static void SetAngle(Minimap map, float mapAngle)
        {
            if (!IsApplied) return;
            if (map.m_mapImageSmall == null) return;

            var mapRect = map.m_mapImageSmall.rectTransform;
            var parent = mapRect.parent as RectTransform;
            if (parent == null) return;

            Vector2 centre = UiTools.CenterOnAnchoredPosition(parent, mapRect);
            float distance = _radius * Mathf.Clamp(Plugin.CompassDistance.Value, 0.1f, 1.5f);

            for (int i = 0; i < _labels.Count; i++)
            {
                var label = _labels[i];
                if (label == null) continue;

                float radians = (_angles[i] + mapAngle) * Mathf.Deg2Rad;
                label.anchoredPosition = centre + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * distance;
                label.localRotation = Quaternion.identity;
            }
        }

        public static void Remove()
        {
            foreach (var label in _labels)
            {
                if (label != null) Object.Destroy(label.gameObject);
            }
            _labels.Clear();
            _angles.Clear();
            _appliedSignature = null;
            _radius = 0f;
        }

        /// <summary>Drops references after the minimap HUD has been destroyed and rebuilt.</summary>
        public static void ResetState()
        {
            _labels.Clear();
            _angles.Clear();
            _appliedSignature = null;
            _radius = 0f;
        }

        private static void RefreshColour()
        {
            foreach (var label in _labels)
            {
                if (label == null) continue;
                var text = label.GetComponent<TMP_Text>();
                if (text != null) text.color = Plugin.CompassColor.Value;
            }
        }

        private static List<string> SplitLetters(string value)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(value)) return result;
            foreach (var part in value.Split(','))
            {
                result.Add(part.Trim());
            }
            return result;
        }
    }
}
