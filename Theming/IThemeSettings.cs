namespace TGTAMM
{
    /// <summary>
    /// Theming-relevant slice of application settings.
    /// ThemeEngine depends only on this interface — not on any app-specific settings model.
    /// </summary>
    public interface IThemeSettings
    {
        string AccentColor        { get; }
        string BgColor            { get; }
        string ColorMode          { get; }
        string TextColorMode      { get; }
        bool   TitlebarPersonalize { get; }
        string FontFamily         { get; }
        bool   MicaEnabled        { get; }
    }
}
