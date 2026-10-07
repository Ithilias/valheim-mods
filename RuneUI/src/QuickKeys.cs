using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace RuneUI
{
    /// <summary>
    /// A quick slot key must not also trigger the vanilla action on the same key (V toggles auto
    /// pickup, for example). While a quick slot key is held and its slot has an item, ZInput reports
    /// that key, and every vanilla button bound to it, as not pressed.
    /// </summary>
    internal static class QuickKeys
    {
        private static readonly AccessTools.FieldRef<ZInput, Dictionary<string, ZInput.ButtonDef>> Buttons =
            AccessTools.FieldRefAccess<ZInput, Dictionary<string, ZInput.ButtonDef>>("m_buttons");
        private static readonly Func<KeyCode, bool, string> KeyCodeToPath =
            AccessTools.MethodDelegate<Func<KeyCode, bool, string>>(AccessTools.Method(typeof(ZInput), "KeyCodeToPath"));

        private static readonly HashSet<string> BlockedButtons = new HashSet<string>();
        private static readonly HashSet<KeyCode> BlockedKeys = new HashSet<KeyCode>();
        private static float _nextRefresh;
        // Set while this class reads keys itself, so its own ZInput calls are not filtered.
        private static bool _reading;
        private static int _activeFrame = -1;
        private static bool _active;

        /// <summary>
        /// Which vanilla buttons share a quick slot key. Players can rebind either side at any time,
        /// so this is redone every few seconds; it is a walk over about a hundred bindings.
        /// </summary>
        private static void Refresh()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 3f;
            BlockedButtons.Clear();
            BlockedKeys.Clear();
            ZInput input = ZInput.instance;
            if (input == null) return;

            var paths = new HashSet<string>();
            for (int i = 0; i < QuickSlots.MaxSize; i++)
            {
                KeyCode key = QuickSlots.KeyFor(i).MainKey;
                if (key == KeyCode.None) continue;
                BlockedKeys.Add(key);
                string path = KeyCodeToPath(key, false);
                if (!string.IsNullOrEmpty(path)) paths.Add(path);
            }
            foreach (var button in Buttons(input))
            {
                ZInput.ButtonDef def = button.Value;
                if (def?.ButtonAction == null || def.ButtonAction.bindings.Count == 0) continue;
                if (paths.Contains(def.GetActionPath(true) ?? "") || paths.Contains(def.GetActionPath(false) ?? ""))
                    BlockedButtons.Add(button.Key);
            }
        }

        /// <summary>Whether a quick slot key with an item behind it is held this frame.</summary>
        private static bool Active()
        {
            if (_activeFrame == Time.frameCount) return _active;
            _activeFrame = Time.frameCount;
            _active = false;
            if (!Plugin.ModEnabled.Value || Player.m_localPlayer == null) return false;
            for (int i = 0; i < QuickSlots.Count && !_active; i++) _active = QuickSlots.KeyHeldWithItem(i);
            return _active;
        }

        /// <summary>
        /// Whether <paramref name="key"/> was pressed this frame, or with <paramref name="held"/> is held,
        /// with all its modifiers held. Read through ZInput, which follows the game's input system.
        /// </summary>
        public static bool Check(BepInEx.Configuration.KeyboardShortcut key, bool held)
        {
            if (key.MainKey == KeyCode.None) return false;
            _reading = true;
            try
            {
                if (!(held ? ZInput.GetKey(key.MainKey, false) : ZInput.GetKeyDown(key.MainKey, false))) return false;
                foreach (KeyCode modifier in key.Modifiers)
                    if (!ZInput.GetKey(modifier, false)) return false;
                return true;
            }
            finally
            {
                _reading = false;
            }
        }

        /// <summary>ZInput.GetButton, GetButtonDown and GetButtonUp postfix.</summary>
        public static void FilterButton(string name, ref bool result)
        {
            if (!result || _reading) return;
            Refresh();
            if (BlockedButtons.Contains(name) && Active()) result = false;
        }

        /// <summary>ZInput.GetKey, GetKeyDown and GetKeyUp postfix.</summary>
        public static void FilterKey(KeyCode key, ref bool result)
        {
            if (!result || _reading) return;
            Refresh();
            if (BlockedKeys.Contains(key) && Active()) result = false;
        }
    }
}
