using System.Collections.Generic;
using UnityEngine;

namespace RuneUI
{
    /// <summary>
    /// Health, stamina, eitr and adrenaline as themed bars in one block, replacing the vanilla
    /// displays. The vanilla ones are hidden by scale, never deactivated, and restored on Remove.
    /// </summary>
    internal static class HudBars
    {
        private const float HealthHeight = 22f;
        private const float StaminaHeight = 14f;
        private const float AdrenalineHeight = 8f;
        private const float Gap = 4f;

        private static RectTransform _root;
        private static ThemedBar _health;
        private static ThemedBar _stamina;
        private static ThemedBar _eitr;
        private static ThemedBar _adrenaline;
        private static int _version = -1;

        private static readonly Dictionary<RectTransform, Vector3> HiddenScales = new Dictionary<RectTransform, Vector3>();

        public static void Update(Hud hud, Player player)
        {
            if (!Plugin.ReplaceBars.Value || player == null)
            {
                Remove();
                return;
            }
            if (_root == null || _version != Theme.Version) Build(hud);

            float width = Plugin.BarsWidth.Value;
            Theme.Place(_root, Plugin.BarsAnchor.Value, Plugin.BarsOffsetX.Value, Plugin.BarsOffsetY.Value);

            float health = player.GetHealth();
            float maxHealth = player.GetMaxHealth();
            _health.Layout(0f, 0f, width, HealthHeight);
            _health.SetValue(health / Mathf.Max(1f, maxHealth),
                Mathf.CeilToInt(health) + " / " + Mathf.CeilToInt(maxHealth));

            float y = HealthHeight + Gap;
            float stamina = player.GetStamina();
            float maxStamina = player.GetMaxStamina();
            float maxEitr = player.GetMaxEitr();
            bool hasEitr = maxEitr > 0f;
            // Stamina and eitr share a row, so magic users do not get a taller block.
            float staminaWidth = hasEitr ? Mathf.Round(width * 0.6f) : width;
            _stamina.Layout(0f, y, staminaWidth, StaminaHeight);
            _stamina.SetValue(stamina / Mathf.Max(1f, maxStamina), Mathf.CeilToInt(stamina).ToString());

            _eitr.SetActive(hasEitr);
            if (hasEitr)
            {
                float eitr = player.GetEitr();
                _eitr.Layout(staminaWidth + Gap, y, width - staminaWidth - Gap, StaminaHeight);
                _eitr.SetValue(eitr / maxEitr, Mathf.CeilToInt(eitr).ToString());
            }
            y += StaminaHeight;

            float adrenaline = player.GetAdrenaline();
            float maxAdrenaline = player.GetMaxAdrenaline();
            bool hasAdrenaline = adrenaline > 0f && maxAdrenaline > 0f;
            _adrenaline.SetActive(hasAdrenaline);
            if (hasAdrenaline)
            {
                y += Gap;
                _adrenaline.Layout(0f, y, width, AdrenalineHeight);
                _adrenaline.SetValue(adrenaline / maxAdrenaline, null);
                y += AdrenalineHeight;
            }

            _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y);
        }

        /// <summary>Runs after the vanilla animators, which may drive the bars' transforms.</summary>
        public static void HideVanilla(Hud hud)
        {
            if (_root == null) return;
            Hide(hud.m_healthPanel);
            Hide(hud.m_foodBarRoot);
            Hide(hud.m_staminaBar2Root);
            Hide(hud.m_eitrBarRoot);
            Hide(hud.m_adrenalineBarRoot);
        }

        private static void Hide(RectTransform rt)
        {
            if (rt == null) return;
            if (!HiddenScales.ContainsKey(rt)) HiddenScales[rt] = rt.localScale;
            if (rt.localScale != Vector3.zero) rt.localScale = Vector3.zero;
        }

        private static void Build(Hud hud)
        {
            Remove();
            _version = Theme.Version;
            _root = Theme.NewRect("RuneUI_Bars", hud.m_rootObject.transform);
            _health = new ThemedBar("Health", _root, Plugin.HealthColor.Value, 14f);
            _stamina = new ThemedBar("Stamina", _root, Plugin.StaminaColor.Value, 11f);
            _eitr = new ThemedBar("Eitr", _root, Plugin.EitrColor.Value, 11f);
            _adrenaline = new ThemedBar("Adrenaline", _root, Plugin.AccentColor.Value, 0f);
        }

        public static void Remove()
        {
            foreach (var pair in HiddenScales)
                if (pair.Key != null) pair.Key.localScale = pair.Value;
            HiddenScales.Clear();
            if (_root != null) Object.Destroy(_root.gameObject);
            ResetState();
        }

        public static void ResetState()
        {
            _root = null;
            _health = _stamina = _eitr = _adrenaline = null;
            HiddenScales.Clear();
        }
    }
}
