namespace TMM
{
    /// <summary>
    /// Detected state of a game executable.
    /// Unknown = not found / unrecognised hash.
    /// Vanilla = Steam DRM exe present (cannot run in virtual mode).
    /// Downgraded = confirmed 1.0 build via MD5 (safe to deploy and launch).
    /// </summary>
    public enum ExeStatus { Unknown, Vanilla, Downgraded }
}
