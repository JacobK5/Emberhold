namespace Emberhold.Game;

/// <summary>A permanent horde-wide buff adopted after each chapter boss falls.</summary>
public enum DoctrineKind { Swift, Phalanx, Berserkers, FrostWard, Siegecraft }

/// <summary>
/// War Doctrines: every 10th wave cleared, the horde adopts one doctrine — a
/// permanent rule that stacks with the others for the rest of the run. The order
/// is a deterministic per-run shuffle (salted), so a resume keeps the same ladder
/// and each run climbs a different one. Pressure keeps rising even when the fort
/// is maxed, and each doctrine asks for a different counter.
/// </summary>
public static class Doctrines
{
    public static readonly DoctrineKind[] All =
        { DoctrineKind.Swift, DoctrineKind.Phalanx, DoctrineKind.Berserkers, DoctrineKind.FrostWard, DoctrineKind.Siegecraft };

    public static string Name(DoctrineKind d) => d switch
    {
        DoctrineKind.Swift => "SWIFT DOCTRINE",
        DoctrineKind.Phalanx => "PHALANX DOCTRINE",
        DoctrineKind.Berserkers => "BERSERKER DOCTRINE",
        DoctrineKind.FrostWard => "FROST WARD",
        DoctrineKind.Siegecraft => "SIEGECRAFT",
        _ => "DOCTRINE",
    };

    public static string Blurb(DoctrineKind d) => d switch
    {
        DoctrineKind.Swift => "raiders +10% speed",
        DoctrineKind.Phalanx => "raiders +12% HP",
        DoctrineKind.Berserkers => "raiders +15% damage",
        DoctrineKind.FrostWard => "slows 30% less effective",
        DoctrineKind.Siegecraft => "+25% damage to structures",
        _ => "",
    };

    /// <summary>Short label for HUD chips ("Swift", "Phalanx", ...).</summary>
    public static string Short(DoctrineKind d) => d switch
    {
        DoctrineKind.Swift => "Swift",
        DoctrineKind.Phalanx => "Phalanx",
        DoctrineKind.Berserkers => "Berserk",
        DoctrineKind.FrostWard => "Frostward",
        DoctrineKind.Siegecraft => "Siegecraft",
        _ => "?",
    };

    /// <summary>The index-th doctrine of this run's deterministic salted ladder.</summary>
    public static DoctrineKind Roll(int salt, int index)
    {
        var order = (DoctrineKind[])All.Clone();
        var rng = new Random(salt * 31 + 17);
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        return order[Math.Clamp(index, 0, order.Length - 1)];
    }

    // ---- stacked effects (read by spawn + combat systems) -----------------

    public static float SpeedMult(IReadOnlyCollection<DoctrineKind> owned)
        => owned.Contains(DoctrineKind.Swift) ? 1.10f : 1f;

    public static float HpMult(IReadOnlyCollection<DoctrineKind> owned)
        => owned.Contains(DoctrineKind.Phalanx) ? 1.12f : 1f;

    public static float DamageMult(IReadOnlyCollection<DoctrineKind> owned)
        => owned.Contains(DoctrineKind.Berserkers) ? 1.15f : 1f;

    /// <summary>Weakens an effective slow factor (1 = no slow) under Frost Ward.</summary>
    public static float ApplySlowResist(IReadOnlyCollection<DoctrineKind> owned, float speedScale)
        => owned.Contains(DoctrineKind.FrostWard) ? 1f - (1f - speedScale) * 0.7f : speedScale;

    public static float StructureDamageMult(IReadOnlyCollection<DoctrineKind> owned)
        => owned.Contains(DoctrineKind.Siegecraft) ? 1.25f : 1f;
}
