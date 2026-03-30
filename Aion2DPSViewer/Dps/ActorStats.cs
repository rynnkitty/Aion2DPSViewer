using System;
using System.Collections.Generic;

namespace Aion2DPSViewer.Dps;

public class ActorStats
{
    public int EntityId { get; set; }
    public string Name { get; set; } = "Unknown";
    public string WhitelistKey { get; set; } = "";
    public bool IsPlayer { get; set; }
    public int ServerId { get; set; } = -1;
    public int JobCode { get; set; } = -1;
    public int CombatScore { get; set; }
    public int CombatPower { get; set; }
    public long TotalDamage { get; set; }
    public int HitCount { get; set; }
    public int CritCount { get; set; }
    public long HealTotal { get; set; }
    public DateTime FirstHit { get; set; } = DateTime.MaxValue;
    public DateTime LastHit { get; set; } = DateTime.MinValue;
    public Dictionary<int, SkillDamage> Skills { get; } = new Dictionary<int, SkillDamage>();
}
