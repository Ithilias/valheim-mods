using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RuneUI
{
    /// <summary>
    /// A second hotbar showing inventory row 2, used with the modifier key plus 1 to 8. Its slots are
    /// clones of the vanilla hotbar slot, so it looks like the hotbar and needs no item state of its own.
    /// </summary>
    internal static class QuickBar2
    {
        private const int Row = 1;

        private sealed class Slot
        {
            public GameObject Go;
            public Image Icon;
            public GuiBar Durability;
            public TMP_Text Amount;
            public TMP_Text Binding;
            public GameObject Equipped;
            public GameObject Queued;
            public int StackShown = -1;
        }

        private static readonly Func<Player, bool> TakeInput =
            AccessTools.MethodDelegate<Func<Player, bool>>(AccessTools.Method(typeof(Player), "TakeInput"));

        private static RectTransform _root;
        private static readonly Slot[] Slots = new Slot[Hotbar.SlotCount];
        private static int _version = -1;
        private static KeyCode _labelKey;

        public static bool ModifierHeld =>
            Plugin.ModEnabled.Value && Plugin.QuickBarEnabled.Value && ZInput.GetKey(Plugin.QuickBarModifier.Value, false);

        public static void Update(Hud hud, Player player)
        {
            if (!Plugin.QuickBarEnabled.Value || player == null)
            {
                Remove();
                return;
            }
            var bar = Hotbar.Find(hud);
            if (bar == null) return;
            if (_root == null || _version != Theme.Version) Build(bar);

            Theme.Place(_root, Plugin.QuickBarAnchor.Value, Plugin.QuickBarOffsetX.Value,
                Plugin.QuickBarOffsetY.Value, Hotbar.BaseScale);
            bool show = !player.IsDead();
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            if (!show) return;

            if (_labelKey != Plugin.QuickBarModifier.Value) SetLabels();
            Inventory inventory = player.GetInventory();
            for (int x = 0; x < Hotbar.SlotCount; x++) Fill(Slots[x], inventory.GetItemAt(x, Row), player);
        }

        /// <summary>Same per-slot display as vanilla HotkeyBar.UpdateIcons.</summary>
        private static void Fill(Slot slot, ItemDrop.ItemData item, Player player)
        {
            if (item == null)
            {
                slot.Icon.gameObject.SetActive(false);
                slot.Durability.gameObject.SetActive(false);
                slot.Equipped.SetActive(false);
                slot.Queued.SetActive(false);
                slot.Amount.gameObject.SetActive(false);
                return;
            }

            slot.Icon.gameObject.SetActive(true);
            slot.Icon.sprite = item.GetIcon();
            bool worn = item.m_shared.m_useDurability && item.m_durability < item.GetMaxDurability();
            slot.Durability.gameObject.SetActive(worn);
            if (worn)
            {
                if (item.m_durability <= 0f)
                {
                    slot.Durability.SetValue(1f);
                    slot.Durability.SetColor(Mathf.Sin(Time.time * 10f) > 0f ? Color.red : new Color(0f, 0f, 0f, 0f));
                }
                else
                {
                    slot.Durability.SetValue(item.GetDurabilityPercentage());
                    slot.Durability.ResetColor();
                }
            }
            slot.Equipped.SetActive(item.m_equipped);
            slot.Queued.SetActive(player.IsEquipActionQueued(item));
            bool stacks = item.m_shared.m_maxStackSize > 1;
            slot.Amount.gameObject.SetActive(stacks);
            if (stacks && slot.StackShown != item.m_stack)
            {
                slot.Amount.text = item.m_stack + " / " + item.m_shared.m_maxStackSize;
                slot.StackShown = item.m_stack;
            }
        }

        /// <summary>Called after Player.Update for the local player.</summary>
        public static void HandleInput(Player player)
        {
            if (!ModifierHeld || !TakeInput(player) || Hud.IsPieceSelectionVisible() || Hud.InRadial()) return;
            for (int i = 0; i < Hotbar.SlotCount; i++)
            {
                if (!ZInput.GetKeyDown(KeyCode.Alpha1 + i, false) && !ZInput.GetKeyDown(KeyCode.Keypad1 + i, false)) continue;
                ItemDrop.ItemData item = player.GetInventory().GetItemAt(i, Row);
                if (item != null) player.UseItem(null, item, false);
            }
        }

        private static void Build(HotkeyBar bar)
        {
            Remove();
            _version = Theme.Version;
            Hotbar.SlotGeometry(bar, out Vector2 size, out Vector2 pivot, out float span);
            _root = Theme.NewRect("RuneUI_QuickBar", bar.transform.parent);
            _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, span);
            _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);

            for (int x = 0; x < Hotbar.SlotCount; x++)
            {
                var go = Object.Instantiate(bar.m_elementPrefab, _root);
                go.name = "QuickSlot" + x;
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.anchoredPosition = new Vector2(x * bar.m_elementSpace + pivot.x * size.x, pivot.y * size.y);
                go.transform.Find("selected").gameObject.SetActive(false);
                if (Plugin.StyleHotbar.Value) Hotbar.StyleSlot(go.transform, null);
                Slots[x] = new Slot
                {
                    Go = go,
                    Icon = go.transform.Find("icon").GetComponent<Image>(),
                    Durability = go.transform.Find("durability").GetComponent<GuiBar>(),
                    Amount = go.transform.Find("amount").GetComponent<TMP_Text>(),
                    Binding = go.transform.Find("binding").GetComponent<TMP_Text>(),
                    Equipped = go.transform.Find("equiped").gameObject,
                    Queued = go.transform.Find("queued").gameObject,
                };
            }
            SetLabels();
        }

        private static void SetLabels()
        {
            _labelKey = Plugin.QuickBarModifier.Value;
            string prefix = KeyLabel(_labelKey);
            for (int x = 0; x < Hotbar.SlotCount; x++) Slots[x].Binding.text = prefix + (x + 1);
        }

        private static string KeyLabel(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                    return "A";
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    return "C";
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                    return "S";
                default:
                    string name = key.ToString();
                    return name.Length > 0 ? name.Substring(0, 1) : "";
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
            for (int i = 0; i < Slots.Length; i++) Slots[i] = null;
        }
    }
}
