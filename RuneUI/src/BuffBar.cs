using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RuneUI
{
    /// <summary>
    /// Eaten food and status effects as one block of hotbar sized icons. Rows fill away from the
    /// anchor, so with the default bottom left anchor new rows stack upwards.
    /// </summary>
    internal static class BuffBar
    {
        private const float Gap = 6f;

        private sealed class Slot
        {
            public RectTransform Root;
            public Image Icon;
            public TMP_Text Time;
            public TMP_Text Badge;
        }

        private static RectTransform _root;
        private static readonly List<Slot> Slots = new List<Slot>();
        private static readonly List<StatusEffect> Effects = new List<StatusEffect>();
        private static readonly ScaleHider Hider = new ScaleHider();
        private static int _version = -1;
        private static float _slotSize = 64f;

        private static bool ShowEffects => Plugin.UnifyBuffs.Value;

        public static void Update(Hud hud, Player player)
        {
            // Without unified buffs this still shows food when the bars replace vanilla's health
            // display, since vanilla shows food inside it.
            if ((!Plugin.UnifyBuffs.Value && !Plugin.ReplaceBars.Value) || player == null)
            {
                Remove();
                return;
            }
            if (_root == null || _version != Theme.Version) Build(hud);
            Theme.Place(_root, Plugin.BuffsAnchor.Value, Plugin.BuffsOffsetX.Value, Plugin.BuffsOffsetY.Value);

            int count = 0;
            foreach (Player.Food food in player.GetFoods()) ShowFood(NextSlot(count++), food);

            Effects.Clear();
            if (ShowEffects)
            {
                player.GetSEMan().GetHUDStatusEffects(Effects);
                foreach (StatusEffect effect in Effects)
                    if (!effect.m_hidden) ShowEffect(NextSlot(count++), effect, player);
            }

            for (int i = count; i < Slots.Count; i++)
                if (Slots[i].Root.gameObject.activeSelf) Slots[i].Root.gameObject.SetActive(false);
            Layout(count);
        }

        /// <summary>Hides the vanilla status effect list. Food is hidden with the vanilla bars.</summary>
        public static void HideVanilla(Hud hud)
        {
            if (_root != null && ShowEffects) Hider.Hide(hud.m_statusEffectListRoot);
            else Hider.Restore();
        }

        private static void ShowFood(Slot slot, Player.Food food)
        {
            SetBadge(slot, null);
            slot.Icon.sprite = food.m_item.GetIcon();
            // Same cues as vanilla: the icon pulses once the food can be eaten again,
            // the timer blinks in the last minute.
            slot.Icon.color = food.CanEatAgain()
                ? new Color(1f, 1f, 1f, 0.7f + Mathf.Sin(Time.time * 5f) * 0.3f)
                : Color.white;
            float seconds = food.m_time / Game.m_foodRate;
            Color text = Plugin.TextColor.Value;
            if (seconds >= 60f)
            {
                SetText(slot, Mathf.CeilToInt(seconds / 60f) + "m");
            }
            else
            {
                SetText(slot, Mathf.FloorToInt(seconds) + "s");
                text.a *= 0.4f + Mathf.Sin(Time.time * 10f) * 0.6f;
            }
            slot.Time.color = text;
        }

        private static void ShowEffect(Slot slot, StatusEffect effect, Player player)
        {
            // Rested and resting show the comfort level, which vanilla only flashes once in a message.
            int hash = effect.NameHash();
            bool comfort = hash == SEMan.s_statusEffectRested || hash == SEMan.s_statusEffectResting || effect is SE_Rested;
            SetBadge(slot, comfort ? player.GetComfortLevel().ToString() : null);
            slot.Icon.sprite = effect.m_icon;
            slot.Icon.color = effect.m_flashIcon && Mathf.Sin(Time.time * 10f) > 0f
                ? new Color(1f, 0.5f, 0.5f, 1f)
                : Color.white;
            SetText(slot, effect.GetIconText());
            slot.Time.color = Plugin.TextColor.Value;
        }

        private static void SetText(Slot slot, string text)
        {
            bool has = !string.IsNullOrEmpty(text);
            if (slot.Time.gameObject.activeSelf != has) slot.Time.gameObject.SetActive(has);
            if (has && slot.Time.text != text) slot.Time.text = text;
        }

        private static void SetBadge(Slot slot, string text)
        {
            bool has = !string.IsNullOrEmpty(text);
            if (slot.Badge.gameObject.activeSelf != has) slot.Badge.gameObject.SetActive(has);
            if (has && slot.Badge.text != text) slot.Badge.text = text;
        }

        private static Slot NextSlot(int index)
        {
            Slot slot = index < Slots.Count ? Slots[index] : AddSlot();
            if (!slot.Root.gameObject.activeSelf) slot.Root.gameObject.SetActive(true);
            return slot;
        }

        /// <summary>Rows run along the anchor's edge and grow away from it.</summary>
        private static void Layout(int count)
        {
            int perRow = Plugin.BuffsPerRow.Value;
            Vector2 anchor = Theme.AnchorPoint(Plugin.BuffsAnchor.Value);
            float step = _slotSize + Gap;
            int columns = Mathf.Max(1, Mathf.Min(count, perRow));
            int rows = Mathf.Max(1, (count + perRow - 1) / perRow);
            _root.sizeDelta = new Vector2(columns * step - Gap, rows * step - Gap);

            // Fill from the anchor side: right to left for right anchors, top down for top anchors.
            bool fromRight = anchor.x > 0.75f;
            bool fromTop = anchor.y > 0.75f;
            for (int i = 0; i < count; i++)
            {
                int col = i % perRow;
                int row = i / perRow;
                var rt = Slots[i].Root;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(fromRight ? 1f : 0f, fromTop ? 1f : 0f);
                rt.anchoredPosition = new Vector2((fromRight ? -col : col) * step, (fromTop ? -row : row) * step);
            }
        }

        private static Slot AddSlot()
        {
            var panel = Theme.NewPanel("Buff" + Slots.Count, _root);
            var rt = panel.rectTransform;
            rt.sizeDelta = new Vector2(_slotSize, _slotSize);

            var iconRt = Theme.NewRect("Icon", rt);
            Theme.Stretch(iconRt, _slotSize * 0.14f);
            var icon = iconRt.gameObject.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;

            var time = Theme.NewText("Time", rt, 13f, TextAlignmentOptions.Bottom);
            var timeRt = (RectTransform)time.transform;
            Theme.Stretch(timeRt, 2f);
            time.fontStyle = FontStyles.Bold;

            var badge = Theme.NewText("Badge", rt, 15f, TextAlignmentOptions.TopRight);
            Theme.Stretch((RectTransform)badge.transform, 4f);
            badge.fontStyle = FontStyles.Bold;
            badge.color = Plugin.AccentColor.Value;
            badge.gameObject.SetActive(false);

            var slot = new Slot { Root = rt, Icon = icon, Time = time, Badge = badge };
            Slots.Add(slot);
            return slot;
        }

        private static void Build(Hud hud)
        {
            Remove();
            _version = Theme.Version;
            var bar = Hotbar.Find(hud);
            if (bar != null)
            {
                Hotbar.SlotGeometry(bar, out Vector2 size, out _, out _);
                _slotSize = size.y * Hotbar.BaseScale;
            }
            _root = Theme.NewRect("RuneUI_Buffs", hud.m_rootObject.transform);
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
            Slots.Clear();
            Effects.Clear();
            Hider.Forget();
        }
    }
}
