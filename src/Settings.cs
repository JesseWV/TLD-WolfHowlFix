using System.Reflection;

namespace WolfHowlFix;

/// <summary>
/// Every ModSettings-typed member in the mod lives in this file, and nothing outside it
/// references <see cref="Settings"/>. That is what makes ModSettings genuinely optional:
/// <see cref="Register"/> is only ever called after <c>Core</c> has confirmed the assembly is
/// loaded, so when it is missing the runtime never has to resolve <c>JsonModSettings</c> and
/// the mod runs on the <see cref="Config"/> defaults instead of failing to load.
///
/// Put another way — if a ModSettings type is referenced anywhere else, the mod stops loading
/// for players who do not have ModSettings installed, and it breaks at class-load time rather
/// than anywhere that points back here.
/// </summary>
internal static class ModSettingsIntegration
{
    internal static void Register()
    {
        var s = Settings.Instance;
        s.AddToModSettings("Wolf Howl Fix");
        s.ApplyVisibility();   // hide the gate and the Diagnostics section up front
        s.Push();              // adopt whatever JsonModSettings just loaded from the JSON

        // Write the file on load so ShowDiagnostics actually appears in it. ModSettings only
        // persists when a setting changes, and this one is never shown in the GUI — so without
        // this the key would never be written and a user would have to know its exact name to
        // add by hand.
        s.Save();
    }
}

/// <summary>
/// In-game settings, shown under Mod Settings so players never have to touch a config file.
///
/// The Diagnostics section is hidden by default. It is only useful for verifying the fix or
/// investigating a regression, and leaving verbose logging on fills the MelonLoader log and
/// costs an Animator query per wolf per frame. To reveal it, hand-edit
/// <c>"ShowDiagnostics": true</c> in <c>Mods/WolfHowlFix.json</c> with the game closed.
/// </summary>
internal sealed class Settings : JsonModSettings
{
    internal static Settings Instance { get; } = new Settings();

    [Section("General")]
    [Name("Enable fix")]
    [Description("Stops wolf howls restarting instead of playing through. " +
                 "Turning this off restores the unpatched behaviour, howl loop included — " +
                 "note that wolves are less dangerous with the bug present, because it " +
                 "prevented them committing to a stalk.")]
    public bool EnableMod = true;

    /// <summary>
    /// Hidden gate. Never shown in the GUI — it exists only to be edited in the JSON.
    /// </summary>
    [Name("Show diagnostics")]
    [Description("Hidden gate — edit the settings JSON to enable.")]
    public bool ShowDiagnostics = false;

    [Section("Diagnostics")]
    [Name("Verbose logging")]
    [Description("Writes to the MelonLoader log: startup, each wolf adjusted, and every howl " +
                 "start, restart and completion. Off means the mod is completely silent. " +
                 "Costs an Animator query per wolf per frame while on.")]
    public bool Verbose = false;

    /// <summary>Copy the values across to the plain fields the fix actually reads.</summary>
    internal void Push()
    {
        Config.EnableMod = EnableMod;
        Config.Verbose = Verbose;
    }

    internal void ApplyVisibility()
    {
        // The gate itself is never visible; it is a JSON-only switch.
        SetFieldVisible(nameof(ShowDiagnostics), false);
        SetFieldVisible(nameof(Verbose), ShowDiagnostics);
        RefreshGUI();
    }

    protected override void OnChange(FieldInfo field, object oldValue, object newValue)
    {
        ApplyVisibility();
        Push();   // take effect immediately rather than waiting for the menu to be confirmed
    }

    protected override void OnConfirm()
    {
        base.OnConfirm();
        Push();
    }
}
