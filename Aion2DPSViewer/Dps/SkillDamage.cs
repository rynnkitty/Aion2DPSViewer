namespace Aion2DPSViewer.Dps;

public class SkillDamage
{
    public int SkillCode { get; set; }
    public string SkillName { get; set; } = "";
    public int[]? Specs { get; set; }
    public long TotalDamage { get; set; }
    public int HitCount { get; set; }
    public int NormalCount { get; set; }
    public int CritCount { get; set; }
    public int BackCount { get; set; }
    public int HardHitCount { get; set; }
    public int PerfectCount { get; set; }
    public int MultiHitCount { get; set; }
    public int BlockCount { get; set; }
    public int EvadeCount { get; set; }
    public long MinDamage { get; set; } = long.MaxValue;
    public long MaxDamage { get; set; }
}
