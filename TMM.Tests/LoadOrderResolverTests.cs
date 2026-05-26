namespace TMM.Tests;

/// <summary>
/// Integration tests for <see cref="LoadOrderResolver.ResolveFinalLoadOrders"/>.
///
/// The resolver runs in two phases:
///   1. Topological sort — satisfies LoadAfter / LoadBefore constraints.
///   2. Bias grouping   — Lower-biased mods first, Higher-biased mods last.
///
/// FinalLoadOrder values are spread across 0–255 using the formula:
///   step = count == 1 ? 0 : 255 / (count - 1)
///   FinalLoadOrder[i] = min(255, i * step)
///
/// Coverage:
///   • Single mod → assigned 0.
///   • Two mods → 0 and 255.
///   • Three mods → spread (0, 127, 254) due to integer division.
///   • LoadAfter constraint respected in topological order.
///   • LoadBefore constraint respected in topological order.
///   • Cycle detected → all mods still assigned (no crash).
///   • Lower-biased mods move to front regardless of topo order.
///   • Higher-biased mods move to back regardless of topo order.
///   • Mixed bias group order: Lower → None → Higher.
///   • Topological constraint preserved within the same bias group.
/// </summary>
public class LoadOrderResolverTests
{
    private static ModItem Mod(string name, LoadOrderBias bias = LoadOrderBias.None,
                               string? loadAfter = null, string? loadBefore = null)
        => new() { Name = name, IsEnabled = true, LoadOrderBias = bias, LoadAfter = loadAfter, LoadBefore = loadBefore };

    private static int Order(ModItem m) => m.FinalLoadOrder;

    // ── Spread arithmetic ─────────────────────────────────────────────────────

    [Fact]
    public void ResolveFinalLoadOrders_SingleMod_AssignedZero()
    {
        var mods = new List<ModItem> { Mod("A") };
        new LoadOrderResolver().ResolveFinalLoadOrders(mods);

        Assert.Equal(0, mods[0].FinalLoadOrder);
    }

    [Fact]
    public void ResolveFinalLoadOrders_TwoMods_AssignedZeroAndTwoFiftyFive()
    {
        var mods = new List<ModItem> { Mod("A"), Mod("B") };
        new LoadOrderResolver().ResolveFinalLoadOrders(mods);

        // step = 255/(2-1) = 255 → [0, 255]
        Assert.Equal(0,   mods[0].FinalLoadOrder);
        Assert.Equal(255, mods[1].FinalLoadOrder);
    }

    [Fact]
    public void ResolveFinalLoadOrders_ThreeMods_EvenlySpreadWithIntegerDivision()
    {
        var mods = new List<ModItem> { Mod("A"), Mod("B"), Mod("C") };
        new LoadOrderResolver().ResolveFinalLoadOrders(mods);

        // step = 255/(3-1) = 127  →  [0, 127, min(255, 254)] = [0, 127, 254]
        Assert.Equal(0,   mods[0].FinalLoadOrder);
        Assert.Equal(127, mods[1].FinalLoadOrder);
        Assert.Equal(254, mods[2].FinalLoadOrder);
    }

    [Fact]
    public void ResolveFinalLoadOrders_AllValuesInZeroTo255Range()
    {
        var mods = Enumerable.Range(0, 10).Select(i => Mod($"Mod{i}")).ToList();
        new LoadOrderResolver().ResolveFinalLoadOrders(mods);

        Assert.All(mods, m => Assert.InRange(m.FinalLoadOrder, 0, 255));
    }

    [Fact]
    public void ResolveFinalLoadOrders_EmptyList_DoesNotThrow()
    {
        // Must be a no-op with no exception.
        var mods = new List<ModItem>();
        new LoadOrderResolver().ResolveFinalLoadOrders(mods);   // should not throw
    }

    // ── LoadAfter / LoadBefore ────────────────────────────────────────────────

    [Fact]
    public void ResolveFinalLoadOrders_LoadAfter_ConstrainedModComesLater()
    {
        // B.LoadAfter = "A"  →  A must be deployed before B.
        var a = Mod("A");
        var b = Mod("B", loadAfter: "A");
        var mods = new List<ModItem> { a, b };

        new LoadOrderResolver().ResolveFinalLoadOrders(mods);

        Assert.True(Order(a) < Order(b),
            $"Expected A ({Order(a)}) < B ({Order(b)}) because B LoadAfter A.");
    }

    [Fact]
    public void ResolveFinalLoadOrders_LoadBefore_ConstrainedModComesEarlier()
    {
        // A.LoadBefore = "B"  →  A must be deployed before B.
        var a = Mod("A", loadBefore: "B");
        var b = Mod("B");
        var mods = new List<ModItem> { b, a };   // deliberately reversed in input

        new LoadOrderResolver().ResolveFinalLoadOrders(mods);

        Assert.True(Order(a) < Order(b),
            $"Expected A ({Order(a)}) < B ({Order(b)}) because A LoadBefore B.");
    }

