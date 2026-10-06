using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RuneUI
{
    /// <summary>
    /// Nearby players with their health, closest first. Health comes from each player's synced
    /// object data, so this works without the other players running the mod.
    /// </summary>
    internal static class PartyList
    {
        private const float RefreshInterval = 0.25f;
        private const float NameHeight = 18f;
        private const float BarHeight = 10f;
        private const float RowGap = 6f;

        private sealed class Row
        {
            public RectTransform Root;
            public TMP_Text Name;
            public ThemedBar Health;
        }

        private static RectTransform _root;
        private static readonly List<Row> Rows = new List<Row>();
        private static readonly List<Player> Nearby = new List<Player>();
        private static int _version = -1;
        private static float _nextRefresh;

        public static void Update(Hud hud, Player player)
        {
            if (!Plugin.PartyEnabled.Value || player == null)
            {
                Remove();
                return;
            }
            if (_root == null || _version != Theme.Version) Build(hud);
            Theme.Place(_root, Plugin.PartyAnchor.Value, Plugin.PartyOffsetX.Value, Plugin.PartyOffsetY.Value);

            if (Time.time >= _nextRefresh)
            {
                _nextRefresh = Time.time + RefreshInterval;
                Collect(player);
            }
            Show(player);
        }

        private static void Collect(Player self)
        {
            Nearby.Clear();
            Vector3 origin = self.transform.position;
            float range = Plugin.PartyRange.Value;
            float rangeSqr = range * range;
            foreach (Player other in Player.GetAllPlayers())
            {
                if (other == null || other == self) continue;
                if ((other.transform.position - origin).sqrMagnitude <= rangeSqr) Nearby.Add(other);
            }
            Nearby.Sort((a, b) =>
                (a.transform.position - origin).sqrMagnitude.CompareTo((b.transform.position - origin).sqrMagnitude));
            int max = Plugin.PartyMaxPlayers.Value;
            if (Nearby.Count > max) Nearby.RemoveRange(max, Nearby.Count - max);
        }

        private static void Show(Player self)
        {
            float width = Plugin.PartyWidth.Value;
            float rowHeight = NameHeight + BarHeight;
            int shown = 0;
            foreach (Player other in Nearby)
            {
                // A player can log out between refreshes.
                if (other == null) continue;
                Row row = shown < Rows.Count ? Rows[shown] : AddRow();
                shown++;
                if (!row.Root.gameObject.activeSelf) row.Root.gameObject.SetActive(true);
                row.Root.anchoredPosition = new Vector2(0f, -(shown - 1) * (rowHeight + RowGap));
                row.Root.sizeDelta = new Vector2(width, rowHeight);

                string name = other.GetPlayerName();
                if (row.Name.text != name) row.Name.text = name;
                row.Health.Layout(0f, NameHeight, width, BarHeight);
                row.Health.SetValue(other.IsDead() ? 0f : other.GetHealthPercentage(), null);
            }
            for (int i = shown; i < Rows.Count; i++)
                if (Rows[i].Root.gameObject.activeSelf) Rows[i].Root.gameObject.SetActive(false);

            _root.sizeDelta = new Vector2(width, Mathf.Max(0f, shown * (rowHeight + RowGap) - RowGap));
        }

        private static Row AddRow()
        {
            var root = Theme.NewRect("Player" + Rows.Count, _root);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);

            var name = Theme.NewText("Name", root, 14f, TextAlignmentOptions.BottomLeft);
            var nameRt = (RectTransform)name.transform;
            nameRt.anchorMin = new Vector2(0f, 1f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.pivot = new Vector2(0f, 1f);
            nameRt.offsetMin = new Vector2(2f, -NameHeight);
            nameRt.offsetMax = Vector2.zero;

            var row = new Row
            {
                Root = root,
                Name = name,
                Health = new ThemedBar("Health", root, Plugin.HealthColor.Value, 0f),
            };
            Rows.Add(row);
            return row;
        }

        private static void Build(Hud hud)
        {
            Remove();
            _version = Theme.Version;
            _root = Theme.NewRect("RuneUI_Party", hud.m_rootObject.transform);
            _nextRefresh = 0f;
        }

        public static void Remove()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            ResetState();
        }

        public static void ResetState()
        {
            _root = null;
            Rows.Clear();
            Nearby.Clear();
        }
    }
}
