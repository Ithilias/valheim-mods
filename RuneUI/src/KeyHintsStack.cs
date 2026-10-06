using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RuneUI
{
    /// <summary>
    /// Stacks the key hints in the bottom right vertically instead of in a long row that runs into the
    /// food slots. Vanilla lays each hint row out with a horizontal layout group; this swaps that for
    /// a vertical one with the same settings, and swaps it back exactly when turned off.
    /// </summary>
    internal static class KeyHintsStack
    {
        private sealed class Swapped
        {
            public GameObject Go;
            public RectOffset Padding;
            public float Spacing;
            public TextAnchor Alignment;
            public bool ControlWidth, ControlHeight, ExpandWidth, ExpandHeight, Reverse;
        }

        private static KeyHints _tracked;
        private static bool _applied;
        private static bool _warned;
        private static readonly List<Swapped> Swaps = new List<Swapped>();

        /// <summary>KeyHints.Update postfix.</summary>
        public static void Update(KeyHints hints)
        {
            if (!ReferenceEquals(hints, _tracked))
            {
                _tracked = hints;
                _applied = false;
                Swaps.Clear();
            }
            bool want = Plugin.ModEnabled.Value && Plugin.StackKeyHints.Value;
            if (want && !_applied) Apply(hints);
            else if (!want && _applied) Restore();
            if (!_applied) return;
            float spacing = Plugin.KeyHintsSpacing.Value;
            foreach (var saved in Swaps)
            {
                var column = saved.Go != null ? saved.Go.GetComponent<VerticalLayoutGroup>() : null;
                if (column != null && !Mathf.Approximately(column.spacing, spacing)) column.spacing = spacing;
            }
        }

        private static void Apply(KeyHints hints)
        {
            _applied = true;
            // The radial menu hints have their own layout and are left alone.
            GameObject[] groups =
            {
                hints.m_buildHints, hints.m_combatHints, hints.m_inventoryHints,
                hints.m_inventoryWithContainerHints, hints.m_fishingHints, hints.m_barberHints,
            };
            foreach (var group in groups)
            {
                if (group == null) continue;
                foreach (var row in group.GetComponentsInChildren<HorizontalLayoutGroup>(true))
                {
                    // Only the rows themselves: a single hint can use a horizontal layout for its
                    // key and label, and that one sits below a row.
                    if (HasRowAbove(row.transform, group.transform)) continue;
                    Swap(row);
                }
            }
            if (Swaps.Count == 0 && !_warned)
            {
                _warned = true;
                Plugin.Log.LogWarning("Found no key hint rows to stack; the game's key hint layout may have changed.");
            }
        }

        private static bool HasRowAbove(Transform t, Transform groupRoot)
        {
            for (var p = t.parent; p != null; p = p.parent)
            {
                if (p.GetComponent<HorizontalLayoutGroup>() != null) return true;
                if (p == groupRoot) break;
            }
            return false;
        }

        private static void Swap(HorizontalLayoutGroup row)
        {
            var go = row.gameObject;
            var saved = new Swapped
            {
                Go = go,
                Padding = new RectOffset(row.padding.left, row.padding.right, row.padding.top, row.padding.bottom),
                Spacing = row.spacing,
                Alignment = row.childAlignment,
                ControlWidth = row.childControlWidth,
                ControlHeight = row.childControlHeight,
                ExpandWidth = row.childForceExpandWidth,
                ExpandHeight = row.childForceExpandHeight,
                Reverse = row.reverseArrangement,
            };
            // A GameObject holds one layout group, so the horizontal one has to go first.
            Object.DestroyImmediate(row);
            var column = go.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(saved.Padding.left, saved.Padding.right, saved.Padding.top, saved.Padding.bottom);
            column.spacing = Plugin.KeyHintsSpacing.Value;
            column.childAlignment = TextAnchor.LowerRight;
            column.childControlWidth = saved.ControlWidth;
            column.childControlHeight = saved.ControlHeight;
            column.childForceExpandWidth = false;
            column.childForceExpandHeight = false;
            Swaps.Add(saved);
        }

        public static void Restore()
        {
            foreach (var saved in Swaps)
            {
                if (saved.Go == null) continue;
                var column = saved.Go.GetComponent<VerticalLayoutGroup>();
                if (column != null) Object.DestroyImmediate(column);
                var row = saved.Go.AddComponent<HorizontalLayoutGroup>();
                row.padding = saved.Padding;
                row.spacing = saved.Spacing;
                row.childAlignment = saved.Alignment;
                row.childControlWidth = saved.ControlWidth;
                row.childControlHeight = saved.ControlHeight;
                row.childForceExpandWidth = saved.ExpandWidth;
                row.childForceExpandHeight = saved.ExpandHeight;
                row.reverseArrangement = saved.Reverse;
            }
            Swaps.Clear();
            _applied = false;
        }
    }
}
