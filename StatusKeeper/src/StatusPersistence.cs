using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace StatusKeeper
{
    /// <summary>
    /// Captures the player's timed status effects into Player.m_customData, which the game already
    /// serialises into the character file, and puts them back on the next spawn.
    ///
    /// Only effects with a ttl are considered: everything permanent is either recomputed from the
    /// world each frame (Wet, Shelter, Cold...) or driven by equipped gear, so persisting it is at
    /// best a no-op. Damage-over-time effects are excluded by default because their damage bookkeeping
    /// lives in private subclass fields that cannot be restored (see the README).
    /// </summary>
    internal static class StatusPersistence
    {
        private const string EffectsKey = "ithilias.statuskeeper.effects";

        // Earlier builds wrote these under a different plugin id. Matched by suffix so the old id
        // does not have to be named here, which also covers any future rename.
        private static readonly string[] LegacySuffixes =
        {
            ".statuskeeper.effects",
            ".statuskeeper.time",
        };

        private struct Saved
        {
            public string Name;
            public float Remaining;
            public short Variant;
        }

        public static void Capture(Player player)
        {
            if (player == null || player.m_customData == null) return;

            RemoveLegacyKeys(player);

            var seman = player.GetSEMan();
            if (seman == null)
            {
                player.m_customData.Remove(EffectsKey);
                return;
            }

            var effects = seman.GetStatusEffects();
            var builder = new StringBuilder();
            int kept = 0;

            if (effects != null)
            {
                foreach (var effect in effects)
                {
                    if (effect == null) continue;
                    if (effect.m_ttl <= 0f) continue;

                    float remaining = effect.GetRemaningTime();
                    if (remaining < Plugin.MinimumRemaining.Value) continue;

                    // Effects held by SEMan are clones, so the name carries a "(Clone)" suffix.
                    string name = Utils.GetPrefabName(effect.name);
                    if (!ShouldPersist(name)) continue;

                    if (kept > 0) builder.Append(';');
                    builder.Append(name).Append(':')
                        .Append(remaining.ToString("F2", CultureInfo.InvariantCulture)).Append(':')
                        .Append(effect.m_hitVariant.ToString(CultureInfo.InvariantCulture));
                    kept++;
                }
            }

            if (kept == 0)
            {
                player.m_customData.Remove(EffectsKey);
                return;
            }

            player.m_customData[EffectsKey] = builder.ToString();
            Plugin.Log.LogInfo($"Saved {kept} status effect(s): {builder}");
        }

        public static void Restore(Player player)
        {
            if (player == null || player.m_customData == null) return;
            if (!player.m_customData.TryGetValue(EffectsKey, out string packed) || string.IsNullOrEmpty(packed))
                return;

            var seman = player.GetSEMan();
            if (seman == null) return;
            if (ObjectDB.instance == null)
            {
                Plugin.Log.LogWarning("ObjectDB not ready; status effects were not restored.");
                return;
            }

            // Time spent logged out is not playtime, so the remaining duration is restored as saved.
            int restored = 0, expired = 0, missing = 0;
            foreach (var saved in Parse(packed))
            {
                float remaining = saved.Remaining;
                if (remaining < Plugin.MinimumRemaining.Value)
                {
                    expired++;
                    continue;
                }

                string name = ResolveRestoreName(saved.Name);
                var effect = seman.AddStatusEffect(name.GetStableHashCode(), resetTime: false, 0, 0f, saved.Variant);
                if (effect == null)
                {
                    // Either the prefab is gone (a mod was removed) or the effect is already active.
                    effect = seman.GetStatusEffect(name.GetStableHashCode());
                    if (effect == null)
                    {
                        missing++;
                        continue;
                    }
                }

                // ttl is the only timing field that is writable from outside; leaving m_time at 0
                // and setting ttl to what was left gives the same remaining duration.
                effect.m_ttl = remaining;
                restored++;
            }

            player.m_customData.Remove(EffectsKey);
            RemoveLegacyKeys(player);

            var summary = new StringBuilder($"Restored {restored} status effect(s)");
            if (expired > 0) summary.Append($", {expired} expired");
            if (missing > 0) summary.Append($", {missing} not found in ObjectDB");
            Plugin.Log.LogInfo(summary.Append('.').ToString());
        }

        /// <summary>
        /// Rested-type effects are restored under a prefab the environment logic will not strip.
        /// Sitting by a fire grants a rested effect under a different prefab whose lifetime
        /// Player.UpdateEnvStatusEffects ties to still being near that fire, so putting it back
        /// under its own name would see it removed on the first frame away from the fire.
        /// </summary>
        private static string ResolveRestoreName(string savedName)
        {
            if (!Plugin.NormaliseRested.Value) return savedName;
            if (!IsRestedPrefab(savedName)) return savedName;
            string target = Plugin.RestedPrefabName.Value;
            return string.IsNullOrEmpty(target) ? savedName : target;
        }

        private static bool IsRestedPrefab(string name)
        {
            foreach (var candidate in Split(Plugin.RestedSourceNames.Value))
            {
                if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool ShouldPersist(string name)
        {
            foreach (var allowed in Split(Plugin.Allowlist.Value))
            {
                if (string.Equals(allowed, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            foreach (var denied in Split(Plugin.Denylist.Value))
            {
                if (string.Equals(denied, name, StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }

        private static IEnumerable<Saved> Parse(string packed)
        {
            foreach (var entry in packed.Split(';'))
            {
                if (string.IsNullOrEmpty(entry)) continue;
                var parts = entry.Split(':');
                if (parts.Length < 2) continue;
                if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float remaining))
                    continue;
                short variant = -1;
                if (parts.Length > 2)
                    short.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out variant);

                yield return new Saved { Name = parts[0], Remaining = remaining, Variant = variant };
            }
        }

        private static void RemoveLegacyKeys(Player player)
        {
            List<string> stale = null;

            foreach (var key in player.m_customData.Keys)
            {
                if (key == EffectsKey) continue;

                foreach (var suffix in LegacySuffixes)
                {
                    if (!key.EndsWith(suffix, StringComparison.Ordinal)) continue;
                    if (stale == null) stale = new List<string>();
                    stale.Add(key);
                    break;
                }
            }

            if (stale == null) return;

            foreach (var key in stale)
            {
                player.m_customData.Remove(key);
            }
        }

        public static IEnumerable<string> Split(string value)
        {
            if (string.IsNullOrEmpty(value)) yield break;
            foreach (var part in value.Split(','))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0) yield return trimmed;
            }
        }

        public static string DescribeActive(Player player)
        {
            var seman = player != null ? player.GetSEMan() : null;
            if (seman == null) return "StatusKeeper: no SEMan available.";

            var effects = seman.GetStatusEffects();
            var sb = new StringBuilder("StatusKeeper active status effects:\n");
            if (effects == null || effects.Count == 0) return sb.Append("  (none)").ToString();

            foreach (var effect in effects)
            {
                if (effect == null) continue;
                string name = Utils.GetPrefabName(effect.name);
                // A permanent effect has m_ttl 0, so GetRemaningTime counts downward from zero
                // forever. Reporting that raw number reads like a bug rather than "no expiry".
                bool timed = effect.m_ttl > 0f;
                sb.Append("  ").Append(name)
                  .Append("  class=").Append(effect.GetType().Name)
                  .Append("  ttl=").Append(timed ? effect.m_ttl.ToString("F1", CultureInfo.InvariantCulture) : "permanent")
                  .Append("  remaining=").Append(timed
                      ? effect.GetRemaningTime().ToString("F1", CultureInfo.InvariantCulture)
                      : "-")
                  .Append("  category=").Append(string.IsNullOrEmpty(effect.m_category) ? "-" : effect.m_category)
                  .Append("  persisted=").Append(timed && ShouldPersist(name))
                  .Append('\n');
            }
            return sb.ToString();
        }
    }
}
