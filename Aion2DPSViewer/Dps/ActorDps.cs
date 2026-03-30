using System.Collections.Generic;

namespace Aion2DPSViewer.Dps;

public class ActorDps
{
    public int EntityId { get; set; }
    public string Name { get; set; } = "";
    public int JobCode { get; set; } = -1;
    public int ServerId { get; set; }
    public int CombatScore { get; set; }
    public int CombatPower { get; set; }
    public long TotalDamage { get; set; }
    public long Dps { get; set; }
    public long PartyDps { get; set; }
    public long WallDps { get; set; }
    public double DamagePercent { get; set; }
    public double BossHpPercent { get; set; }
    public double CritRate { get; set; }
    public long HealTotal { get; set; }
    public bool IsUploader { get; set; }
    public List<SkillDps>? TopSkills { get; set; }
    public List<BuffUptimeEntry>? BuffUptime { get; set; }
}
