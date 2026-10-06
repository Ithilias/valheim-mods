using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace StatusKeeper
{
    [BepInPlugin(Guid, "Status Keeper", PluginVersion.Value)]
    [BepInProcess("valheim.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "ithilias.statuskeeper";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> ModEnabled;
        internal static ConfigEntry<string> Denylist;
        internal static ConfigEntry<string> Allowlist;
        internal static ConfigEntry<float> MinimumRemaining;
        internal static ConfigEntry<bool> NormaliseRested;
        internal static ConfigEntry<string> RestedSourceNames;
        internal static ConfigEntry<string> RestedPrefabName;
        internal static ConfigEntry<KeyboardShortcut> DumpEffectsKey;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            ModEnabled = Config.Bind("1 - General", "Enabled", true,
                "Master switch. Turning this off stops both saving and restoring.");

            Denylist = Config.Bind("2 - Persistence", "Denylist",
                "Wet,Cold,Freezing,Shelter,CampFire,Encumbered,SoftDeath,Burning,Poison,Frost,Lightning,Spirit,Smoked,Tared",
                "Status effect prefab names never carried across a logout. The defaults cover two groups: " +
                "effects the game recomputes from your surroundings every frame anyway, and damage-over-time " +
                "effects whose damage bookkeeping cannot be restored (see the README). Remove entries here " +
                "if you want logging out to stop curing them.");
            Allowlist = Config.Bind("2 - Persistence", "Allowlist", "",
                "Prefab names always carried across, even if listed in the denylist. Takes priority.");
            MinimumRemaining = Config.Bind("2 - Persistence", "Minimum remaining seconds", 1f,
                new ConfigDescription("Effects with less time left than this are not worth carrying across.",
                    new AcceptableValueRange<float>(0f, 60f)));

            NormaliseRested = Config.Bind("3 - Rested", "Normalise rested effect", true,
                "Restore rested-type effects under the prefab below. Sitting by a fire grants rested under a " +
                "prefab whose lifetime the game ties to still being near that fire, so restoring it under its " +
                "own name would see it stripped on the first frame away from the fire.");
            RestedSourceNames = Config.Bind("3 - Rested", "Rested source prefabs", "Resting,Rested",
                "Prefab names treated as rested-type effects for the rule above.");
            RestedPrefabName = Config.Bind("3 - Rested", "Rested target prefab", "Rested",
                "The prefab rested-type effects are restored as.");

            DumpEffectsKey = Config.Bind("4 - Debug", "Dump status effects key",
                new KeyboardShortcut(KeyCode.F10, KeyCode.LeftControl),
                "Logs every active status effect with its prefab name, class, ttl and whether it would be " +
                "persisted. Useful for filling in the deny/allow lists.");

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Patches));

            Log.LogInfo("Status Keeper loaded.");
        }

        // BaseUnityPlugin is a MonoBehaviour, so the debug key needs no game patch of its own.
        private void Update()
        {
            if (Player.m_localPlayer == null) return;
            if (!DumpEffectsKey.Value.IsDown()) return;
            Log.LogInfo(StatusPersistence.DescribeActive(Player.m_localPlayer));
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
