using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SnappySync
{
    /// <summary>
    /// CLIENT-SIDE, COSMETIC ONLY. Makes networked objects you do not own catch up to their
    /// received position faster, so a creature's reaction to a hit is seen sooner.
    ///
    /// Two things govern how long that takes on the receiving side, and this mod addresses both.
    ///
    /// HOW OFTEN the catch-up runs. ZSyncTransform.ClientSync is driven from
    /// MonoUpdaters.FixedUpdate, and it early-returns if it already ran this rendered frame. The
    /// frame guard is a ceiling, not the cadence: the real rate is min(fps, 1/fixedDeltaTime),
    /// and nothing in the game sets fixedDeltaTime, so that is min(fps, 50) Hz. Above 50 fps the
    /// guard never fires and everyone converges at 50 Hz no matter how many frames they render.
    /// Render Rate Smoothing moves the call to CustomLateUpdate so it runs once per rendered
    /// frame for real.
    ///
    /// HOW FAR it moves each time. Vanilla eases by a fixed 20% of the remaining distance:
    ///     if (dist > 5f)         position = target;                            // teleport
    ///     else if (dist > 0.01f) MovePosition(Vector3.Lerp(pos, target, 0.2f));
    /// At 50 Hz that is a time constant of about 90 ms. This replaces it with an exponential
    /// approach against real elapsed time, so the configured rate means the same thing on every
    /// machine and can be set faster than vanilla.
    ///
    /// It changes only how quickly the visual catches up to data already received. It does not
    /// predict, extrapolate or reconcile anything, so it cannot rubber-band, and it has no effect
    /// on authority, hit registration or damage. There is no server component and no version
    /// enforcement, so it is safe to install per-player: anyone without it sees vanilla.
    /// </summary>
    [BepInPlugin(Guid, "SnappySync", PluginVersion.Value)]
    [BepInProcess("valheim.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "ithilias.snappysync";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> RenderRate;
        internal static ConfigEntry<float> Rate;
        internal static ConfigEntry<float> SnapDistance;
        internal static ConfigEntry<bool> LogStats;

        private float _nextLog;
        private int _lastFrame;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Master switch. Off falls back to vanilla smoothing exactly.");

            RenderRate = Config.Bind("General", "Render Rate Smoothing", true,
                new ConfigDescription(
                    "Run the catch-up once per rendered frame instead of on the physics tick. " +
                    "Vanilla is capped at the physics rate (50 Hz), so above 50 fps everyone " +
                    "converges at the same speed and extra frames buy nothing. With this on, a " +
                    "higher frame rate genuinely does reduce how long a reaction takes to show. " +
                    "Objects with a kinematic rigidbody keep the vanilla physics-tick path. " +
                    "Costs a little CPU, since the work now runs every frame."));

            Rate = Config.Bind("General", "Catch-up Rate", 25f,
                new ConfigDescription(
                    "How quickly a non-owned object converges on its received position, per second. " +
                    "The visual time constant is roughly 1/rate: 25 = ~40 ms, 11 = vanilla (~90 ms). " +
                    "Higher is snappier but shows network jitter more; lower is smoother but " +
                    "laggier. Frame-rate independent, unlike vanilla.",
                    new AcceptableValueRange<float>(5f, 60f)));

            SnapDistance = Config.Bind("General", "Snap Distance", 5f,
                new ConfigDescription(
                    "Metres of discrepancy above which the object is teleported instead of eased. " +
                    "5 is vanilla, and the game already teleports at 5, so only values BELOW 5 " +
                    "change anything. Lowering it makes big knockbacks land instantly at the cost " +
                    "of a visible pop.",
                    new AcceptableValueRange<float>(0.5f, 5f)));

            LogStats = Config.Bind("Debug", "Log Stats", false,
                "Log a line every 30s with how many syncs were smoothed and snapped.");

            try
            {
                new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            }
            catch (Exception e)
            {
                Log.LogError($"Patching failed, the game is running vanilla smoothing: {e}");
                return;
            }

            RenderRateSync.Init();

            Log.LogInfo($"SnappySync {PluginVersion.Value} loaded - catch-up rate {Rate.Value}/s, " +
                        $"snap {SnapDistance.Value}m, render rate smoothing " +
                        $"{(RenderRateSync.Active ? "on" : "off")}.");
        }

        /// <summary>
        /// Stats live here rather than inside the hot path: this runs once per frame for the whole
        /// game, not once per synced object. Frames are counted rather than derived from
        /// Time.deltaTime, which reports the fixed timestep when read from FixedUpdate.
        /// </summary>
        private void LateUpdate()
        {
            Smoothing.LastFrameTime = Time.deltaTime;

            if (!LogStats.Value) return;

            if (_nextLog <= 0f)
            {
                _nextLog = Time.time + 30f;
                _lastFrame = Time.frameCount;
                return;
            }

            if (Time.time < _nextLog) return;

            int frames = Time.frameCount - _lastFrame;
            Log.LogInfo($"smoothed {Smoothing.Smoothed} snapped {Smoothing.Snapped} " +
                        $"| rate {Rate.Value}/s | {frames / 30f:F0} fps | render rate " +
                        $"{(RenderRateSync.Active ? "on" : "off")}");

            _nextLog = Time.time + 30f;
            _lastFrame = Time.frameCount;
        }
    }
}
