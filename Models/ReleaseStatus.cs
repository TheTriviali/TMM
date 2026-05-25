namespace TMM
{
    /// <summary>Publication/maturity status shown on library game cards.</summary>
    public enum ReleaseStatus
    {
        Release,   // No chip shown
        Beta,      // Yellow chip
        Alpha,     // Orange chip
        PreAlpha,  // Red-orange chip — for bundled placeholder/stub game profiles
        Testing    // Blue chip
    }
}
