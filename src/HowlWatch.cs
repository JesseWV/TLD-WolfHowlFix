using System.Collections.Generic;
using Il2Cpp;
using Il2CppTLD.AI;
using UnityEngine;

namespace WolfHowlFix;

/// <summary>
/// Optional diagnostics. The fix itself lives in <see cref="Patch_UpdateScent"/>; nothing in
/// this file changes game behaviour.
///
/// This watches the howl animation state per wolf and reports starts, restarts and completions,
/// which is how the bug was originally characterised: a non-looping 6 s clip whose
/// normalizedTime kept regressing, up to 48 restarts on a single wolf. With the fix in place a
/// howl should log exactly one START and one END CLEAN at ~5.7 s.
///
/// Everything is behind the verbose setting, so the mod is silent and near-free by default.
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

    private sealed class State
    {
        public Animator Anim;
        public bool WasInHowl;
        public float LastNt;
        public int Starts;
        public float FirstStart;
    }

    private static readonly Dictionary<int, State> States = new();
    private static readonly List<int> Seen = new();
    private static readonly List<int> Stale = new();

    private static float Fps => 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

    /// <summary>Frames between housekeeping passes; restart detection itself is per-frame.</summary>
    private const int ScanIntervalFrames = 15;

    private static int _frame;

    /// <summary>Nothing to undo — the fix writes no persistent state onto wolves.</summary>
    internal static void RestoreAll() => States.Clear();

    internal static void Tick()
    {
        bool verbose = Config.Verbose;

        bool scanDue = false;
        if (++_frame >= ScanIntervalFrames) { _frame = 0; scanDue = true; }

        // Cheapest possible path when the mod is running silently, which is the default.
        if (!verbose) return;

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
            if (scanDue) Seen.Add(id);
            TrackHowl(id, s);
        }

        if (!scanDue) return;

        // Forget wolves that have left the controller's list (despawned, died).
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

        s = new State { Anim = anim };
        States[id] = s;
        return s;
    }

    /// <summary>
    /// Detection is sound because <c>Wolf_Default_Idle_Howl</c> is non-looping, so normalizedTime
    /// rises monotonically and a regression means the state was re-entered.
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

    private static void Log(string message)
    {
        if (!Config.Verbose) return;
        Core.Log.Msg($"{message}  [fps={Fps:0}]");
    }
}
