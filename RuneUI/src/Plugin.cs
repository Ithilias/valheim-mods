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
        internal static ConfigEntry<Color> StaggerColor;
        internal static ConfigEntry<Color> Quality1Color;
        internal static ConfigEntry<Color> Quality2Color;
        internal static ConfigEntry<Color> Quality3Color;
        internal static ConfigEntry<Color> Quality4Color;
        internal static ConfigEntry<Color> QualityAboveMaxColor;

        internal static ConfigEntry<string> FontName;
        internal static ConfigEntry<float> FontScale;

        internal static ConfigEntry<float> CornerRadius;
        internal static ConfigEntry<float> BorderWidth;
        internal static ConfigEntry<float> BarShading;
        internal static ConfigEntry<bool> QualityRings;
        internal static ConfigEntry<float> QualityRingWidth;

        internal static ConfigEntry<float> HudScale;
        internal static ConfigEntry<bool> ReplaceBars;
        internal static ConfigEntry<HudAnchor> BarsAnchor;
        internal static ConfigEntry<float> BarsOffsetX;
        internal static ConfigEntry<float> BarsOffsetY;
        internal static ConfigEntry<float> BarsWidth;
        internal static ConfigEntry<bool> StackBars;
        internal static ConfigEntry<float> StackGap;
        internal static ConfigEntry<bool> StackKeyHints;
        internal static ConfigEntry<float> KeyHintsSpacing;
        internal static ConfigEntry<bool> MoveHotbar;
        internal static ConfigEntry<bool> StyleHotbar;
        internal static ConfigEntry<bool> ShowEmptySlots;
        internal static ConfigEntry<HudAnchor> HotbarAnchor;
        internal static ConfigEntry<float> HotbarOffsetX;
        internal static ConfigEntry<float> HotbarOffsetY;
        internal static ConfigEntry<float> HotbarScale;
        internal static ConfigEntry<bool> PowerSlot;
        internal static ConfigEntry<bool> UnifyBuffs;
        internal static ConfigEntry<HudAnchor> BuffsAnchor;
        internal static ConfigEntry<float> BuffsOffsetX;
        internal static ConfigEntry<float> BuffsOffsetY;
        internal static ConfigEntry<int> BuffsPerRow;

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

        internal static ConfigEntry<bool> QuickSlotsEnabled;
        internal static ConfigEntry<int> QuickSlotCount;
        internal static readonly ConfigEntry<KeyboardShortcut>[] QuickSlotKeys = new ConfigEntry<KeyboardShortcut>[QuickSlots.MaxSize];
        internal static readonly ConfigEntry<string>[] QuickSlotLabels = new ConfigEntry<string>[QuickSlots.MaxSize];
        internal static ConfigEntry<float> QuickInventoryOffsetX;
        internal static ConfigEntry<float> QuickInventoryOffsetY;

        internal static ConfigEntry<bool> GearSlotsEnabled;

        internal static ConfigEntry<bool> KeepGearOnDeath;
        internal static ConfigEntry<bool> KeepQuickOnDeath;
        internal static ConfigEntry<bool> ReequipArmour;
        internal static ConfigEntry<bool> ReequipWeapons;

        internal static ConfigEntry<int> UtilitySlots;
        internal static ConfigEntry<int> ExtraInventoryRows;
        internal static ConfigEntry<float> BaseCarryWeight;

        internal static ConfigEntry<bool> SkillToastsEnabled;
        internal static ConfigEntry<float> SkillToastsDuration;
        internal static ConfigEntry<int> SkillToastsMax;
        internal static ConfigEntry<HudAnchor> SkillToastsAnchor;
        internal static ConfigEntry<float> SkillToastsOffsetX;
        internal static ConfigEntry<float> SkillToastsOffsetY;

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
            StaggerColor = Config.Bind("2 - Colors", "Stagger bar", new Color(0.85f, 0.87f, 0.92f, 1f),
                "Stagger bar fill. The bar shows while you are taking hits and fills up as you get close to being staggered.");
            Quality1Color = Config.Bind("2 - Colors", "Quality 1", new Color(0.62f, 0.62f, 0.62f, 1f),
                "Ring around upgradable items at quality 1.");
            Quality2Color = Config.Bind("2 - Colors", "Quality 2", new Color(0.30f, 0.80f, 0.30f, 1f),
                "Ring around items at quality 2.");
            Quality3Color = Config.Bind("2 - Colors", "Quality 3", new Color(0.25f, 0.55f, 1f, 1f),
                "Ring around items at quality 3.");
            Quality4Color = Config.Bind("2 - Colors", "Quality 4", new Color(1f, 0.78f, 0.15f, 1f),
                "Ring around items at quality 4.");
            QualityAboveMaxColor = Config.Bind("2 - Colors", "Quality above max", new Color(0.92f, 0.18f, 0.15f, 1f),
                "Ring around items upgraded past their normal maximum, shown with the quality number.");

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
            BarShading = Config.Bind("4 - Shape", "Bar shading", 0.5f,
                new ConfigDescription("Fades bars from light at the top to dark at the bottom for a 3D look. 0 keeps them flat.",
                    new AcceptableValueRange<float>(0f, 1f)));
            QualityRings = Config.Bind("4 - Shape", "Quality rings", true,
                "Draw a ring in the quality colour around upgradable items in the hotbars, inventory and containers.");
            QualityRingWidth = Config.Bind("4 - Shape", "Quality ring width", 2.5f,
                new ConfigDescription("Thickness of the quality ring in pixels.",
                    new AcceptableValueRange<float>(1f, 8f)));

            HudScale = Config.Bind("5 - HUD layout", "HUD scale", 1f,
                new ConfigDescription("Size of the bars, hotbar, quick bar, food and party list.",
                    new AcceptableValueRange<float>(0.5f, 2f)));
            ReplaceBars = Config.Bind("5 - HUD layout", "Replace bars", true,
                "Hide the vanilla health, food, stamina, eitr, adrenaline and stagger displays and show this mod's bars instead. Eaten food then shows with the buffs.");
            BarsAnchor = Config.Bind("5 - HUD layout", "Bars anchor", HudAnchor.Bottom,
                "Screen point the bars are placed relative to.");
            BarsOffsetX = BindOffset("5 - HUD layout", "Bars offset X", 0f, StackedOffset("Horizontal"));
            BarsOffsetY = BindOffset("5 - HUD layout", "Bars offset Y", 0f, StackedOffset("Vertical"));
            BarsWidth = Config.Bind("5 - HUD layout", "Bars width", 420f,
                new ConfigDescription("Width of the bars.", new AcceptableValueRange<float>(150f, 1000f)));
            StackBars = Config.Bind("5 - HUD layout", "Stack bars", true,
                "Stack the quick bar under the hotbar and the bars on top of it, so they never overlap. The hotbar's " +
                "anchor and offsets then move all three, and the quick bar and bars offsets only nudge each one from " +
                "its stacked spot. Needs Move hotbar.");
            StackGap = Config.Bind("5 - HUD layout", "Stack gap", 6f,
                new ConfigDescription("Space between stacked blocks.", new AcceptableValueRange<float>(0f, 50f)));
            StackKeyHints = Config.Bind("5 - HUD layout", "Stack key hints", true,
                "Show the key hints in the bottom right as a column instead of a long row.");
            KeyHintsSpacing = Config.Bind("5 - HUD layout", "Key hints spacing", 4f,
                new ConfigDescription("Space between stacked key hints.", new AcceptableValueRange<float>(0f, 40f)));
            MoveHotbar = Config.Bind("5 - HUD layout", "Move hotbar", true,
                "Move the vanilla hotbar to the position below.");
            StyleHotbar = Config.Bind("5 - HUD layout", "Style hotbar", true,
                "Draw hotbar and quick bar slots in the theme colours.");
            ShowEmptySlots = Config.Bind("5 - HUD layout", "Show empty hotbar slots", true,
                "Always show all eight hotbar slots, like the quick bar. Vanilla only shows them up to the last " +
                "item, and none after a death empties your inventory.");
            HotbarAnchor = Config.Bind("5 - HUD layout", "Hotbar anchor", HudAnchor.Bottom,
                "Screen point the hotbar is placed relative to.");
            HotbarOffsetX = BindOffset("5 - HUD layout", "Hotbar offset X", 0f, "Horizontal offset from the anchor.");
            HotbarOffsetY = BindOffset("5 - HUD layout", "Hotbar offset Y", 86f, "Vertical offset from the anchor.");
            HotbarScale = Config.Bind("5 - HUD layout", "Hotbar scale", 1f,
                new ConfigDescription("Size of the hotbar and quick bar, on top of HUD scale. They shrink and grow " +
                    "around the hotbar anchor, so the slots stay together. The hotbar's size needs Move hotbar.",
                    new AcceptableValueRange<float>(0.5f, 2f)));
            PowerSlot = Config.Bind("5 - HUD layout", "Forsaken power slot", true,
                "Show your forsaken power as a slot right of the hotbar, with its key and cooldown, instead of the vanilla display.");
            UnifyBuffs = Config.Bind("5 - HUD layout", "Unify buffs", true,
                "Show eaten food and status effects together as hotbar sized icons, instead of the vanilla displays.");
            BuffsAnchor = Config.Bind("5 - HUD layout", "Buffs anchor", HudAnchor.BottomLeft,
                "Screen point the buffs are placed relative to. They fill rows away from it.");
            BuffsOffsetX = BindOffset("5 - HUD layout", "Buffs offset X", 20f, "Horizontal offset from the anchor.");
            BuffsOffsetY = BindOffset("5 - HUD layout", "Buffs offset Y", 20f, "Vertical offset from the anchor.");
            BuffsPerRow = Config.Bind("5 - HUD layout", "Buffs per row", 6,
                new ConfigDescription("Icons in a row before a new row starts.", new AcceptableValueRange<int>(1, 20)));

            QuickBarEnabled = Config.Bind("6 - Quick bar 2", "Enabled", true,
                "Show the second inventory row as a quick bar and use its items with the modifier key plus 1 to 8.");
            QuickBarModifier = Config.Bind("6 - Quick bar 2", "Modifier key", KeyCode.LeftAlt,
                "Hold this and press 1 to 8 to use the item in that slot of the second row. " +
                "While it is held, 1 to 8 do not use the normal hotbar.");
            QuickBarAnchor = Config.Bind("6 - Quick bar 2", "Anchor", HudAnchor.Bottom,
                "Screen point the quick bar is placed relative to.");
            QuickBarOffsetX = BindOffset("6 - Quick bar 2", "Offset X", 0f, StackedOffset("Horizontal"));
            QuickBarOffsetY = BindOffset("6 - Quick bar 2", "Offset Y", 0f, StackedOffset("Vertical"));

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
            PartyOffsetX = BindOffset("7 - Party list", "Offset X", 20f, "Horizontal offset from the anchor.");
            PartyOffsetY = BindOffset("7 - Party list", "Offset Y", -20f, "Vertical offset from the anchor.");
            PartyWidth = Config.Bind("7 - Party list", "Width", 220f,
                new ConfigDescription("Width of each row.", new AcceptableValueRange<float>(120f, 500f)));

            QuickSlotsEnabled = Config.Bind("8 - Quick slots", "Enabled", true,
                "Extra slots for any item but ammo, each used with its own key, shown right of the quick bar and " +
                "below your inventory. Items in them count toward your weight; weapons and tools in them can be " +
                "equipped. Turning this off only hides the slots; items already in them are kept.");
            QuickSlotCount = Config.Bind("8 - Quick slots", "Slot count", 3,
                new ConfigDescription("Number of quick slots. Items in slots you remove move to your inventory.",
                    new AcceptableValueRange<int>(0, QuickSlots.MaxSize)));
            KeyCode[] defaultKeys = { KeyCode.Z, KeyCode.V, KeyCode.B, KeyCode.None, KeyCode.None, KeyCode.None };
            for (int i = 0; i < QuickSlots.MaxSize; i++)
            {
                QuickSlotKeys[i] = Config.Bind("8 - Quick slots", $"Slot {i + 1} key", new KeyboardShortcut(defaultKeys[i]),
                    $"Key that uses the item in quick slot {i + 1}. While it is held with an item in the slot, " +
                    "vanilla actions on the same key do not trigger.");
                QuickSlotLabels[i] = Config.Bind("8 - Quick slots", $"Slot {i + 1} label", "",
                    $"Text shown on quick slot {i + 1}. Empty shows its key.");
            }
            QuickInventoryOffsetX = BindOffset("8 - Quick slots", "Inventory offset X", 0f, "Horizontal position of the quick slots under the inventory. With a chest open they sit right of the inventory instead.");
            QuickInventoryOffsetY = BindOffset("8 - Quick slots", "Inventory offset Y", -12f, "Vertical position of the quick slots under the inventory. With a chest open they sit right of the inventory instead.");

            GearSlotsEnabled = Config.Bind("10 - Gear slots", "Enabled", true,
                "Six slots for worn armour (head, chest, legs, cape, utility and trinket), shown right of the " +
                "inventory. What is in them is worn: put armour on and it moves into its slot, take " +
                "it out and it comes off. Turning this off hides the slots; gear already in them stays worn.");

            KeepGearOnDeath = Config.Bind("11 - Death", "Keep gear", false,
                "Gear in the gear slots stays with you when you die instead of going into the tombstone, and you " +
                "still wear it after respawning. This makes dying easier than vanilla.");
            KeepQuickOnDeath = Config.Bind("11 - Death", "Keep quick slots", false,
                "Items in the quick slots stay with you when you die instead of going into the tombstone. This " +
                "makes dying easier than vanilla.");
            ReequipArmour = Config.Bind("11 - Death", "Re-equip armour", true,
                "When you pick up your tombstone, put the armour you wore back on.");
            ReequipWeapons = Config.Bind("11 - Death", "Re-equip weapons", true,
                "When you pick up your tombstone, take the weapons, shield, tools and ammo you held back in hand.");

            UtilitySlots = Config.Bind("12 - Balance", "Utility items", 1,
                new ConfigDescription("Utility items, such as belts and the Wishbone, you can wear at once. Vanilla allows " +
                    "one. Each extra one adds a gear slot. You can never wear two of the same item, and the extra ones " +
                    "do not show on your character.", new AcceptableValueRange<int>(1, 1 + MultiUtility.MaxExtra)));
            ExtraInventoryRows = Config.Bind("12 - Balance", "Extra inventory rows", 0,
                new ConfigDescription("Rows added to your inventory, up to the game's limit of 9 rows. Items in rows " +
                    "you remove move to free cells, or are dropped on the ground if there is no room.",
                    new AcceptableValueRange<int>(0, 5)));
            BaseCarryWeight = Config.Bind("12 - Balance", "Base carry weight", 300f,
                new ConfigDescription("How much you can carry before belts and other bonuses. 300 is vanilla and leaves " +
                    "the value to the game and other mods.", new AcceptableValueRange<float>(50f, 5000f)));

            SkillToastsEnabled = Config.Bind("9 - Skill toasts", "Enabled", true,
                "Show a toast with the skill's level and progress to the next level whenever a skill gains experience.");
            SkillToastsDuration = Config.Bind("9 - Skill toasts", "Duration", 4f,
                new ConfigDescription("Seconds a toast stays after the skill's last gain.",
                    new AcceptableValueRange<float>(1f, 20f)));
            SkillToastsMax = Config.Bind("9 - Skill toasts", "Max toasts", 4,
                new ConfigDescription("Most toasts shown at once; the oldest goes first.",
                    new AcceptableValueRange<int>(1, 10)));
            SkillToastsAnchor = Config.Bind("9 - Skill toasts", "Anchor", HudAnchor.Right,
                "Screen point the toasts are placed relative to. Older toasts move away from it.");
            SkillToastsOffsetX = BindOffset("9 - Skill toasts", "Offset X", -20f, "Horizontal offset from the anchor.");
            SkillToastsOffsetY = BindOffset("9 - Skill toasts", "Offset Y", -60f, "Vertical offset from the anchor.");

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

        /// <summary>
        /// Offsets get a range so config editors offer negative values too, not only the plain number
        /// box some of them fall back to for an unbounded float.
        /// </summary>
        private static string StackedOffset(string direction) =>
            direction + " offset from the anchor. With Stack bars on, a nudge from the stacked position instead.";

        private ConfigEntry<float> BindOffset(string section, string key, float value, string description) =>
            Config.Bind(section, key, value,
                new ConfigDescription(description, new AcceptableValueRange<float>(-2000f, 2000f)));

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            Theme.DestroySprites();
        }
    }
}
