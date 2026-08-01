using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace WolfHowlFix;

/// <summary>
/// The fix: correct the runaway accumulator itself.
///
/// THE DEFECT. <c>BaseAi.UpdateScent()</c> maintains <c>m_LostScentTimeSeconds</c>, "how long
/// have I been too far from my target". Its out-of-range branch adds an absolute clock value
/// — hundreds to tens of thousands — instead of <c>Time.deltaTime</c>. One tick beyond the
/// break-stalking range therefore blows past the 30 s <c>m_BreakStalkingTimeSeconds</c>
/// threshold, and <c>ProcessStalking()</c> immediately fires the give-up: howl trigger, revert
/// to the default AI mode. The wolf re-acquires about a second later and repeats, so a 6 s
/// howl clip is re-triggered roughly once per second and never finishes.
///
/// Measured live: 0 -> 53.20 in a single frame; on an untouched wolf in ordinary play the field
/// reached 107,265. Successive give-ups land on values that climb by ~1 per second, which is
/// the game clock being added — the defect visible in the data.
///
/// WHY A POSTFIX WORKS. From the disassembly of ProcessStalking:
///
/// <code>
///   +0x6BA  call   UpdateScent            &lt;- a Harmony postfix returns here
///   +0x6BF  movss  xmm0, [rbx + 0x92C]    &lt;- m_LostScentTimeSeconds, re-read from memory
///   +0x6C7  comiss xmm0, [rbx + 0x918]    &lt;- compared against m_BreakStalkingTimeSeconds
///   +0x6CE  jbe    (skip the give-up)
/// </code>
///
/// The compared value is loaded from the field AFTER the call returns, so a postfix that
/// repairs the field is read by the very next instruction. An earlier note in this project
/// claimed the race was unwinnable; that was true of polling from <c>OnUpdate</c>, not of a
/// postfix. <c>UpdateScent</c> is a real method with exactly one call site (RVA 0x5B9280,
/// called from 0x5B8C8A) — it is not inlined, unlike the howl methods, which is why Harmony
/// can bind it.
///
/// WHAT THIS RESTORES. The intended rule, unchanged: out of range accumulates real time and
/// gives up after 30 seconds. Nothing is retuned. No sense range, break range or any other
/// field is written, so wolves disengage exactly as Hinterland designed and every variant is
/// covered — including timberwolves and wolves that cannot close the distance because terrain
/// is in the way, both of which a break-range workaround misses.
/// </summary>
[HarmonyPatch(typeof(BaseAi), "UpdateScent")]
internal static class Patch_UpdateScent
{
    /// <summary>
    /// Largest believable single-tick increase. Real frames are ~0.017 s and the game never
    /// runs slow enough to legitimately add a whole second, while the bug adds hundreds — so
    /// anything above this is unambiguously the defect and never a valid accumulation.
    /// </summary>
    private const float MaxPlausibleTick = 1f;

    /// <summary>Corrections applied, so the diagnostics can prove the patch actually bound.</summary>
    internal static int Corrections;

    private static void Prefix(BaseAi __instance, out float __state)
    {
        __state = __instance != null ? __instance.m_LostScentTimeSeconds : 0f;
    }

    private static void Postfix(BaseAi __instance, float __state)
    {
        if (!Config.EnableMod || __instance == null) return;

        try
        {
            float now = __instance.m_LostScentTimeSeconds;
            float jump = now - __state;
            if (jump <= MaxPlausibleTick) return;   // ordinary accumulation, leave it alone

            // Replace the garbage increment with the real one. Clamped at zero so a corrupted
            // starting value cannot leave the timer negative.
            float repaired = Mathf.Max(0f, __state) + Time.deltaTime;
            __instance.m_LostScentTimeSeconds = repaired;

            Corrections++;
            if (Config.Verbose && Corrections <= 5)
                Core.Log.Msg($"scent timer repaired: +{jump:0.0} -> +{Time.deltaTime:0.000} " +
                             $"(now {repaired:0.00}s of {__instance.m_BreakStalkingTimeSeconds:0}s)" +
                             (Corrections == 5 ? "  [further corrections not logged]" : ""));
        }
        catch { }
    }
}
