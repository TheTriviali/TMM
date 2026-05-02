namespace TGTAMM
{
    /// <summary>
    /// Progress payload reported by long-running deploy/clone operations.
    /// </summary>
    /// <param name="Stage">Human-readable stage label (e.g. "Cloning vanilla files...").</param>
    /// <param name="Current">Items processed so far (0 if not applicable).</param>
    /// <param name="Total">Total items expected (0 if unknown / indeterminate).</param>
    public readonly record struct DeploymentProgress(string Stage, int Current, int Total);
}
