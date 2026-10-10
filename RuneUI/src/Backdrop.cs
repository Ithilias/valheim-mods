using UnityEngine;

namespace RuneUI
{
    /// <summary>
    /// One panel behind the hotbar, quick bar and bars, so they read as a single part of the HUD
    /// instead of floating over the world. It only covers what is always there, so it does not move
    /// while adrenaline or stagger bars come and go.
    /// </summary>
    internal static class Backdrop
    {
        private static RectTransform _root;
        private static int _version = -1;

        public static void Update(Hud hud, Player player)
        {
            if (!Plugin.BackdropEnabled.Value || player == null)
            {
                Remove();
                return;
            }
            var bar = Hotbar.Find(hud);
            if (bar == null) return;
            if (_root == null || _version != Theme.Version) Build(bar);

            Transform space = _root.parent;
            bool found = false;
            Rect box = Rect.zero;
            if (!player.IsDead())
            {
                if (Hotbar.TryBounds(space, out Rect r)) { box = r; found = true; }
                if (QuickBar2.TryBounds(space, out r)) { box = found ? Theme.Union(box, r) : r; found = true; }
                if (HudBars.TryBounds(space, out r)) { box = found ? Theme.Union(box, r) : r; found = true; }
            }
            if (_root.gameObject.activeSelf != found) _root.gameObject.SetActive(found);
            if (!found) return;

            float pad = Plugin.BackdropPadding.Value * Plugin.HudScale.Value;
            box = Rect.MinMaxRect(box.xMin - pad, box.yMin - pad, box.xMax + pad, box.yMax + pad);
            Place(_root, box);
        }

        /// <summary>Sizes and moves <paramref name="rt"/> to cover <paramref name="box"/>, given in its parent's space.</summary>
        private static void Place(RectTransform rt, Rect box)
        {
            var parent = (RectTransform)rt.parent;
            Vector2 anchorPoint = parent.rect.min + Vector2.Scale(parent.rect.size, rt.anchorMin);
            Vector2 pos = box.min + Vector2.Scale(box.size, rt.pivot) - anchorPoint;
            // anchoredPosition does not read back exactly, so only write real changes.
            if ((rt.anchoredPosition - pos).sqrMagnitude > 0.25f) rt.anchoredPosition = pos;
            if ((rt.sizeDelta - box.size).sqrMagnitude > 0.25f) rt.sizeDelta = box.size;
        }

        private static void Build(HotkeyBar bar)
        {
            Remove();
            _version = Theme.Version;
            _root = Theme.NewRect("RuneUI_Backdrop", bar.transform.parent);
            _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(0.5f, 0.5f);
            // Right behind the hotbar, so the bars and quick bar, added later, draw on top too.
            _root.SetSiblingIndex(bar.transform.GetSiblingIndex());
            Frames.Build(_root, Plugin.BackdropStyle.Value, Plugin.BackdropOpacity.Value);
        }

        public static void Remove()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            ResetState();
        }

        public static void ResetState()
        {
            _root = null;
            _version = -1;
        }
    }
}
