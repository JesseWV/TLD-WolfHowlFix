using System.Collections.Generic;
using Il2Cpp;
using Il2CppTLD.AI;
using UnityEngine;

namespace WolfHowlFix;

/// <summary>
/// The fix, plus optional verbose instrumentation.
///
/// THE DEFECT (read from the disassembly of GameAssembly.dll — see INVESTIGATION.md):
///
/// <c>BaseAi.UpdateScent()</c> accumulates <c>m_LostScentTimeSeconds</c> with no upper clamp
/// on its out-of-range branch, while the in-range branch clamps to [0, MAX]. The value runs
/// away to millions against a ~2,500 s clock. <c>BaseAi.ProcessStalking()</c> then always
/// passes its break-stalking test:
///
/// <code>
/// if (m_LostScentTimeSeconds > m_BreakStalkingTimeSeconds) {
///     m_Animator.SetTrigger(m_AnimParameter_Howl_Trigger);   // wolves howl on giving up
///     SetAiMode(m_DefaultMode);
/// }
/// </code>
///
/// So a stalking wolf gives up on its first tick, howls, reverts to Wander, re-acquires about
/// a second later, and repeats — re-firing the howl once per second against a 6 s clip, which
/// therefore never completes.
///
/// WHY ONLY SOME WOLVES: the corrupting branch only runs while a stalking wolf is beyond
/// <c>m_BreakSlalkingRange</c>. Sense ranges were raised per-variant without scaling it, so
/// only variants whose senses exceed their own break range are exposed (values are
/// detection / smell / hearing vs break range):
///
/// <code>
///   aurora            105 / 150 / 90    vs 100   AFFECTED
///   aurora_Fluffy     105 / 150 / 90    vs 100   AFFECTED
///   Starving           90 / 125 / 60    vs 100   AFFECTED
///   Starving_Aurora    90 / 125 / 60    vs 100   AFFECTED
///   grey_aurora        90 / 120 / 120   vs 100   AFFECTED
///   grey (timberwolf)  90 / 120 / 120   vs 200   correct
///   normal             75 / 100 / 60    vs 100   correct
///   normal_Fluffy      75 / 100 / 60    vs 100   correct
///   town               40 /  50 / 30    vs 100   correct
/// </code>
///
/// Note grey and timberwolf are the same animal — the prefab is WILDLIFE_Wolf_grey, the
/// spawned object is WILDLIFE_Timberwolf_NavMeshAgent. Its break range was correctly raised
/// to 200; its aurora variant was missed and left at 100. That, not the field of view, is
/// why this presents as an aurora-night problem.
///
/// THE FIX: on affected variants only, raise <c>m_BreakSlalkingRange</c> above the wolf's real
/// acquisition limit so the unclamped branch never executes. Correctly-tuned variants are
/// skipped entirely. Nothing else changes — perception, stalking, attacking and disengaging
/// all behave normally.
///
/// Verified live: churn 5-per-5s -> 0, restarts 59 -> 0, howls clean at 5.6-5.7 s.
///
/// WHAT THIS CANNOT DO: restore the give-up rule. The accumulator adds the absolute clock
/// value (~2,400) rather than deltaTime (~0.017), so a single tick beyond the range overshoots
/// the 30 s threshold by orders of magnitude. No choice of range or threshold makes the timing
/// sane from outside the binary; the only options are "never run that branch" (this) or "make
/// the threshold unreachable" (an earlier attempt, see INVESTIGATION.md §3b). Upstream, the
/// fix is `+= deltaTime` plus the missing clamp.
///
/// This writes one plain float field per wolf and restores it on unload and shutdown. No
/// Harmony prefix, no skipped native method, so it cannot trigger the hard crash that a
/// false-returning prefix on an AI method causes.
/// </summary>
internal static class HowlWatch
{
    /// <summary>
    /// Hash of the howl STATE in Wolf_Redux_Controller.
    ///
    /// Do NOT use <c>BaseAi.m_AnimState_Howl</c>: on 2.55 that field holds 538344807 while the
    /// real state <c>Triggered_Howl</c> hashes to -69000134, so matching on it silently detects
    /// zero howls. (<c>m_AnimParameter_Howl_Trigger</c> IS correct.)
    /// </summary>
    private static readonly int HowlStateHash = Animator.StringToHash("Triggered_Howl");

    /// <summary>
    /// Value written into <c>m_BreakSlalkingRange</c> (Hinterland's spelling) on affected
    /// wolves only.
    ///
    /// The unclamped accumulator branch in <c>UpdateScent()</c> only executes while a stalking
    /// wolf is beyond this range, so putting it above the wolf's real acquisition limit stops
    /// that branch ever running. Measured live: wolves acquire the player out to ~210 m and
    /// return Infinity past ~220 m, so 300 clears it with margin.
    ///
    /// Precedent for the approach: grey wolves already ship with 200 against 120 m senses.
    /// </summary>
    private const float SafeBreakStalkingRange = 300f;

