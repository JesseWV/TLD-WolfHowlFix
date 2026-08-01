using System;
using System.Linq;
using MelonLoader;
using WolfHowlFix;

[assembly: MelonInfo(typeof(Core), "WolfHowlFix", "2.0.0", "Lycanthor")]
[assembly: MelonGame("Hinterland", "TheLongDark")]
[assembly: MelonOptionalDependencies("ModSettings")]

namespace WolfHowlFix;

/// <summary>
/// Fixes wolf howls restarting instead of playing through (TLD 2.55).
///
/// A wolf howls when it gives up a stalk. A runaway accumulator in
/// <c>BaseAi.UpdateScent()</c> makes the give-up test pass on the first tick of every stalk,
/// so the wolf gives up, howls, reverts, re-acquires a second later and repeats — re-firing
/// the howl once per second against a 6 s clip that therefore never finishes.
/// </summary>
public class Core : MelonMod
{
    internal static Core Instance { get; private set; }
    internal static MelonLogger.Instance Log => Instance.LoggerInstance;

    public override void OnInitializeMelon()
    {
        Instance = this;

        // ModSettings is an optional dependency. Detect it at runtime and only then touch any
        // ModSettings-typed code, so the mod still loads — and stays enabled, on the Config
        // defaults — when ModSettings is not installed.
        bool modSettingsPresent = AppDomain.CurrentDomain.GetAssemblies()
            .Any(a => a.GetName().Name == "ModSettings");

        if (modSettingsPresent)
        {
            ModSettingsIntegration.Register();
        }
        else
        {
            // Worth one line even though the mod is otherwise silent: without it, a player
            // looking for the settings page has no way to tell that the fix is running fine
            // and only the toggle is missing.
            LoggerInstance.Msg("ModSettings not detected — fix enabled by default. " +
                               "Install ModSettings for an in-game on/off toggle.");
        }

        // Verify the Harmony patch actually bound. IL2CPP methods that get inlined bind
        // silently and never fire — that happened to the howl methods early in this project —
        // so a mod that looks fine but does nothing is a real failure mode. UpdateScent is not
        // inlined (one call site, from ProcessStalking), but check rather than assume.
        var target = typeof(Il2Cpp.BaseAi).GetMethod("UpdateScent",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Public);
        if (target == null)
            LoggerInstance.Error("BaseAi.UpdateScent not found — the fix is NOT active. " +
                                 "The game version is probably newer than this mod supports.");
        else if (Config.Verbose)
            LoggerInstance.Msg($"ready (enabled={Config.EnableMod}); patched BaseAi.UpdateScent");
    }

    public override void OnUpdate() => HowlWatch.Tick();

    // The fix adjusts a field on live wolves, so put it back on the way out rather than
    // leaving it altered for the rest of the session.
    public override void OnSceneWasUnloaded(int buildIndex, string sceneName) => HowlWatch.RestoreAll();

    public override void OnApplicationQuit() => HowlWatch.RestoreAll();
}
