using System;
using System.Collections.Generic;

namespace Aion2DPSViewer.Dps;

public class CombatRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public double ElapsedSeconds { get; set; }
    public long TotalPartyDamage { get; set; }
    public string Target { get; set; } = "";
    public long TargetMaxHp { get; set; }
    public List<ActorDps> Players { get; set; } = new List<ActorDps>();
    public List<TimelineEntry>? Timeline { get; set; }
    public List<HitLogEntry>? HitLog { get; set; }
    public List<BuffUptimeEntry>? BossDebuffs { get; set; }
    public int? DungeonId { get; set; }
}
