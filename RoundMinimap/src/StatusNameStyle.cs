using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace RoundMinimap
{
    /// <summary>
    /// Keeps long status effect names ("Boon of the Lox") in line with the others. The game draws a
    /// name too wide for its box in a smaller font, and since the text hangs from the top of the box,
    /// the smaller name sits higher than its neighbours. Moving it down by the difference in ascent
    /// puts its baseline where a full size name's would be. Full size names are left alone.
    ///
    /// The offset is worked out from the text as drawn: its own baseline and the size the game
    /// shrank it to. Ascent scales with font size, so the full size baseline follows from those.
    /// </summary>
    internal static class StatusNameStyle
    {
        private const string TimeTextName = "TimeText";
        private const float Tolerance = 0.1f;

        private static readonly AccessTools.FieldRef<Hud, List<RectTransform>> IconsRef =
            AccessTools.FieldRefAccess<Hud, List<RectTransform>>("m_statusEffects");

        private static Hud _hud;
        private static bool _captured;
        private static Vector2 _originalPosition;
        private static bool _anyMoved;
        private static readonly List<TMP_Text> _texts = new List<TMP_Text>();

        public static void Attach(Hud hud)
        {
            _hud = hud;
            _captured = false;
            _anyMoved = false;

            var name = FindName(hud.m_statusEffectTemplate);
            if (name == null)
            {
                Plugin.Log.LogWarning("Status effect template has no name text; 'Line up shrunk names' does nothing.");
                return;
            }
            _originalPosition = name.rectTransform.anchoredPosition;
            _captured = true;
        }

        /// <summary>Runs every frame: a name's size changes whenever its effect or text does.</summary>
        public static void Apply(Hud hud, bool lineUp)
        {
            if (!ReferenceEquals(hud, _hud) || !_captured) return;
            if (!lineUp && !_anyMoved) return;

            var icons = IconsRef(hud);
            if (icons == null) return;

            bool anyMoved = false;
            foreach (var icon in icons)
            {
                var name = FindName(icon);
                if (name == null) continue;
                float drop = lineUp ? BaselineDrop(name) : 0f;
                var rect = name.rectTransform;
                Vector2 wanted = _originalPosition - new Vector2(0f, drop);
                if ((rect.anchoredPosition - wanted).sqrMagnitude > Tolerance * Tolerance) rect.anchoredPosition = wanted;
                anyMoved |= drop > 0f;
            }
            _anyMoved = anyMoved;
        }

        public static void Restore()
        {
            if (_hud != null) Apply(_hud, false);
        }

        /// <summary>How far a shrunk name has to move down to share the full size baseline.</summary>
        private static float BaselineDrop(TMP_Text name)
        {
            if (!name.enableAutoSizing || !name.isActiveAndEnabled) return 0f;
            // A freshly created icon is not laid out until the canvas next rebuilds; do it now so a
            // new effect's name does not show at the raised position for a frame.
            if (name.havePropertiesChanged && !string.IsNullOrEmpty(name.text)
                && (name.textInfo == null || name.textInfo.characterCount == 0))
                name.ForceMeshUpdate();
            var info = name.textInfo;
            if (info == null || info.characterCount == 0 || info.lineCount == 0) return 0f;

            float drawnSize = info.characterInfo[0].pointSize;
            float fullSize = name.fontSizeMax;
            if (drawnSize <= 0f || drawnSize >= fullSize - 0.01f) return 0f;

            // Both are in the text's own space, so moving the box does not feed back into this.
            float ascent = name.rectTransform.rect.yMax - info.lineInfo[0].baseline;
            if (ascent <= 0f) return 0f;
            return ascent * (fullSize / drawnSize - 1f);
        }

        /// <summary>The name is the icon's text that is not the timer, which vanilla finds by name.</summary>
        private static TMP_Text FindName(RectTransform icon)
        {
            if (icon == null) return null;
            icon.GetComponentsInChildren(true, _texts);
            TMP_Text found = null;
            foreach (var text in _texts)
            {
                if (text.name == TimeTextName) continue;
                found = text;
                break;
            }
            _texts.Clear();
            return found;
        }
    }
}
