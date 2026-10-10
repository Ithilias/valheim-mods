using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RuneUI
{
    /// <summary>
    /// Wearing up to three utility items at once, for the local player. Vanilla keeps a single
    /// utility item; the first stays in vanilla's field, the others are kept here, and the vanilla
    /// methods that read that field get hooks that add these. They do not show on the character.
    /// </summary>
    internal static class MultiUtility
    {
        public const int MaxExtra = 2;

        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> UtilityItem =
            AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_utilityItem");
        private static readonly AccessTools.FieldRef<Humanoid, HashSet<StatusEffect>> EquipmentEffects =
            AccessTools.FieldRefAccess<Humanoid, HashSet<StatusEffect>>("m_equipmentStatusEffects");
        private static readonly AccessTools.FieldRef<Character, SEMan> Seman =
            AccessTools.FieldRefAccess<Character, SEMan>("m_seman");
        private static readonly AccessTools.FieldRef<Player, float[]> ModifierValues =
            AccessTools.FieldRefAccess<Player, float[]>("m_equipmentModifierValues");
        private static readonly FieldInfo ModifierFieldsField = AccessTools.Field(typeof(Player), "s_equipmentModifierSourceFields");
        private static readonly Action<Humanoid> SetupEquipment =
            AccessTools.MethodDelegate<Action<Humanoid>>(AccessTools.Method(typeof(Humanoid), "SetupEquipment"));
        private static readonly Action<Humanoid, ItemDrop.ItemData, float> DrainDurability =
            AccessTools.MethodDelegate<Action<Humanoid, ItemDrop.ItemData, float>>(AccessTools.Method(typeof(Humanoid), "DrainEquipedItemDurability"));
        private static readonly Func<Humanoid, string, int> GetSetCount =
            AccessTools.MethodDelegate<Func<Humanoid, string, int>>(AccessTools.Method(typeof(Humanoid), "GetSetCount"));

        private static readonly ItemDrop.ItemData[] Extras = new ItemDrop.ItemData[MaxExtra];
        private static Humanoid _owner;
        // Status effects this class added for the extras, so it can take them off again.
        private static readonly HashSet<StatusEffect> ExtraEffects = new HashSet<StatusEffect>();

        /// <summary>Extra utility items allowed, from the setting.</summary>
        public static int Allowed => Mathf.Clamp(Plugin.UtilitySlots.Value - 1, 0, MaxExtra);

        private static bool Mine(Humanoid who) => who != null && ReferenceEquals(who, Player.m_localPlayer);

        private static void Own(Humanoid who)
        {
            if (ReferenceEquals(who, _owner)) return;
            _owner = who;
            Array.Clear(Extras, 0, Extras.Length);
            ExtraEffects.Clear();
        }

        /// <summary>The extra utility items <paramref name="who"/> wears.</summary>
        public static IEnumerable<ItemDrop.ItemData> Worn(Humanoid who)
        {
            if (!ReferenceEquals(who, _owner)) yield break;
            foreach (var item in Extras)
                if (item != null) yield return item;
        }

        public static int WornCount(Humanoid who)
        {
            int count = UtilityItem(who) != null ? 1 : 0;
            foreach (var _ in Worn(who)) count++;
            return count;
        }

        private static int IndexOf(Humanoid who, ItemDrop.ItemData item)
        {
            if (item == null || !ReferenceEquals(who, _owner)) return -1;
            for (int i = 0; i < Extras.Length; i++)
                if (ReferenceEquals(Extras[i], item)) return i;
            return -1;
        }

        /// <summary>
        /// Humanoid.EquipItem prefix. With vanilla's utility field taken and an extra free, empty the
        /// field for the call, so vanilla runs all its checks and equips without taking the first off.
        /// Returns the item that was in the field, for the postfix to put back.
        /// </summary>
        public static ItemDrop.ItemData BeforeEquip(Humanoid who, ItemDrop.ItemData item)
        {
            if (!Mine(who) || item == null || item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Utility) return null;
            Own(who);
            ItemDrop.ItemData first = UtilityItem(who);
            if (first == null || ReferenceEquals(first, item) || IndexOf(who, item) >= 0) return null;
            // Never two of the same item.
            if (first.m_shared.m_name == item.m_shared.m_name) return null;
            foreach (var worn in Worn(who))
                if (worn.m_shared.m_name == item.m_shared.m_name) return null;
            if (FreeIndex() < 0) return null;
            UtilityItem(who) = null;
            return first;
        }

        private static int FreeIndex()
        {
            for (int i = 0; i < Allowed; i++)
                if (Extras[i] == null) return i;
            return -1;
        }

        /// <summary>Humanoid.EquipItem finalizer: the new item becomes an extra, the first goes back.</summary>
        public static void AfterEquip(Humanoid who, ItemDrop.ItemData item, ItemDrop.ItemData first, bool equipped)
        {
            if (first == null) return;
            if (equipped && ReferenceEquals(UtilityItem(who), item)) Extras[FreeIndex()] = item;
            UtilityItem(who) = first;
            first.m_equipped = true;
            if (equipped) SetupEquipment(who);
        }

        /// <summary>Humanoid.IsItemEquiped postfix.</summary>
        public static bool IsWorn(Humanoid who, ItemDrop.ItemData item) => IndexOf(who, item) >= 0;

        /// <summary>
        /// Humanoid.UnequipItem prefix. Vanilla does not know the extras, so take one off here; it
        /// then sees the item as not equipped and does nothing.
        /// </summary>
        public static void Unequip(Humanoid who, ItemDrop.ItemData item, bool triggerEffects)
        {
            int index = IndexOf(who, item);
            if (index < 0) return;
            Extras[index] = null;
            item.m_equipped = false;
            SetupEquipment(who);
            item.m_shared.m_unequipEffect.Create(who.transform.position, Quaternion.identity);
        }

        /// <summary>Humanoid.UnequipAllItems and Player.UnequipDeathDropItems postfix.</summary>
        public static void UnequipAll(Humanoid who)
        {
            if (!ReferenceEquals(who, _owner)) return;
            foreach (var item in Extras)
                if (item != null) who.UnequipItem(item, false);
        }

        /// <summary>Takes off extras beyond what the setting allows, after it was lowered.</summary>
        public static void Update(Player player)
        {
            if (!ReferenceEquals(player, _owner)) return;
            for (int i = Allowed; i < Extras.Length; i++)
                if (Extras[i] != null) player.UnequipItem(Extras[i]);
        }

        /// <summary>Humanoid.UpdateEquipmentStatusEffects prefix: keep vanilla from removing the extras' effects.</summary>
        public static void BeforeEffects(Humanoid who)
        {
            if (!ReferenceEquals(who, _owner) || ExtraEffects.Count == 0) return;
            EquipmentEffects(who).ExceptWith(ExtraEffects);
        }

        /// <summary>Humanoid.UpdateEquipmentStatusEffects postfix: add the extras' own and set effects.</summary>
        public static void AfterEffects(Humanoid who)
        {
            if (!ReferenceEquals(who, _owner)) return;
            var wanted = new HashSet<StatusEffect>();
            foreach (var item in Worn(who))
            {
                if (item.m_shared.m_equipStatusEffect != null) wanted.Add(item.m_shared.m_equipStatusEffect);
                if (item.m_shared.m_setStatusEffect != null && item.m_shared.m_setName.Length > 0 && item.m_shared.m_setSize > 1
                    && GetSetCount(who, item.m_shared.m_setName) >= item.m_shared.m_setSize)
                    wanted.Add(item.m_shared.m_setStatusEffect);
            }
            HashSet<StatusEffect> vanilla = EquipmentEffects(who);
            SEMan seman = Seman(who);
            foreach (var effect in ExtraEffects)
                if (!wanted.Contains(effect) && !vanilla.Contains(effect)) seman.RemoveStatusEffect(effect.NameHash());
            foreach (var effect in wanted)
                if (!vanilla.Contains(effect) && !seman.HaveStatusEffect(effect.NameHash()))
                    seman.AddStatusEffect(effect, false, 0, 0f, -1);
            wanted.ExceptWith(vanilla);
            ExtraEffects.Clear();
            ExtraEffects.UnionWith(wanted);
        }

        /// <summary>Humanoid.GetSetCount postfix.</summary>
        public static int SetCount(Humanoid who, string setName)
        {
            int count = 0;
            foreach (var item in Worn(who))
                if (item.m_shared.m_setName == setName) count++;
            return count;
        }

        /// <summary>Humanoid.GetEquipmentWeight postfix.</summary>
        public static float Weight(Humanoid who)
        {
            float weight = 0f;
            foreach (var item in Worn(who)) weight += item.m_shared.m_weight;
            return weight;
        }

        /// <summary>Humanoid.UpdateEquipment postfix: worn items lose durability like vanilla's.</summary>
        public static void Drain(Humanoid who, float dt)
        {
            foreach (var item in new List<ItemDrop.ItemData>(Worn(who)))
                if (item.m_shared.m_useDurability) DrainDurability(who, item, dt);
        }

        /// <summary>Player.GetEquipmentEitrRegenModifier postfix.</summary>
        public static float EitrRegen(Humanoid who)
        {
            float value = 0f;
            foreach (var item in Worn(who)) value += item.m_shared.m_eitrRegenModifier;
            return value;
        }

        /// <summary>Player.UpdateModifiers postfix: movement, stamina and the other equipment modifiers.</summary>
        public static void AddModifiers(Player player)
        {
            if (!ReferenceEquals(player, _owner) || !(ModifierFieldsField?.GetValue(null) is FieldInfo[] fields)) return;
            float[] values = ModifierValues(player);
            if (values == null) return;
            foreach (var item in Worn(player))
                for (int i = 0; i < values.Length && i < fields.Length; i++)
                    if (fields[i].GetValue(item.m_shared) is float value) values[i] += value;
        }

        public static void ResetState()
        {
            _owner = null;
            Array.Clear(Extras, 0, Extras.Length);
            ExtraEffects.Clear();
        }
    }
}
