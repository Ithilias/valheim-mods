using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RoundMinimap
{
    /// <summary>Fonts the game ships, offered for the biome label and the compass letters.</summary>
    internal enum HudFont
    {
        /// <summary>Whatever the game uses for the biome label.</summary>
        Default,
        /// <summary>The runic-looking header lettering.</summary>
        Norse,
        NorseBold,
        AveriaSerif,
        AveriaSans,
    }

    /// <summary>
    /// Finds the game's TMP font assets by name and switches texts between them. The game never
    /// references its fonts from code, only from UI objects, so they are looked up among the loaded
    /// assets. A text keeps its look (outline and so on) by moving to the new font's material preset
    /// with the same name suffix, e.g. "Valheim-Norse - Outline" for "Valheim-AveriaSerifLibre - Outline".
    /// </summary>
    internal static class HudFonts
    {
        private static readonly Dictionary<HudFont, string> AssetNames = new Dictionary<HudFont, string>
        {
            { HudFont.Norse, "Valheim-Norse" },
            { HudFont.NorseBold, "Valheim-Norsebold" },
            { HudFont.AveriaSerif, "Valheim-AveriaSerifLibre" },
            { HudFont.AveriaSans, "Valheim-AveriaSansLibre" },
        };

        private static readonly Dictionary<HudFont, TMP_FontAsset> _fonts = new Dictionary<HudFont, TMP_FontAsset>();
        private static readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private static readonly HashSet<HudFont> _missing = new HashSet<HudFont>();

        /// <summary>
        /// The game's font asset for <paramref name="font"/>, or null if it is not loaded. A font found
        /// missing is not searched for again until the HUD is rebuilt: the search scans every loaded
        /// object.
        /// </summary>
        public static TMP_FontAsset Find(HudFont font)
        {
            if (font == HudFont.Default || !AssetNames.TryGetValue(font, out string name)) return null;
            if (_fonts.TryGetValue(font, out var cached) && cached != null) return cached;
            if (_missing.Contains(font)) return null;

            foreach (var asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (asset != null && asset.name == name)
                {
                    _fonts[font] = asset;
                    _missing.Remove(font);
                    return asset;
                }
            }

            _missing.Add(font);
            var loaded = new List<string>();
            foreach (var asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (asset != null) loaded.Add(asset.name);
            Plugin.Log.LogWarning($"Font '{name}' is not loaded; keeping the current one. Loaded fonts: {string.Join(", ", loaded)}.");
            return null;
        }

        /// <summary>Lets missing fonts be searched for again, after the HUD has been rebuilt.</summary>
        public static void ResetState() => _missing.Clear();

        /// <summary>
        /// Gives <paramref name="text"/> the chosen font, or the original one for Default or when the
        /// chosen font is missing. Meant for one-off changes; it looks up names, so not every frame.
        /// </summary>
        public static void Set(TMP_Text text, HudFont font, TMP_FontAsset originalFont, Material originalMaterial)
        {
            if (text == null || originalFont == null) return;

            TMP_FontAsset target = Find(font) ?? originalFont;
            Material material = target == originalFont
                ? originalMaterial
                : MatchingMaterial(target, originalFont, originalMaterial);

            if (text.font != target) text.font = target;
            if (material != null && text.fontSharedMaterial != material) text.fontSharedMaterial = material;
        }

        /// <summary>The preset of <paramref name="target"/> styled like <paramref name="originalMaterial"/>.</summary>
        private static Material MatchingMaterial(TMP_FontAsset target, TMP_FontAsset originalFont, Material originalMaterial)
        {
            string suffix = string.Empty;
            if (originalMaterial != null)
            {
                // A material another mod instanced is named after its source plus " (Instance)".
                string source = originalMaterial.name;
                if (source.EndsWith(" (Instance)")) source = source.Substring(0, source.Length - " (Instance)".Length);
                if (source.StartsWith(originalFont.name)) suffix = source.Substring(originalFont.name.Length);
            }
            if (suffix.Length == 0) return target.material;

            string name = target.name + suffix;
            if (_materials.TryGetValue(name, out var cached) && cached != null) return cached;

            foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (material != null && material.name == name)
                {
                    _materials[name] = material;
                    return material;
                }
            }
            // No preset with that style: the font's own material at least renders correctly.
            _materials[name] = target.material;
            return target.material;
        }
    }

    /// <summary>
    /// The biome label's font and font size. The mod only touches them when a setting changes: with
    /// Default and 0 it leaves the label alone, so other mods that set fonts are not fought every
    /// frame. The font the label had when the mod first changed it is kept, so Default and turning
    /// the mod off can put it back, and so the compass letters cloned from the label can start from
    /// it. The same goes for the size.
    /// </summary>
    internal static class BiomeLabelFont
    {
        private static TMP_Text _label;
        private static HudFont _handled;
        private static bool _applied;
        private static float _handledSize;
        private static float _originalSize;
        private static bool _sizeApplied;

        /// <summary>The label's font before the mod changed it, or null while it has not.</summary>
        public static TMP_FontAsset OriginalFont { get; private set; }
        public static Material OriginalMaterial { get; private set; }

        public static void Apply(Minimap map)
        {
            var label = map.m_biomeNameSmall;
            if (label == null) return;
            HudFont wanted = Plugin.BiomeFont.Value;
            float wantedSize = Mathf.Max(0f, Plugin.BiomeFontSize.Value);
            if (label == _label && wanted == _handled && wantedSize == _handledSize) return;

            if (label != _label)
            {
                _label = label;
                _applied = false;
                _sizeApplied = false;
                OriginalFont = null;
                OriginalMaterial = null;
                // Forces both to be looked at for the new label.
                _handled = (HudFont)(-1);
                _handledSize = -1f;
            }

            if (wantedSize != _handledSize)
            {
                _handledSize = wantedSize;
                ApplySize(label, wantedSize);
            }

            if (wanted == _handled) return;
            _handled = wanted;

            if (wanted == HudFont.Default)
            {
                RestoreFont();
                return;
            }

            if (!_applied)
            {
                OriginalFont = label.font;
                OriginalMaterial = label.fontSharedMaterial;
            }
            HudFonts.Set(label, wanted, OriginalFont, OriginalMaterial);
            _applied = true;
            // The label's box is sized from the rendered text later this frame.
            label.ForceMeshUpdate();
        }

        private static void ApplySize(TMP_Text label, float size)
        {
            if (size <= 0f)
            {
                RestoreSize();
            }
            else
            {
                if (!_sizeApplied) _originalSize = label.fontSize;
                _sizeApplied = true;
                if (label.fontSize != size) label.fontSize = size;
            }
            label.ForceMeshUpdate();
        }

        /// <summary>Puts the original font and size back if the mod changed them.</summary>
        public static void Restore()
        {
            RestoreFont();
            RestoreSize();
            // Applied again from scratch if the mod is switched back on.
            _handled = (HudFont)(-1);
            _handledSize = -1f;
        }

        private static void RestoreFont()
        {
            if (_applied && _label != null) HudFonts.Set(_label, HudFont.Default, OriginalFont, OriginalMaterial);
            _applied = false;
            OriginalFont = null;
            OriginalMaterial = null;
        }

        private static void RestoreSize()
        {
            if (_sizeApplied && _label != null) _label.fontSize = _originalSize;
            _sizeApplied = false;
        }

        /// <summary>Drops references after the minimap HUD has been destroyed and rebuilt.</summary>
        public static void ResetState()
        {
            _label = null;
            _applied = false;
            _sizeApplied = false;
            OriginalFont = null;
            OriginalMaterial = null;
        }
    }
}