    private sealed class State
    {
        public Animator Anim;
        public int TriggerHash;

        // fix
        public float OriginalBreakRange;   // m_BreakSlalkingRange; valid only when Applied
        public bool Applied;
        public bool Excluded;              // correctly-tuned variant; never touched
        public float OutOfRangeSeconds;

        // verbose diagnostics only
        public bool WasInHowl;
        public float LastNt;
        public int Starts;
        public float FirstStart;
    }

    private static readonly Dictionary<int, State> States = new();
    private static readonly List<int> Seen = new();
    private static readonly List<int> Stale = new();

    private static float Fps => 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

    /// <summary>Restore every wolf we touched. Called on scene unload and mod shutdown.</summary>
    internal static void RestoreAll()
    {
        try
        {
            var whc = BaseAiManager.GetWolfHowlController();
            if (whc?.m_Wolves == null) { States.Clear(); return; }

            for (int i = 0; i < whc.m_Wolves.Count; i++)
            {
                var wolf = whc.m_Wolves[i];
                if (wolf == null) continue;
                if (States.TryGetValue(wolf.GetInstanceID(), out var s) && s.Applied)
                {
                    try { wolf.m_BreakSlalkingRange = s.OriginalBreakRange; } catch { }
                }
            }
        }
        catch { }
        States.Clear();
    }

    /// <summary>
    /// Frames between fix scans.
    ///
    /// The fix only has to reach a wolf before it can be stalking from beyond its break range,
    /// so quarter-second granularity is ample. Scanning is also the only cost that scales with
    /// wolf count: measured on 2.55, the list indexer is ~125 ns and GetInstanceID ~75 ns per
    /// wolf, while the field reads the fix actually needs are ~13 ns. Throttling therefore
    /// removes ~93% of the mod's per-frame cost.
    /// </summary>
    private const int ScanIntervalFrames = 15;

    private static int _frame;

    internal static void Tick()
    {
        bool verbose = Settings.Instance.Verbose;

        bool scanDue = false;
        if (++_frame >= ScanIntervalFrames) { _frame = 0; scanDue = true; }

        // Cheapest possible path on the other 14 frames in 15.
        if (!scanDue && !verbose) return;

        WolfHowlController whc;
        try
        {
            whc = BaseAiManager.GetWolfHowlController();
            if (whc?.m_Wolves == null) return;
        }
        catch { return; }

        if (scanDue) Seen.Clear();

        int count = whc.m_Wolves.Count;
        for (int i = 0; i < count; i++)
        {
            AiBaseWolf wolf;
            try { wolf = whc.m_Wolves[i]; } catch { continue; }
            if (wolf == null) continue;

            int id;
            try { id = wolf.GetInstanceID(); } catch { continue; }

            var s = Resolve(id, wolf);
            if (s == null) continue;

            if (scanDue)
            {
                Seen.Add(id);
                if (Settings.Instance.EnableMod) ApplyFix(id, wolf, s);
                else RevertFix(wolf, s);
            }

            // Restart detection compares normalizedTime against the previous frame, so it is
            // never throttled — but it only runs at all when verbose logging is on.
            if (verbose) TrackHowl(id, s);
        }

        if (!scanDue) return;

        // Forget wolves that have left the controller's list (despawned, died). Their
        // m_BreakSlalkingRange goes with them, so there is nothing to restore.
        foreach (var kv in States)
            if (!Seen.Contains(kv.Key)) Stale.Add(kv.Key);
        for (int i = 0; i < Stale.Count; i++) States.Remove(Stale[i]);
        Stale.Clear();
    }

    /// <summary>Fetches or lazily creates the cached per-wolf state. Null if unusable.</summary>
    private static State Resolve(int id, AiBaseWolf wolf)
    {
        if (States.TryGetValue(id, out var s)) return s;

        Animator anim;
        try
        {
            if (!wolf.gameObject.activeInHierarchy) return null;
            anim = wolf.gameObject.GetComponentInChildren<Animator>();
        }
        catch { return null; }
        if (anim == null) return null;

        s = new State { Anim = anim, TriggerHash = wolf.m_AnimParameter_Howl_Trigger };
        States[id] = s;
        return s;
    }

