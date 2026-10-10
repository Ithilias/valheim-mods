using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RuneUI
{
    /// <summary>
    /// A toast per skill that shows its level and progress to the next level whenever it gains
    /// experience. Further gains while the toast is up update that same toast and keep it up.
    /// </summary>
    internal static class SkillToasts
    {
        private const float Width = 260f;
        private const float Height = 54f;
        private const float Gap = 6f;
        private const float FadeTime = 0.5f;
        private const float LevelUpFlash = 1.5f;

        private sealed class Toast
        {
            public Skills.SkillType Skill;
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Icon;
            public TMP_Text Title;
            public ThemedBar Progress;
            public float ShownAt;
            public float LeveledAt = -99f;
        }

        private struct Gain
        {
            public Skills.SkillType Skill;
            public bool LeveledUp;
        }

        private static readonly AccessTools.FieldRef<Skills, Player> SkillsPlayer =
            AccessTools.FieldRefAccess<Skills, Player>("m_player");
        private static readonly Func<Skills, Skills.SkillType, Skills.Skill> GetSkill =
            AccessTools.MethodDelegate<Func<Skills, Skills.SkillType, Skills.Skill>>(
                AccessTools.Method(typeof(Skills), "GetSkill"));

        private static RectTransform _root;
        private static int _version = -1;
        private static readonly List<Toast> Toasts = new List<Toast>();
        // Gains arrive from game code, possibly before the HUD exists; they are shown on the next HUD update.
        private static readonly List<Gain> Pending = new List<Gain>();

        /// <summary>Skills.RaiseSkill prefix: remember the level to spot a level up.</summary>
        public static float LevelBefore(Skills skills, Skills.SkillType type)
        {
            if (type == Skills.SkillType.None || !IsLocal(skills)) return -1f;
            return GetSkill(skills, type).m_level;
        }

        /// <summary>Skills.RaiseSkill postfix.</summary>
        public static void OnRaised(Skills skills, Skills.SkillType type, float levelBefore)
        {
            if (levelBefore < 0f || !Plugin.ModEnabled.Value || !Plugin.SkillToastsEnabled.Value) return;
            Skills.Skill skill = GetSkill(skills, type);
            if (skill.m_level >= 100f && skill.m_level <= levelBefore) return; // maxed, nothing changes
            Pending.Add(new Gain { Skill = type, LeveledUp = skill.m_level > levelBefore });
        }

        private static bool IsLocal(Skills skills) =>
            Player.m_localPlayer != null && ReferenceEquals(SkillsPlayer(skills), Player.m_localPlayer);

        public static void Update(Hud hud, Player player)
        {
            if (!Plugin.SkillToastsEnabled.Value || player == null)
            {
                Pending.Clear();
                Remove();
                return;
            }
            if (_root == null || _version != Theme.Version) Build(hud);
            Theme.Place(_root, Plugin.SkillToastsAnchor.Value, Plugin.SkillToastsOffsetX.Value,
                Plugin.SkillToastsOffsetY.Value);

            foreach (Gain gain in Pending) Show(gain, player.GetSkills());
            Pending.Clear();

            float now = Time.time;
            float duration = Plugin.SkillToastsDuration.Value;
            for (int i = Toasts.Count - 1; i >= 0; i--)
            {
                Toast t = Toasts[i];
                float age = now - t.ShownAt;
                if (age >= duration)
                {
                    Object.Destroy(t.Root.gameObject);
                    Toasts.RemoveAt(i);
                    continue;
                }
                t.Group.alpha = Mathf.Clamp01((duration - age) / FadeTime);
                bool flashing = now - t.LeveledAt < LevelUpFlash;
                t.Title.color = flashing && Mathf.Sin(now * 12f) > 0f ? Plugin.AccentColor.Value : Plugin.TextColor.Value;
            }
            Layout();
        }

        private static void Show(Gain gain, Skills skills)
        {
            Skills.Skill skill = GetSkill(skills, gain.Skill);
            Toast toast = Toasts.Find(t => t.Skill == gain.Skill);
            if (toast == null)
            {
                if (Toasts.Count >= Plugin.SkillToastsMax.Value)
                {
                    // Make room by dropping the toast that has been up longest.
                    Object.Destroy(Toasts[0].Root.gameObject);
                    Toasts.RemoveAt(0);
                }
                toast = AddToast(gain.Skill);
            }

            float now = Time.time;
            toast.ShownAt = now;
            if (gain.LeveledUp) toast.LeveledAt = now;
            toast.Icon.sprite = skill.m_info.m_icon;
            string name = Localization.instance.Localize("$skill_" + skill.m_info.m_skill.ToString().ToLower());
            int level = (int)skill.m_level;
            toast.Title.text = gain.LeveledUp ? $"{name}  {level}  (+1)" : $"{name}  {level}";
            float progress = skill.m_level >= 100f ? 1f : skill.GetLevelPercentage();
            toast.Progress.SetValue(progress, Mathf.FloorToInt(progress * 100f) + "%");

            // The most recently updated toast goes last, which Layout puts at the anchor.
            Toasts.Remove(toast);
            Toasts.Add(toast);
        }

        /// <summary>Newest at the anchor; older ones move away from it.</summary>
        private static void Layout()
        {
            Vector2 anchor = Theme.AnchorPoint(Plugin.SkillToastsAnchor.Value);
            float direction = anchor.y > 0.75f ? -1f : 1f;
            int count = Toasts.Count;
            for (int i = 0; i < count; i++)
            {
                var rt = Toasts[i].Root;
                rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
                int fromNewest = count - 1 - i;
                rt.anchoredPosition = new Vector2(0f, direction * fromNewest * (Height + Gap));
            }
            _root.sizeDelta = new Vector2(Width, Mathf.Max(0f, count * (Height + Gap) - Gap));
        }

        private static Toast AddToast(Skills.SkillType type)
        {
            var panel = Theme.NewPanel("Skill_" + type, _root);
            var rt = panel.rectTransform;
            rt.sizeDelta = new Vector2(Width, Height);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var iconRt = Theme.NewRect("Icon", rt);
            iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(8f, 0f);
            iconRt.sizeDelta = new Vector2(Height - 16f, Height - 16f);
            var icon = iconRt.gameObject.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;

            float textLeft = Height - 2f;
            var title = Theme.NewText("Title", rt, 15f, TextAlignmentOptions.Left);
            var titleRt = (RectTransform)title.transform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.offsetMin = new Vector2(textLeft, -26f);
            titleRt.offsetMax = new Vector2(-8f, -4f);

            var bar = new ThemedBar("Progress", rt, Plugin.AccentColor.Value, 11f);
            bar.Layout(textLeft, Height - 22f, Width - textLeft - 8f, 14f);

            var toast = new Toast { Skill = type, Root = rt, Group = group, Icon = icon, Title = title, Progress = bar };
            Toasts.Add(toast);
            return toast;
        }

        private static void Build(Hud hud)
        {
            Remove();
            _version = Theme.Version;
            _root = Theme.NewRect("RuneUI_SkillToasts", hud.m_rootObject.transform);
        }

        public static void Remove()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            ResetState();
        }

        public static void ResetState()
        {
            _root = null;
            Toasts.Clear();
        }
    }
}
