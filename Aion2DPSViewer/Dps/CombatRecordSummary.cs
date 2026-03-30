using System;

namespace Aion2DPSViewer.Dps;

public class CombatRecordSummary
{
    public string Id { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public double ElapsedSeconds { get; set; }
    public long TotalPartyDamage { get; set; }
    public string Target { get; set; } = "";
    public long TargetMaxHp { get; set; }
    public int PlayerCount { get; set; }
}
