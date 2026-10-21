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
        /// <summary>The runic looking header lettering.</summary>
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

        /// <summary>The game's font asset for <paramref name="font"/>, or null if it is not loaded.</summary>
        public static TMP_FontAsset Find(HudFont font)
        {
            if (font == HudFont.Default || !AssetNames.TryGetValue(font, out string name)) return null;
            if (_fonts.TryGetValue(font, out var cached) && cached != null) return cached;

            foreach (var asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (asset != null && asset.name == name)
                {
                    _fonts[font] = asset;
                    _missing.Remove(font);
                    return asset;
                }
            }

            if (_missing.Add(font))
            {
                var loaded = new List<string>();
                foreach (var asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                    if (asset != null) loaded.Add(asset.name);
                Plugin.Log.LogWarning($"Font '{name}' is not loaded; keeping the default. Loaded fonts: {string.Join(", ", loaded)}.");
            }
            return null;
        }

        /// <summary>
        /// Gives <paramref name="text"/> the chosen font, or the original one for Default or when the
        /// chosen font is missing. Does nothing when the text already has it.
        /// </summary>
        public static void Apply(TMP_Text text, HudFont font, TMP_FontAsset originalFont, Material originalMaterial)
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
            if (originalMaterial != null && originalMaterial.name.StartsWith(originalFont.name))
                suffix = originalMaterial.name.Substring(originalFont.name.Length);
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
    /// The biome label's font. The original font and material are kept so Default, and turning the
    /// mod off, can put them back, and so the compass letters cloned from the label can start from
    /// the original rather than whatever the label shows now.
    /// </summary>
    internal static class BiomeLabelFont
    {
        private static TMP_Text _label;

        public static TMP_FontAsset OriginalFont { get; private set; }
        public static Material OriginalMaterial { get; private set; }

        public static void Apply(Minimap map)
        {
            var label = map.m_biomeNameSmall;
            if (label == null) return;
            if (label != _label)
            {
                _label = label;
                OriginalFont = label.font;
                OriginalMaterial = label.fontSharedMaterial;
            }
            HudFonts.Apply(label, Plugin.BiomeFont.Value, OriginalFont, OriginalMaterial);
        }

        public static void Restore()
        {
            if (_label != null) HudFonts.Apply(_label, HudFont.Default, OriginalFont, OriginalMaterial);
        }

        /// <summary>Drops references after the minimap HUD has been destroyed and rebuilt.</summary>
        public static void ResetState()
        {
            _label = null;
            OriginalFont = null;
            OriginalMaterial = null;
        }
    }
}
