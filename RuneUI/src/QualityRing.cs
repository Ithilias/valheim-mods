using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RuneUI
{
    /// <summary>
    /// A ring in the quality colour around an upgradable item's slot. Items upgraded past their normal
    /// maximum get a red ring and, where the game shows no number itself, their quality level.
    /// </summary>
    internal static class QualityRing
    {
        private const string RingName = "RuneUI_Quality";
        private const string NumberName = "RuneUI_QualityNumber";

        private static readonly AccessTools.FieldRef<InventoryGrid, List<InventoryElement>> Elements =
            AccessTools.FieldRefAccess<InventoryGrid, List<InventoryElement>>("m_elements");
        private static readonly AccessTools.FieldRef<InventoryGrid, Inventory> GridInventory =
            AccessTools.FieldRefAccess<InventoryGrid, Inventory>("m_inventory");

        /// <summary>InventoryGrid.UpdateGui postfix: rings for the inventory, container and food slot grids.</summary>
        public static void UpdateGrid(InventoryGrid grid)
        {
            Inventory inventory = GridInventory(grid);
            bool on = Plugin.ModEnabled.Value && Plugin.QualityRings.Value;
            foreach (InventoryElement element in Elements(grid))
            {
                if (element == null) continue;
                if (!on)
                {
                    Remove(element.transform);
                    continue;
                }
                Vector2i pos = element.Position;
                // The inventory shows quality numbers itself.
                Update(element.transform, inventory?.GetItemAt(pos.x, pos.y), false);
            }
        }

        public static void Update(Transform slot, ItemDrop.ItemData item, bool showNumber)
        {
            bool upgradable = Plugin.QualityRings.Value && item != null && item.m_shared.m_maxQuality > 1;
            var ring = slot.Find(RingName);
            if (!upgradable)
            {
                if (ring != null && ring.gameObject.activeSelf) ring.gameObject.SetActive(false);
                return;
            }

            if (ring == null) ring = Build(slot);
            if (!ring.gameObject.activeSelf) ring.gameObject.SetActive(true);

            bool aboveMax = item.m_quality > item.m_shared.m_maxQuality || item.m_quality >= 5;
            var img = ring.GetComponent<Image>();
            if (img.sprite != Theme.RingSprite) Theme.SetSliced(img, Theme.RingSprite, img.color);
            img.color = ColorFor(item.m_quality, aboveMax);

            var number = ring.Find(NumberName).GetComponent<TMP_Text>();
            bool numberShown = showNumber && aboveMax;
            if (number.gameObject.activeSelf != numberShown) number.gameObject.SetActive(numberShown);
            if (numberShown)
            {
                string text = item.m_quality.ToString();
                if (number.text != text) number.text = text;
                number.color = img.color;
            }
        }

        public static Color ColorFor(int quality, bool aboveMax)
        {
            if (aboveMax) return Plugin.QualityAboveMaxColor.Value;
            switch (quality)
            {
                case 1: return Plugin.Quality1Color.Value;
                case 2: return Plugin.Quality2Color.Value;
                case 3: return Plugin.Quality3Color.Value;
                default: return Plugin.Quality4Color.Value;
            }
        }

        private static Transform Build(Transform slot)
        {
            var img = Theme.NewImage(RingName, slot, Theme.RingSprite, Color.white);
            Theme.Stretch(img.rectTransform);
            img.transform.SetAsLastSibling();

            var number = Theme.NewText(NumberName, img.transform, 14f, TextAlignmentOptions.TopLeft);
            var rt = (RectTransform)number.transform;
            Theme.Stretch(rt, 4f);
            number.fontStyle = FontStyles.Bold;
            return img.transform;
        }

        /// <summary>Removes the ring this mod added to a slot it does not own.</summary>
        public static void Remove(Transform slot)
        {
            var ring = slot.Find(RingName);
            if (ring == null) return;
            ring.name = "RuneUI_Removed";
            Object.Destroy(ring.gameObject);
        }
    }
}
