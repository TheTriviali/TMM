namespace TMM
{
    /// <summary>
    /// Single source of truth for the app's display version. The assembly version
    /// defaults to 1.0.0, which would falsely imply a 1.0 release, so the pre-release
    /// label is maintained here explicitly. Update this one constant per release.
    /// </summary>
    public static class AppInfo
    {
        /// <summary>User-facing version label, e.g. "v0.1-alpha-9".</summary>
        public const string DisplayVersion = "v0.1-alpha-9";
    }
}
