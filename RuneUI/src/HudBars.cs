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
        private const float ThinHeight = 8f;
        private const float Gap = 4f;

        private static RectTransform _root;
        private static ThemedBar _health;
        private static ThemedBar _stamina;
        private static ThemedBar _eitr;
        private static ThemedBar _adrenaline;
        private static ThemedBar _stagger;
        private static float _staggerHideTimer = 99f;
        private static int _version = -1;

        private static readonly ScaleHider Hider = new ScaleHider();

        public static void Update(Hud hud, Player player)
        {
            if (!Plugin.ReplaceBars.Value || player == null)
            {
                Remove();
                return;
            }
            if (_root == null || _version != Theme.Version) Build(hud);

            float width = Plugin.BarsWidth.Value;
            Hotbar.Find(hud);

            // Thin optional bars go above health, so health and stamina never move when they appear.
            float adrenaline = player.GetAdrenaline();
            float maxAdrenaline = player.GetMaxAdrenaline();
            bool hasAdrenaline = adrenaline > 0f && maxAdrenaline > 0f;
            float stagger = player.GetStaggerPercentage();
            // Like vanilla, the stagger bar stays a second after it empties.
            _staggerHideTimer = stagger > 0f ? 0f : _staggerHideTimer + Time.deltaTime;
            bool hasStagger = _staggerHideTimer < 1f;

            float y = 0f;
            _stagger.SetActive(hasStagger);
            if (hasStagger)
            {
                _stagger.Layout(0f, y, width, ThinHeight);
                _stagger.SetValue(stagger, null);
                y += ThinHeight + Gap;
            }
            _adrenaline.SetActive(hasAdrenaline);
            if (hasAdrenaline)
            {
                _adrenaline.Layout(0f, y, width, ThinHeight);
                _adrenaline.SetValue(adrenaline / maxAdrenaline, null);
                y += ThinHeight + Gap;
            }

            float health = player.GetHealth();
            float maxHealth = player.GetMaxHealth();
            _health.Layout(0f, y, width, HealthHeight);
            _health.SetValue(health / Mathf.Max(1f, maxHealth),
                Mathf.CeilToInt(health) + " / " + Mathf.CeilToInt(maxHealth));
            y += HealthHeight + Gap;

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
                _eitr.SetValue(eitr / maxEitr, Mathf.CeilToInt(eitr) + " / " + Mathf.CeilToInt(maxEitr));
            }
            y += StaminaHeight;

            _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y);

            if (Hotbar.Stacking && Hotbar.TryEdges(_root.parent, out _, out float hotbarTop))
            {
                // On top of the hotbar; the bars' own offsets only nudge them from there.
                float bottom = Theme.Bounds(_root, _root.parent).yMin;
                Theme.PlaceStacked(_root, Plugin.HotbarOffsetX.Value + Plugin.BarsOffsetX.Value, bottom,
                    hotbarTop + Plugin.StackGap.Value + Plugin.BarsOffsetY.Value);
            }
            else
            {
                Theme.Place(_root, Plugin.BarsAnchor.Value, Plugin.BarsOffsetX.Value, Plugin.BarsOffsetY.Value);
            }
        }

        /// <summary>
        /// The box around the health and stamina rows as drawn, in <paramref name="space"/>. The thin
        /// adrenaline and stagger bars come and go, so they are left out.
        /// </summary>
        public static bool TryBounds(Transform space, out Rect bounds)
        {
            bounds = Rect.zero;
            if (_root == null || !_root.gameObject.activeInHierarchy) return false;
            bounds = Theme.Union(Theme.Bounds(_health.Root, space), Theme.Bounds(_stamina.Root, space));
            return true;
        }

        /// <summary>Runs after the vanilla animators, which may drive the bars' transforms.</summary>
        public static void HideVanilla(Hud hud)
        {
            if (_root == null) return;
            Hider.Hide(hud.m_healthPanel);
            Hider.Hide(hud.m_foodBarRoot);
            Hider.Hide(hud.m_staminaBar2Root);
            Hider.Hide(hud.m_eitrBarRoot);
            Hider.Hide(hud.m_adrenalineBarRoot);
            if (hud.m_staggerAnimator != null) Hider.Hide(hud.m_staggerAnimator.transform);
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
            _stagger = new ThemedBar("Stagger", _root, Plugin.StaggerColor.Value, 0f);
        }

        public static void Remove()
        {
            Hider.Restore();
            if (_root != null) Object.Destroy(_root.gameObject);
            ResetState();
        }

        public static void ResetState()
        {
            _root = null;
            _health = _stamina = _eitr = _adrenaline = _stagger = null;
            Hider.Forget();
        }
    }
}