    private static void ApplyFix(int id, AiBaseWolf wolf, State s)
    {
        try
        {
            // Correctly-tuned variants are left completely untouched.
            if (s.Excluded) return;

            // Keep the wolf out of the branch that corrupts the accumulator.
            if (!s.Applied)
            {
                float original = wolf.m_BreakSlalkingRange;

                // A wolf is only exposed if it can perceive the player from beyond its own
                // break-stalking range. If it cannot, it is never "too far" while stalking and
                // the bad branch is unreachable. Grey/timberwolf (120 senses vs 200), normal
                // (100 vs 100) and town (50 vs 100) are already correct and are skipped.
                float longestSense = Mathf.Max(wolf.m_DetectionRange,
                                     Mathf.Max(wolf.m_SmellRange, wolf.m_HearFootstepsRange));

                if (original > longestSense || original >= SafeBreakStalkingRange)
                {
                    s.Excluded = true;
                    return;
                }

                s.OriginalBreakRange = original;
                s.Applied = true;
                LogStatus($"id={id} breakRange {original:0} -> {SafeBreakStalkingRange:0} " +
                          $"(longest sense {longestSense:0})");
            }

            if (wolf.m_BreakSlalkingRange < SafeBreakStalkingRange)
                wolf.m_BreakSlalkingRange = SafeBreakStalkingRange;

            // Clear any accumulator already corrupted before we got here. Cosmetic rather than
            // load-bearing — UpdateScent() runs one instruction before the value is read inside
            // ProcessStalking, so we can never win that race. The range above is what actually
            // fixes it; this just stops the field reading in the millions for other consumers.
            if (wolf.m_LostScentTimeSeconds > 0f)
                wolf.m_LostScentTimeSeconds = 0f;

            // There is deliberately no second half.
            //
            // An earlier version also reimplemented the give-up rule the broken check was
            // meant to enforce ("stalking beyond m_BreakSlalkingRange for
            // m_BreakStalkingTimeSeconds -> one howl, revert to m_DefaultMode"). It was built,
            // it fired correctly, and it turned out to be pointless — see INVESTIGATION.md §3b.
            //
            // Measured: after the give-up fired, the wolf went
            // Wander -> InvestigateSmell -> Stalking in 53 ms. It never dropped the track.
            // The reason is structural: m_BreakSlalkingRange (100) is INSIDE m_SmellRange
            // (150), so the rule can only ever fire at a distance where the wolf can still
            // smell the player and immediately re-acquire. Beyond smell range, where giving up
            // would stick, the wolf is not stalking in the first place. There is no window in
            // which that rule can work, with or without the accumulator bug.
            //
            // Wolves disengage perfectly well on their own by losing line of sight, which was
            // observed repeatedly. So the fix is just half 1: stop the corrupted comparison
            // firing, and change nothing else.
        }
        catch { }
    }

    private static void RevertFix(AiBaseWolf wolf, State s)
    {
        if (!s.Applied) return;
        try { wolf.m_BreakSlalkingRange = s.OriginalBreakRange; } catch { }
        s.Applied = false;
        s.OutOfRangeSeconds = 0f;
    }

    /// <summary>
    /// Verbose-only. Detection is sound because <c>Wolf_Default_Idle_Howl</c> is non-looping,
    /// so normalizedTime rises monotonically and a regression means the state was re-entered.
    /// </summary>
    private static void TrackHowl(int id, State s)
    {
        AnimatorStateInfo info;
        try
        {
            if (s.Anim == null || !s.Anim.isActiveAndEnabled) return;
            info = s.Anim.GetCurrentAnimatorStateInfo(0);
        }
        catch { return; }

        bool inHowl = info.shortNameHash == HowlStateHash;
        float nt = info.normalizedTime;

        if (inHowl)
        {
            if (!s.WasInHowl)
            {
                s.Starts = 1;
                s.FirstStart = Time.realtimeSinceStartup;
                Log($"id={id} howl START nt={nt:0.00}");
            }
            else if (nt < s.LastNt - 0.01f)
            {
                s.Starts++;
                Log($"id={id} howl RESTART #{s.Starts} nt {s.LastNt:0.00} -> {nt:0.00}");
            }
            s.LastNt = nt;
            s.WasInHowl = true;
            return;
        }

        if (!s.WasInHowl) return;

        float elapsed = Time.realtimeSinceStartup - s.FirstStart;
        string verdict = s.Starts <= 1 ? "CLEAN" : $"BUGGED ({s.Starts} starts)";
        Log($"id={id} howl END {verdict} finalNt={s.LastNt:0.00} wallClock={elapsed:0.0}s");
        s.WasInHowl = false;
        s.Starts = 0;
        s.LastNt = 0f;
    }

    /// <summary>Per-howl detail. Only when verbose logging is on.</summary>
    private static void Log(string message)
    {
        if (!Settings.Instance.Verbose) return;
        Core.Log.Msg($"{message}  [fps={Fps:0}]");
    }

    /// <summary>Occasional status lines, without the per-frame fps suffix.</summary>
    private static void LogStatus(string message)
    {
        if (!Settings.Instance.Verbose) return;
        Core.Log.Msg(message);
    }
}
