namespace WolfHowlFix;

/// <summary>
/// The live settings the fix actually reads.
///
/// Deliberately a plain static class with no ModSettings types anywhere in it. ModSettings is
/// an optional dependency: when it is absent, <see cref="ModSettingsIntegration"/> is never
/// touched, nothing tries to resolve the missing assembly, and these defaults stand — so the
/// mod runs fixed-on with logging off, which is the sensible unattended configuration.
///
/// When ModSettings IS present it pushes the user's values in here, and the hot path is none
/// the wiser.
/// </summary>
internal static class Config
{
    /// <summary>Whether the fix is applied. On by default so the mod works with no setup.</summary>
    internal static bool EnableMod = true;

    /// <summary>Verbose logging. Off by default — the mod is silent unless asked otherwise.</summary>
    internal static bool Verbose;
}
