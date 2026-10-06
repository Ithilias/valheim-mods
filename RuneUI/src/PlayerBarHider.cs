using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RuneUI
{
    /// <summary>
    /// Hides the health bar EnemyHud floats over other players, keeping their name. Only the bar's
    /// scale is changed, and only on huds this mod hid, so turning it off restores exactly that.
    /// </summary>
    internal static class PlayerBarHider
    {
        private static readonly FieldInfo HudsField = AccessTools.Field(typeof(EnemyHud), "m_huds");
        private static readonly FieldInfo GuiField =
            AccessTools.Field(AccessTools.Inner(typeof(EnemyHud), "HudData"), "m_gui");

        private static readonly Dictionary<Transform, Vector3> Hidden = new Dictionary<Transform, Vector3>();
        private static readonly List<Transform> Stale = new List<Transform>();

        public static bool Active =>
            Plugin.ModEnabled.Value && Plugin.PartyEnabled.Value && Plugin.HidePlayerBars.Value;

        public static void Update(EnemyHud enemyHud)
        {
            if (!Active)
            {
                Restore();
                return;
            }
            if (!(HudsField.GetValue(enemyHud) is IDictionary huds)) return;

            foreach (DictionaryEntry entry in huds)
            {
                var character = entry.Key as Character;
                if (character == null || !character.IsPlayer()) continue;
                var gui = GuiField.GetValue(entry.Value) as GameObject;
                if (gui == null) continue;
                Transform health = gui.transform.Find("Health");
                if (health == null) continue;
                if (!Hidden.ContainsKey(health)) Hidden[health] = health.localScale;
                if (health.localScale != Vector3.zero) health.localScale = Vector3.zero;
            }

            // Huds are destroyed when players leave view; forget those.
            foreach (var t in Hidden.Keys)
                if (t == null) Stale.Add(t);
            foreach (var t in Stale) Hidden.Remove(t);
            Stale.Clear();
        }

        public static void Restore()
        {
            if (Hidden.Count == 0) return;
            foreach (var pair in Hidden)
                if (pair.Key != null) pair.Key.localScale = pair.Value;
            Hidden.Clear();
        }
    }
}
