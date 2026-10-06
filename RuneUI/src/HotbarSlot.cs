using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RuneUI
{
    /// <summary>
    /// A clone of the vanilla hotbar slot that this mod fills itself, so the quick bar, food slots and
    /// forsaken power slot look exactly like the hotbar.
    /// </summary>
    internal sealed class HotbarSlot
    {
        public readonly GameObject Go;
        public readonly Image Icon;
        public readonly GuiBar Durability;
        public readonly TMP_Text Amount;
        public readonly TMP_Text Binding;
        private readonly GameObject _equipped;
        private readonly GameObject _queued;
        private int _stackShown = -1;

        public HotbarSlot(HotkeyBar bar, Transform parent, string name)
        {
            Go = Object.Instantiate(bar.m_elementPrefab, parent);
            Go.name = name;
            Transform t = Go.transform;
            Icon = t.Find("icon").GetComponent<Image>();
            Durability = t.Find("durability").GetComponent<GuiBar>();
            Amount = t.Find("amount").GetComponent<TMP_Text>();
            Binding = t.Find("binding").GetComponent<TMP_Text>();
            _equipped = t.Find("equiped").gameObject;
            _queued = t.Find("queued").gameObject;
            t.Find("selected").gameObject.SetActive(false);
            if (Plugin.StyleHotbar.Value) Hotbar.StyleSlot(t, null);
        }

        /// <summary>Places the slot where vanilla would put hotbar slot <paramref name="index"/>, counted from <paramref name="origin"/>.</summary>
        public void PlaceAt(HotkeyBar bar, int index, Vector2 origin)
        {
            Go.transform.localPosition = new Vector3(origin.x + index * bar.m_elementSpace, origin.y, 0f);
        }

        public void SetBinding(string text)
        {
            if (!Binding.enabled) Binding.enabled = true;
            if (Binding.text != text) Binding.text = text;
        }

        /// <summary>Same per-slot display as vanilla HotkeyBar.UpdateIcons.</summary>
        public void Fill(ItemDrop.ItemData item, Player player)
        {
            QualityRing.Update(Go.transform, item, true);
            if (item == null)
            {
                Clear();
                return;
            }

            SetActive(Icon.gameObject, true);
            Icon.sprite = item.GetIcon();
            Icon.color = Color.white;
            bool worn = item.m_shared.m_useDurability && item.m_durability < item.GetMaxDurability();
            SetActive(Durability.gameObject, worn);
            if (worn)
            {
                if (item.m_durability <= 0f)
                {
                    Durability.SetValue(1f);
                    Durability.SetColor(Mathf.Sin(Time.time * 10f) > 0f ? Color.red : new Color(0f, 0f, 0f, 0f));
                }
                else
                {
                    Durability.SetValue(item.GetDurabilityPercentage());
                    Durability.ResetColor();
                }
            }
            SetActive(_equipped, item.m_equipped);
            SetActive(_queued, player.IsEquipActionQueued(item));
            bool stacks = item.m_shared.m_maxStackSize > 1;
            SetActive(Amount.gameObject, stacks);
            if (stacks && _stackShown != item.m_stack)
            {
                Amount.text = item.m_stack + " / " + item.m_shared.m_maxStackSize;
                _stackShown = item.m_stack;
            }
        }

        public void Clear()
        {
            SetActive(Icon.gameObject, false);
            SetActive(Durability.gameObject, false);
            SetActive(_equipped, false);
            SetActive(_queued, false);
            SetActive(Amount.gameObject, false);
            _stackShown = -1;
        }

        public static void SetActive(GameObject go, bool active)
        {
            if (go.activeSelf != active) go.SetActive(active);
        }
    }
}
