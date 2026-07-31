using System.Reflection;

namespace WolfHowlFix;

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
    [Description("Writes a line to the MelonLoader log for every howl start, restart and " +
                 "completion. Noisy, and costs an Animator query per wolf per frame — " +
                 "only useful for checking the fix is working.")]
    public bool Verbose = false;

    private void ApplyVisibility()
    {
        // The gate itself is never visible; it is a JSON-only switch.
        SetFieldVisible(nameof(ShowDiagnostics), false);
        SetFieldVisible(nameof(Verbose), ShowDiagnostics);
        RefreshGUI();
    }

    protected override void OnChange(FieldInfo field, object oldValue, object newValue)
    {
        ApplyVisibility();
    }

    internal static void OnLoad()
    {
        Instance.AddToModSettings("WolfHowlFix");
        Instance.ApplyVisibility();   // hide the gate and the Diagnostics section up front

        // Write the file on load so ShowDiagnostics actually appears in it. ModSettings only
        // persists when a setting changes, and this one is never shown in the GUI — so without
        // this the key would never be written and a user would have to know its exact name to
        // add by hand.
        Instance.Save();
    }
}
