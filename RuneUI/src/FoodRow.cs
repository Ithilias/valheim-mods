using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RuneUI
{
    /// <summary>The three eaten foods with their remaining time, shown next to the hotbars.</summary>
    internal static class FoodRow
    {
        private const int Slots = 3;
        private const float SlotSize = 46f;
        private const float SlotGap = 6f;
        private const float TimeHeight = 18f;

        private static RectTransform _root;
        private static readonly Image[] Icons = new Image[Slots];
        private static readonly TMP_Text[] Times = new TMP_Text[Slots];
        private static int _version = -1;

        public static void Update(Hud hud, Player player)
        {
            // Part of replacing the vanilla bars, since vanilla shows food inside the health display.
            if (!Plugin.ReplaceBars.Value || player == null)
            {
                Remove();
                return;
            }
            if (_root == null || _version != Theme.Version) Build(hud);
            Theme.Place(_root, Plugin.FoodAnchor.Value, Plugin.FoodOffsetX.Value, Plugin.FoodOffsetY.Value);

            List<Player.Food> foods = player.GetFoods();
            for (int i = 0; i < Slots; i++)
            {
                bool has = i < foods.Count;
                SetActive(Icons[i].gameObject, has);
                SetActive(Times[i].gameObject, has);
                if (!has) continue;

                Player.Food food = foods[i];
                Icons[i].sprite = food.m_item.GetIcon();
                // Same cues as vanilla: the icon pulses once the food can be eaten again,
                // the timer blinks in the last minute.
                Icons[i].color = food.CanEatAgain()
                    ? new Color(1f, 1f, 1f, 0.7f + Mathf.Sin(Time.time * 5f) * 0.3f)
                    : Color.white;
                float seconds = food.m_time / Game.m_foodRate;
                Color text = Plugin.TextColor.Value;
                if (seconds >= 60f)
                {
                    Times[i].text = Mathf.CeilToInt(seconds / 60f) + "m";
                }
                else
                {
                    Times[i].text = Mathf.FloorToInt(seconds) + "s";
                    text.a *= 0.4f + Mathf.Sin(Time.time * 10f) * 0.6f;
                }
                Times[i].color = text;
            }
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go.activeSelf != active) go.SetActive(active);
        }

        private static void Build(Hud hud)
        {
            Remove();
            _version = Theme.Version;
            _root = Theme.NewRect("RuneUI_Food", hud.m_rootObject.transform);
            _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Slots * SlotSize + (Slots - 1) * SlotGap);
            _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, SlotSize + TimeHeight);

            for (int i = 0; i < Slots; i++)
            {
                var panel = Theme.NewPanel("Food" + i, _root);
                var rt = panel.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(i * (SlotSize + SlotGap), 0f);
                rt.sizeDelta = new Vector2(SlotSize, SlotSize);

                var iconRt = Theme.NewRect("Icon", rt);
                Theme.Stretch(iconRt, 5f);
                var icon = iconRt.gameObject.AddComponent<Image>();
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                Icons[i] = icon;

                var time = Theme.NewText("Time", _root, 13f, TextAlignmentOptions.Center);
                var timeRt = (RectTransform)time.transform;
                timeRt.anchorMin = timeRt.anchorMax = timeRt.pivot = new Vector2(0f, 0f);
                timeRt.anchoredPosition = new Vector2(i * (SlotSize + SlotGap), 0f);
                timeRt.sizeDelta = new Vector2(SlotSize, TimeHeight);
                Times[i] = time;
            }
        }

        public static void Remove()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            ResetState();
        }

        public static void ResetState()
        {
            _root = null;
            for (int i = 0; i < Slots; i++)
            {
                Icons[i] = null;
                Times[i] = null;
            }
        }
    }
}