    [Fact]
    public void ResolveFinalLoadOrders_ChainedConstraints_OrderPreserved()
    {
        // A → B → C  (each loads after the previous)
        var a = Mod("A");
        var b = Mod("B", loadAfter: "A");
        var c = Mod("C", loadAfter: "B");
        var mods = new List<ModItem> { c, b, a };   // reversed input

        new LoadOrderResolver().ResolveFinalLoadOrders(mods);

        Assert.True(Order(a) < Order(b), "A must precede B");
        Assert.True(Order(b) < Order(c), "B must precede C");
    }

    // ── Cycle detection ───────────────────────────────────────────────────────

    [Fact]
    public void ResolveFinalLoadOrders_MutualLoadAfterCycle_AllModsAssigned()
    {
        // A.LoadAfter = "B" and B.LoadAfter = "A" → cycle; both must still appear.
        var a = Mod("A", loadAfter: "B");
        var b = Mod("B", loadAfter: "A");
        var mods = new List<ModItem> { a, b };

        new LoadOrderResolver().ResolveFinalLoadOrders(mods);   // must not throw

        // Both mods must receive a valid FinalLoadOrder in [0, 255]
        Assert.InRange(a.FinalLoadOrder, 0, 255);
        Assert.InRange(b.FinalLoadOrder, 0, 255);
    }

    // ── Bias grouping ─────────────────────────────────────────────────────────

    [Fact]
    public void ResolveFinalLoadOrders_LowerBias_AppearsBeforeUnbiasedMods()
    {
        var normal = Mod("Normal", LoadOrderBias.None);
        var low    = Mod("Low",    LoadOrderBias.Lower);
        var mods   = new List<ModItem> { normal, low };

        new LoadOrderResolver().ResolveFinalLoadOrders(mods);

        Assert.True(Order(low) < Order(normal),
            $"Lower-biased mod ({Order(low)}) should come before unbiased mod ({Order(normal)}).");
    }

    [Fact]
    public void ResolveFinalLoadOrders_HigherBias_AppearsAfterUnbiasedMods()
    {
        var normal = Mod("Normal", LoadOrderBias.None);
        var high   = Mod("High",   LoadOrderBias.Higher);
        var mods   = new List<ModItem> { high, normal };   // reversed input

        new LoadOrderResolver().ResolveFinalLoadOrders(mods);

        Assert.True(Order(high) > Order(normal),
            $"Higher-biased mod ({Order(high)}) should come after unbiased mod ({Order(normal)}).");
    }

    [Fact]
    public void ResolveFinalLoadOrders_MixedBias_OrderIsLowerNoneHigher()
    {
        // Regardless of input order: Lower group < None group < Higher group.
        var low    = Mod("Low",    LoadOrderBias.Lower);
        var middle = Mod("Middle", LoadOrderBias.None);
        var high   = Mod("High",   LoadOrderBias.Higher);
        // Deliberately scrambled
        var mods = new List<ModItem> { high, middle, low };

        new LoadOrderResolver().ResolveFinalLoadOrders(mods);

        Assert.True(Order(low)    < Order(middle), $"Lower ({Order(low)}) < None ({Order(middle)})");
        Assert.True(Order(middle) < Order(high),   $"None ({Order(middle)}) < Higher ({Order(high)})");
    }

    // ── Interaction: topology + bias ─────────────────────────────────────────

    [Fact]
    public void ResolveFinalLoadOrders_LoadAfterWithBias_TopologicalOrderWithinBiasGroup()
    {
        // A and B are both unbiased. B.LoadAfter = "A" → A before B within None group.
        // C is Lower-biased → C first overall.
        var a = Mod("A", LoadOrderBias.None);
        var b = Mod("B", LoadOrderBias.None, loadAfter: "A");
        var c = Mod("C", LoadOrderBias.Lower);
        // Input order scrambled: B, C, A
        var mods = new List<ModItem> { b, c, a };

        new LoadOrderResolver().ResolveFinalLoadOrders(mods);

        // C (Lower) must precede all None mods
        Assert.True(Order(c) < Order(a), $"Lower ({Order(c)}) must precede A ({Order(a)})");
        Assert.True(Order(c) < Order(b), $"Lower ({Order(c)}) must precede B ({Order(b)})");
        // Within the None group, topological order A → B must hold
        Assert.True(Order(a) < Order(b), $"A ({Order(a)}) must precede B ({Order(b)}) (LoadAfter)");
    }
}
