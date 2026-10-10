using UnityEngine;

namespace RuneUI
{
    /// <summary>
    /// The forsaken power as a slot right of the hotbar's eighth slot, with its key and cooldown,
    /// replacing the vanilla display. It is a child of the hotbar, so it moves and scales with it.
    /// </summary>
    internal static class PowerSlot
    {
        private const float Gap = 14f;

        private static HotbarSlot _slot;
        private static int _version = -1;
        private static readonly ScaleHider Hider = new ScaleHider();

        public static void Update(Hud hud, HotkeyBar bar, Player player)
        {
            if (!Plugin.PowerSlot.Value || player == null)
            {
                Remove();
                return;
            }
            if (_slot == null || _slot.Go == null || _version != Theme.Version) Build(bar);
            Hider.Hide(hud.m_gpRoot);

            bool show = !player.IsDead();
            HotbarSlot.SetActive(_slot.Go, show);
            if (!show) return;

            string key = ZInput.instance != null ? ZInput.instance.GetBoundKeyString("GP", true) : "";
            _slot.SetBinding(key);

            player.GetGuardianPowerHUD(out StatusEffect power, out float cooldown);
            if (power == null)
            {
                _slot.Clear();
                return;
            }
            HotbarSlot.SetActive(_slot.Icon.gameObject, true);
            _slot.Icon.sprite = power.m_icon;
            _slot.Icon.color = cooldown > 0f ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            bool cooling = cooldown > 0f;
            HotbarSlot.SetActive(_slot.Amount.gameObject, cooling);
            if (cooling) _slot.Amount.text = StatusEffect.GetTimeString(cooldown);
        }

        private static void Build(HotkeyBar bar)
        {
            Remove();
            _version = Theme.Version;
            _slot = new HotbarSlot(bar, bar.transform, "RuneUI_PowerSlot");
            _slot.PlaceAt(bar, Hotbar.SlotCount, new Vector2(Gap, 0f));
            _slot.Clear();
        }

        public static void Remove()
        {
            Hider.Restore();
            if (_slot != null && _slot.Go != null) Object.Destroy(_slot.Go);
            _slot = null;
        }

        public static void ResetState()
        {
            _slot = null;
            Hider.Forget();
        }
    }
}
