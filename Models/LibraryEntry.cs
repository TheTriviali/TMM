namespace TMM
{
    /// <summary>
    /// View-model record for a single game card shown in the library.
    /// Built by UnifiedShellWindow from GameProfile / CustomGameProfile data.
    /// </summary>
    public record LibraryEntry(
        /// <summary>Primary key — e.g. "GTA_III_SERIES", "GTA_IV_SERIES", or a CustomGame key.</summary>
        string Key,

        /// <summary>Large text on the card art banner (e.g. "GTA III Series").</summary>
        string DisplayName,

        /// <summary>Subtitle below the name (e.g. "III · Vice City · San Andreas").</summary>
        string Subtitle,

        /// <summary>Gradient start color hex for the card art.</summary>
        string GradientStartHex,

        /// <summary>Gradient end color hex for the card art.</summary>
        string GradientEndHex,

        /// <summary>Status chip shown on card. Release = chip hidden.</summary>
        ReleaseStatus Status,

        /// <summary>Number of installed mods across all GameKeys in this entry.</summary>
        int ModCount,

        /// <summary>True if all required game paths are configured.</summary>
        bool IsReady,

        /// <summary>Human-readable category label, e.g. "GTA Series", "RPG", "Open World".</summary>
        string Category,

        /// <summary>
        /// One or more GameProfile keys this card represents.
        /// GTA III Series = ["III","VC","SA"]; GTA IV Series = ["IV","TLaD","TBoGT"]; custom = [key].
        /// </summary>
        string[] GameKeys,

        /// <summary>True when this is a placeholder alpha game (no real paths or mods).</summary>
        bool IsPlaceholder = false,

        /// <summary>
        /// True when the user has archived this game (hidden from main grid, shown in archive panel).
        /// Persisted via AppSettings.ArchivedGameKeys.
        /// Archived cards show at opacity 0.55 with desaturated gradient.
        /// </summary>
        bool IsArchived = false,

        /// <summary>
        /// True when this is the user's designated "active" game.
        /// Active card shows a filled accent star in its footer bar.
        /// On app close, the active game is saved; on relaunch, the ModManager opens with it.
        /// Only one card can be active at a time.
        /// </summary>
        bool IsActive = false,

        /// <summary>
        /// Backing file basename, e.g. "GTA_III.tmmgame" or "MyGame.json".
        /// Shown in small text on the library card for power-user reference.
        /// Null for placeholder entries.
        /// </summary>
        string? TmmGameFileName = null
    );
}
