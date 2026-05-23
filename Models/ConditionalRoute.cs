namespace TMM
{
    /// <summary>
    /// A deployment routing rule that is evaluated at deploy-time based on
    /// whether a specific sub-folder actually exists in the game directory.
    ///
    /// Example for GTA IV ASI mods:
    ///   Extension    = ".asi"
    ///   CheckSubdir  = "plugins"
    ///   RouteIfExists  = "plugins"   → GTAIV\plugins\mod.asi
    ///   RouteIfMissing = "."         → GTAIV\mod.asi
    /// </summary>
    public record ConditionalRoute(
        string Extension,       // e.g. ".asi"  (lowercase)
        string CheckSubdir,     // sub-folder to probe (e.g. "plugins")
        string RouteIfExists,   // relative output dir when sub-folder is present
        string RouteIfMissing   // relative output dir when sub-folder is absent
    );
}
