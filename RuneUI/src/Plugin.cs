using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace RuneUI
{
    [BepInPlugin(Guid, "Rune UI", PluginVersion.Value)]
    [BepInProcess("valheim.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "ithilias.runeui";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> ModEnabled;
        internal static ConfigEntry<KeyboardShortcut> DumpHudKey;

        internal static ConfigEntry<Color> PanelColor;
        internal static ConfigEntry<Color> BorderColor;
        internal static ConfigEntry<Color> AccentColor;
        internal static ConfigEntry<Color> TextColor;
        internal static ConfigEntry<Color> HealthColor;
        internal static ConfigEntry<Color> StaminaColor;
        internal static ConfigEntry<Color> EitrColor;

        internal static ConfigEntry<string> FontName;
        internal static ConfigEntry<float> FontScale;

        internal static ConfigEntry<float> CornerRadius;
        internal static ConfigEntry<float> BorderWidth;

        internal static ConfigEntry<float> HudScale;
        internal static ConfigEntry<bool> ReplaceBars;
        internal static ConfigEntry<HudAnchor> BarsAnchor;
        internal static ConfigEntry<float> BarsOffsetX;
        internal static ConfigEntry<float> BarsOffsetY;
        internal static ConfigEntry<float> BarsWidth;
        internal static ConfigEntry<bool> MoveHotbar;
        internal static ConfigEntry<bool> StyleHotbar;
        internal static ConfigEntry<HudAnchor> HotbarAnchor;
        internal static ConfigEntry<float> HotbarOffsetX;
        internal static ConfigEntry<float> HotbarOffsetY;
        internal static ConfigEntry<HudAnchor> FoodAnchor;
        internal static ConfigEntry<float> FoodOffsetX;
        internal static ConfigEntry<float> FoodOffsetY;

        internal static ConfigEntry<bool> QuickBarEnabled;
        internal static ConfigEntry<KeyCode> QuickBarModifier;
        internal static ConfigEntry<HudAnchor> QuickBarAnchor;
        internal static ConfigEntry<float> QuickBarOffsetX;
        internal static ConfigEntry<float> QuickBarOffsetY;

        internal static ConfigEntry<bool> PartyEnabled;
        internal static ConfigEntry<float> PartyRange;
        internal static ConfigEntry<int> PartyMaxPlayers;
        internal static ConfigEntry<bool> HidePlayerBars;
        internal static ConfigEntry<HudAnchor> PartyAnchor;
        internal static ConfigEntry<float> PartyOffsetX;
        internal static ConfigEntry<float> PartyOffsetY;
        internal static ConfigEntry<float> PartyWidth;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            ModEnabled = Config.Bind("1 - General", "Enabled", true,
                "Master switch. Turning this off puts the vanilla HUD back.");
            DumpHudKey = Config.Bind("1 - General", "Dump HUD key",
                new KeyboardShortcut(KeyCode.F8, KeyCode.LeftControl),
                "Logs the HUD's object tree to the BepInEx log. Useful for bug reports.");

            PanelColor = Config.Bind("2 - Colors", "Panel background", new Color(0.07f, 0.08f, 0.10f, 0.82f),
                "Fill colour of panels, bar backgrounds and slots.");
            BorderColor = Config.Bind("2 - Colors", "Panel border", new Color(0.78f, 0.64f, 0.38f, 0.9f),
                "Border colour of panels, bars and slots.");
            AccentColor = Config.Bind("2 - Colors", "Accent", new Color(0.95f, 0.78f, 0.42f, 1f),
                "Highlights such as the adrenaline bar and key labels.");
            TextColor = Config.Bind("2 - Colors", "Text", new Color(0.94f, 0.91f, 0.85f, 1f),
                "Colour of text drawn by this mod.");
            HealthColor = Config.Bind("2 - Colors", "Health bar", new Color(0.80f, 0.20f, 0.18f, 1f),
                "Health bar fill, also used in the party list.");
            StaminaColor = Config.Bind("2 - Colors", "Stamina bar", new Color(0.93f, 0.74f, 0.22f, 1f),
                "Stamina bar fill.");
            EitrColor = Config.Bind("2 - Colors", "Eitr bar", new Color(0.50f, 0.38f, 0.92f, 1f),
                "Eitr bar fill.");

            FontName = Config.Bind("3 - Font", "Font name", "",
                "Name of a TextMeshPro font loaded by the game, for example Norse SDF or AveriaSerifLibre-Bold SDF. " +
                "Empty uses the font of the vanilla health text.");
            FontScale = Config.Bind("3 - Font", "Font size scale", 1f,
                new ConfigDescription("Multiplies the size of text drawn by this mod.",
                    new AcceptableValueRange<float>(0.5f, 2f)));

            CornerRadius = Config.Bind("4 - Shape", "Corner radius", 5f,
                new ConfigDescription("Roundness of panels, bars and slots, in pixels.",
                    new AcceptableValueRange<float>(0f, 16f)));
            BorderWidth = Config.Bind("4 - Shape", "Border width", 1.5f,
                new ConfigDescription("Border thickness in pixels. 0 draws no border.",
                    new AcceptableValueRange<float>(0f, 6f)));

            HudScale = Config.Bind("5 - HUD layout", "HUD scale", 1f,
                new ConfigDescription("Size of the bars, hotbar, quick bar, food and party list.",
                    new AcceptableValueRange<float>(0.5f, 2f)));
            ReplaceBars = Config.Bind("5 - HUD layout", "Replace bars", true,
                "Hide the vanilla health, food, stamina, eitr and adrenaline displays and show this mod's bars and food row instead.");
            BarsAnchor = Config.Bind("5 - HUD layout", "Bars anchor", HudAnchor.Bottom,
                "Screen point the bars are placed relative to.");
            BarsOffsetX = Config.Bind("5 - HUD layout", "Bars offset X", 0f, "Horizontal offset from the anchor.");
            BarsOffsetY = Config.Bind("5 - HUD layout", "Bars offset Y", 172f, "Vertical offset from the anchor.");
            BarsWidth = Config.Bind("5 - HUD layout", "Bars width", 420f,
                new ConfigDescription("Width of the bars.", new AcceptableValueRange<float>(150f, 1000f)));
            MoveHotbar = Config.Bind("5 - HUD layout", "Move hotbar", true,
                "Move the vanilla hotbar to the position below.");
            StyleHotbar = Config.Bind("5 - HUD layout", "Style hotbar", true,
                "Draw hotbar and quick bar slots in the theme colours.");
            HotbarAnchor = Config.Bind("5 - HUD layout", "Hotbar anchor", HudAnchor.Bottom,
                "Screen point the hotbar is placed relative to.");
            HotbarOffsetX = Config.Bind("5 - HUD layout", "Hotbar offset X", 0f, "Horizontal offset from the anchor.");
            HotbarOffsetY = Config.Bind("5 - HUD layout", "Hotbar offset Y", 92f, "Vertical offset from the anchor.");
            FoodAnchor = Config.Bind("5 - HUD layout", "Food anchor", HudAnchor.Bottom,
                "Screen point the food row is placed relative to.");
            FoodOffsetX = Config.Bind("5 - HUD layout", "Food offset X", 380f, "Horizontal offset from the anchor.");
            FoodOffsetY = Config.Bind("5 - HUD layout", "Food offset Y", 16f, "Vertical offset from the anchor.");

            QuickBarEnabled = Config.Bind("6 - Quick bar 2", "Enabled", true,
                "Show the second inventory row as a quick bar and use its items with the modifier key plus 1 to 8.");
            QuickBarModifier = Config.Bind("6 - Quick bar 2", "Modifier key", KeyCode.LeftAlt,
                "Hold this and press 1 to 8 to use the item in that slot of the second row. " +
                "While it is held, 1 to 8 do not use the normal hotbar.");
            QuickBarAnchor = Config.Bind("6 - Quick bar 2", "Anchor", HudAnchor.Bottom,
                "Screen point the quick bar is placed relative to.");
            QuickBarOffsetX = Config.Bind("6 - Quick bar 2", "Offset X", 0f, "Horizontal offset from the anchor.");
            QuickBarOffsetY = Config.Bind("6 - Quick bar 2", "Offset Y", 16f, "Vertical offset from the anchor.");

            PartyEnabled = Config.Bind("7 - Party list", "Enabled", true,
                "List nearby players with their health.");
            PartyRange = Config.Bind("7 - Party list", "Range", 100f,
                new ConfigDescription("Only players within this many metres are listed.",
                    new AcceptableValueRange<float>(10f, 500f)));
            PartyMaxPlayers = Config.Bind("7 - Party list", "Max players", 10,
                new ConfigDescription("Most rows shown; the closest players win.",
                    new AcceptableValueRange<int>(1, 20)));
            HidePlayerBars = Config.Bind("7 - Party list", "Hide bars over players", true,
                "Hide the health bar floating over other players. Their names stay.");
            PartyAnchor = Config.Bind("7 - Party list", "Anchor", HudAnchor.TopLeft,
                "Screen point the party list is placed relative to.");
            PartyOffsetX = Config.Bind("7 - Party list", "Offset X", 20f, "Horizontal offset from the anchor.");
            PartyOffsetY = Config.Bind("7 - Party list", "Offset Y", -20f, "Vertical offset from the anchor.");
            PartyWidth = Config.Bind("7 - Party list", "Width", 220f,
                new ConfigDescription("Width of each row.", new AcceptableValueRange<float>(120f, 500f)));

            // Every change rebuilds what this mod drew, so colours, font and shape apply live.
            Config.SettingChanged += (_, __) => Theme.Invalidate();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Patches));

            Log.LogInfo("Rune UI loaded.");
        }

        private void Update()
        {
            if (Hud.instance == null || !DumpHudKey.Value.IsDown()) return;
            Log.LogInfo(UiTools.DumpHierarchy(Hud.instance.transform));
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            Theme.DestroySprites();
        }
    }
}
