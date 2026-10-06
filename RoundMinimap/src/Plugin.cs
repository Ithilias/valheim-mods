using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace RoundMinimap
{
    internal enum RotationMode
    {
        /// <summary>Follow the camera, i.e. where the player is looking.</summary>
        PlayerLook,
        /// <summary>Follow the character's body facing instead.</summary>
        PlayerBody,
    }

    [BepInPlugin(Guid, "Round Minimap", PluginVersion.Value)]
    [BepInProcess("valheim.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "ithilias.roundminimap";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> ModEnabled;
        internal static ConfigEntry<bool> RotateMinimap;
        internal static ConfigEntry<RotationMode> RotationSource;
        internal static ConfigEntry<float> RotationSmoothing;
        internal static ConfigEntry<bool> FixPlayerMarker;
        internal static ConfigEntry<bool> KeepPinsUpright;
        internal static ConfigEntry<bool> RotateOtherModIcons;
        internal static ConfigEntry<float> MapSizeMultiplier;
        internal static ConfigEntry<bool> GrowTowardsCentre;
        internal static ConfigEntry<float> MapOffsetX;
        internal static ConfigEntry<float> MapOffsetY;
        internal static ConfigEntry<bool> MakeRoomForStatusEffects;
        internal static ConfigEntry<bool> StatusWrapRows;
        internal static ConfigEntry<int> StatusMinPerRow;
        internal static ConfigEntry<int> StatusIconsPerRow;
        internal static ConfigEntry<float> StatusIconSpacing;
        internal static ConfigEntry<bool> StatusLineUpNames;
        internal static ConfigEntry<float> StatusRowSpacing;
        internal static ConfigEntry<bool> StatusAvoidValheimPlusClock;
        internal static ConfigEntry<bool> StatusAvoidHotbars;
        internal static ConfigEntry<float> StatusEdgeMargin;
        internal static ConfigEntry<float> StatusOffsetX;
        internal static ConfigEntry<float> StatusOffsetY;
        internal static ConfigEntry<Color> BiomeBackgroundColor;
        internal static ConfigEntry<float> BiomePaddingX;
        internal static ConfigEntry<float> BiomePaddingY;
        internal static ConfigEntry<float> BiomeCornerRadius;
        internal static ConfigEntry<float> BiomeBorderThickness;
        internal static ConfigEntry<Color> BiomeBorderColor;
        internal static ConfigEntry<float> ZoomLevel;
        internal static ConfigEntry<float> ZoomStep;
        internal static ConfigEntry<KeyboardShortcut> ZoomInKey;
        internal static ConfigEntry<KeyboardShortcut> ZoomOutKey;
        internal static ConfigEntry<bool> RoundMinimap;
        internal static ConfigEntry<float> CircleScale;
        internal static ConfigEntry<int> CircleSegments;
        internal static ConfigEntry<float> BorderThickness;
        internal static ConfigEntry<float> BorderShadow;
        internal static ConfigEntry<float> BorderShadowStrength;
        internal static ConfigEntry<Color> BorderColor;
        internal static ConfigEntry<bool> HideSquareBackground;
        internal static ConfigEntry<bool> CenterBiomeLabel;
        internal static ConfigEntry<float> BiomeLabelGap;
        internal static ConfigEntry<bool> PullWindMarkerIn;
        internal static ConfigEntry<float> WindMarkerDistance;
        internal static ConfigEntry<string> HiddenElements;
        internal static ConfigEntry<CompassMode> CompassModeSetting;
        internal static ConfigEntry<string> CompassLetters;
        internal static ConfigEntry<float> CompassDistance;
        internal static ConfigEntry<float> CompassFontSize;
        internal static ConfigEntry<Color> CompassColor;
        internal static ConfigEntry<KeyboardShortcut> DumpHierarchyKey;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            ModEnabled = Config.Bind("1 - General", "Enabled", true,
                "Master switch. Turning this off restores the vanilla minimap without a restart.");

            RotateMinimap = Config.Bind("2 - Rotation", "Rotate minimap", true,
                "Rotate the small minimap so the direction you are facing points up. Off keeps north at the " +
                "top. Round map only: the square map always shows north up.");
            RotationSource = Config.Bind("2 - Rotation", "Rotation follows", RotationMode.PlayerLook,
                "PlayerLook follows the camera (where you are looking). PlayerBody follows your character's facing.");
            RotationSmoothing = Config.Bind("2 - Rotation", "Smoothing", 0.08f,
                new ConfigDescription(
                    "Seconds of easing applied to map rotation. 0 snaps instantly; higher values lag behind the camera.",
                    new AcceptableValueRange<float>(0f, 1f)));
            FixPlayerMarker = Config.Bind("2 - Rotation", "Fix player arrow", true,
                "Keep the player arrow pointing straight up (the map turns instead). " +
                "Turn off to have the arrow show your look direction relative to the map.");
            RotateOtherModIcons = Config.Bind("2 - Rotation", "Turn icons from other mods", true,
                "Also turn icons that other mods draw on the small map themselves, and hide them past " +
                "the round edge, such as HUDCompass's cart, ship and portal markers. Without this they " +
                "stay where a north-up map would have them.");
            KeepPinsUpright = Config.Bind("2 - Rotation", "Keep pin icons upright", true,
                "Pin positions always rotate with the map; this also forces the icons themselves to stay level.");

            MapSizeMultiplier = Config.Bind("3 - Shape", "Map size multiplier", 1f,
                new ConfigDescription("Scales the whole small map. 1 is the vanilla size. This magnifies the " +
                    "map rather than revealing more of the world - use zoom for that. Round map only.",
                    new AcceptableValueRange<float>(0.5f, 3f)));
            GrowTowardsCentre = Config.Bind("3 - Shape", "Grow towards screen centre", true,
                "When the map is made bigger, grow it towards the middle of the screen so it stays clear " +
                "of the screen edge. Off grows it equally in every direction, which pushes a corner map " +
                "off the edge. Round map only.");

            ZoomLevel = Config.Bind("6 - Zoom", "Zoom level", 0.01f,
                new ConfigDescription("Remembered zoom, updated whenever you zoom by any means. Smaller is " +
                    "closer in. The game clamps this to its own limits.",
                    new AcceptableValueRange<float>(0.001f, 1f)));
            ZoomStep = Config.Bind("6 - Zoom", "Zoom step", 1.3f,
                new ConfigDescription("How much one press of a zoom key multiplies or divides the zoom by.",
                    new AcceptableValueRange<float>(1.01f, 3f)));
            ZoomInKey = Config.Bind("6 - Zoom", "Zoom in key", new KeyboardShortcut(KeyCode.KeypadPlus),
                "Zooms the small map in. The game's own MapZoomIn binding keeps working too.");
            ZoomOutKey = Config.Bind("6 - Zoom", "Zoom out key", new KeyboardShortcut(KeyCode.KeypadMinus),
                "Zooms the small map out. The game's own MapZoomOut binding keeps working too.");

            RoundMinimap = Config.Bind("3 - Shape", "Round minimap", true,
                "Draw the small minimap as a circle. Off gives you the vanilla square map, which does not " +
                "rotate or resize, and most of this mod's other settings only apply to the round map.");
            CircleScale = Config.Bind("3 - Shape", "Circle scale", 1f,
                new ConfigDescription(
                    "Circle diameter as a fraction of the minimap's shortest side. 1 uses the full width. Round map only.",
                    new AcceptableValueRange<float>(0.25f, 1f)));
            CircleSegments = Config.Bind("3 - Shape", "Circle segments", 128,
                new ConfigDescription("Number of segments in the circle's mesh. Higher is smoother. Round map only.",
                    new AcceptableValueRange<int>(12, 512)));
            BorderThickness = Config.Bind("3 - Shape", "Border thickness", 3f,
                new ConfigDescription("Thickness in pixels of the ring drawn over the circle's edge. 0 draws none. Round map only.",
                    new AcceptableValueRange<float>(0f, 24f)));
            BorderShadow = Config.Bind("3 - Shape", "Border shadow", 5f,
                new ConfigDescription("Width in pixels of the soft shadow fading outwards from the edge. 0 draws none. Round map only.",
                    new AcceptableValueRange<float>(0f, 32f)));
            BorderShadowStrength = Config.Bind("3 - Shape", "Border shadow strength", 0.5f,
                new ConfigDescription("Peak opacity of that shadow, relative to the border colour's alpha. Round map only.",
                    new AcceptableValueRange<float>(0f, 1f)));
            BorderColor = Config.Bind("3 - Shape", "Border color", new Color(0.05f, 0.04f, 0.03f, 0.9f),
                "Colour of the rim and its shadow. Round map only.");

            HideSquareBackground = Config.Bind("4 - Layout", "Hide square background", true,
                "Switch off the graphics of any panel larger than the circle, so the vanilla square " +
                "backing and frame stop sticking out past the round edge. Only the panel's own graphic " +
                "is hidden, so anything sitting on it still shows. Round map only.");
            CenterBiomeLabel = Config.Bind("4 - Layout", "Center biome label below map", true,
                "Move the biome name to sit centred under the circle instead of overlapping its corner. Round map only.");
            BiomeLabelGap = Config.Bind("4 - Layout", "Biome label gap", 8f,
                new ConfigDescription("Pixels between the circle's edge and the biome label. Round map only.",
                    new AcceptableValueRange<float>(-40f, 60f)));
            PullWindMarkerIn = Config.Bind("4 - Layout", "Pull wind marker inside circle", true,
                "Keep the wind arrow in the corner vanilla chose, but move it in along that direction " +
                "until it sits inside the circle. Round map only.");
            WindMarkerDistance = Config.Bind("4 - Layout", "Wind marker distance", 0.8f,
                new ConfigDescription("How far out the wind arrow sits, as a fraction of the circle's radius. Round map only.",
                    new AcceptableValueRange<float>(0f, 1f)));
            MapOffsetX = Config.Bind("4 - Layout", "Map offset X", 0f,
                new ConfigDescription("Moves the whole small map sideways, in pixels. Negative moves it " +
                    "left, positive moves it right.",
                    new AcceptableValueRange<float>(-2000f, 2000f)));
            MapOffsetY = Config.Bind("4 - Layout", "Map offset Y", 0f,
                new ConfigDescription("Moves the whole small map up or down, in pixels. Negative moves it " +
                    "down, positive moves it up.",
                    new AcceptableValueRange<float>(-2000f, 2000f)));
            HiddenElements = Config.Bind("4 - Layout", "Hidden elements", "",
                "Comma-separated GameObject names under the small map root to hide outright, for anything " +
                "'Hide square background' misses. Use the hierarchy dump key to find names.");

            MakeRoomForStatusEffects = Config.Bind("8 - Status effects", "Make room for status effects", true,
                "When a bigger or moved map reaches the status effect icons (Rested, Wet, Cold and so on), " +
                "move the icons aside so they keep the gap vanilla leaves. They move back when the map no " +
                "longer reaches them. Off leaves them where the game puts them, apart from the offset below.");
            StatusWrapRows = Config.Bind("8 - Status effects", "Wrap rows to fit", true,
                "If moving the icons aside would push them off the screen or into a hotbar, put fewer icons " +
                "in each row so the list grows downwards instead.");
            StatusMinPerRow = Config.Bind("8 - Status effects", "Minimum icons per row", 3,
                new ConfigDescription("The fewest icons a row is cut down to when wrapping. If even that does " +
                    "not fit, the icons still move clear of the map.",
                    new AcceptableValueRange<int>(1, 20)));
            StatusIconsPerRow = Config.Bind("8 - Status effects", "Icons per row", 0,
                new ConfigDescription("How many icons go in a row before the next row starts below. 0 uses the " +
                    "game's own value. Wrapping to fit can still make rows shorter than this.",
                    new AcceptableValueRange<int>(0, 20)));
            StatusIconSpacing = Config.Bind("8 - Status effects", "Icon spacing", 0f,
                new ConfigDescription("Distance from one icon to the next. 0 uses the game's own value.",
                    new AcceptableValueRange<float>(0f, 300f)));
            StatusRowSpacing = Config.Bind("8 - Status effects", "Row spacing", 0f,
                new ConfigDescription("Distance from one row to the next. 0 uses the same as Icon spacing, " +
                    "like the game does.",
                    new AcceptableValueRange<float>(0f, 300f)));
            StatusLineUpNames = Config.Bind("8 - Status effects", "Line up shrunk names", true,
                "The game draws a name too long for its box, such as Boon of the Lox, in a smaller font that " +
                "hangs higher than the others. This moves it down so it sits on the same baseline as its " +
                "neighbours. Full size names are not moved.");
            StatusAvoidHotbars = Config.Bind("8 - Status effects", "Avoid hotbars", true,
                "Treat the hotbar, and extra bars from mods such as EquipmentAndQuickSlots, as something the " +
                "icons must not be pushed into.");
            StatusAvoidValheimPlusClock = Config.Bind("8 - Status effects", "Avoid ValheimPlus clock", true,
                "Treat the day and time text ValheimPlus shows at the top of the screen as something the " +
                "icons must not be pushed into. Does nothing without ValheimPlus.");
            StatusEdgeMargin = Config.Bind("8 - Status effects", "Edge margin", 8f,
                new ConfigDescription("Pixels kept free between the icons and the screen edge, a hotbar or the ValheimPlus clock.",
                    new AcceptableValueRange<float>(0f, 200f)));
            StatusOffsetX = Config.Bind("8 - Status effects", "Offset X", 0f,
                new ConfigDescription("Moves the status effect icons sideways, in pixels, for HUD layouts the " +
                    "automatic placement does not know about. Negative moves them left.",
                    new AcceptableValueRange<float>(-2000f, 2000f)));
            StatusOffsetY = Config.Bind("8 - Status effects", "Offset Y", 0f,
                new ConfigDescription("Moves the status effect icons up or down, in pixels. Negative moves them down.",
                    new AcceptableValueRange<float>(-2000f, 2000f)));

            CompassModeSetting = Config.Bind("5 - Compass", "Compass", CompassMode.Cardinal,
                "Cardinal letters drawn around the edge of the circle, showing which way is north as the " +
                "map turns. NorthOnly draws just the north marker. Round map only.");
            CompassLetters = Config.Bind("5 - Compass", "Compass letters", "N,E,S,W",
                "Letters used for north, east, south and west, in that order.");
            CompassDistance = Config.Bind("5 - Compass", "Compass distance", 0.86f,
                new ConfigDescription("How far out the letters sit, as a fraction of the circle's radius.",
                    new AcceptableValueRange<float>(0.1f, 1.5f)));
            CompassFontSize = Config.Bind("5 - Compass", "Compass font size", 14f,
                new ConfigDescription("Font size of the compass letters.",
                    new AcceptableValueRange<float>(6f, 40f)));
            CompassColor = Config.Bind("5 - Compass", "Compass color", new Color(0.96f, 0.93f, 0.85f, 0.9f),
                "Colour of the compass letters.");

            BiomeBackgroundColor = Config.Bind("7 - Biome label", "Background color", new Color(0f, 0f, 0f, 0f),
                "Colour of a box drawn behind the biome name, to keep it readable over bright ground. " +
                "The last two hex digits are the opacity: 00, the default, draws no box. 000000AA is a " +
                "dark see-through box.");
            BiomePaddingX = Config.Bind("7 - Biome label", "Padding horizontal", 8f,
                new ConfigDescription("Pixels of box to the left and right of the biome name.",
                    new AcceptableValueRange<float>(0f, 40f)));
            BiomePaddingY = Config.Bind("7 - Biome label", "Padding vertical", 3f,
                new ConfigDescription("Pixels of box above and below the biome name.",
                    new AcceptableValueRange<float>(0f, 40f)));
            BiomeCornerRadius = Config.Bind("7 - Biome label", "Corner radius", 4f,
                new ConfigDescription("Roundness of the box's corners, in pixels. 0 is square.",
                    new AcceptableValueRange<float>(0f, 20f)));
            BiomeBorderThickness = Config.Bind("7 - Biome label", "Border thickness", 0f,
                new ConfigDescription("Thickness in pixels of a border around the box. 0 draws none. " +
                    "Works without a background colour too, for just an outline.",
                    new AcceptableValueRange<float>(0f, 8f)));
            BiomeBorderColor = Config.Bind("7 - Biome label", "Border color", new Color(0.96f, 0.93f, 0.85f, 0.8f),
                "Colour of that border.");

            DumpHierarchyKey = Config.Bind("4 - Debug", "Dump hierarchy key",
                new KeyboardShortcut(KeyCode.F9, KeyCode.LeftControl),
                "Logs the small minimap's UI hierarchy to the BepInEx log, with rect and component details.");

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Patches));

            Log.LogInfo("Round Minimap loaded.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
