using MelonLoader;
using WolfHowlFix;

[assembly: MelonInfo(typeof(Core), "WolfHowlFix", "1.0.0", "Lycanthor")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

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
        Settings.OnLoad();
        // Silent unless the user has opted into logging. MelonLoader already announces
        // the mod name and version at load, so this line is pure duplication otherwise.
        if (Settings.Instance.Verbose)
            LoggerInstance.Msg($"ready (enabled={Settings.Instance.EnableMod})");
    }

    public override void OnUpdate() => HowlWatch.Tick();

    // The fix adjusts a field on live wolves, so put it back on the way out rather than
    // leaving it altered for the rest of the session.
    public override void OnSceneWasUnloaded(int buildIndex, string sceneName) => HowlWatch.RestoreAll();

    public override void OnApplicationQuit() => HowlWatch.RestoreAll();
}
