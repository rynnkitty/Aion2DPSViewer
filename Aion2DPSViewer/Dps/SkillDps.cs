namespace Aion2DPSViewer.Dps;

public class SkillDps
{
    public string Name { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public string? SkillType { get; set; }
    public int[]? Specs { get; set; }
    public long TotalDamage { get; set; }
    public double Percent { get; set; }
    public int HitCount { get; set; }
    public int NormalCount { get; set; }
    public int CritCount { get; set; }
    public int BackCount { get; set; }
    public int HardHitCount { get; set; }
    public int PerfectCount { get; set; }
    public int MultiHitCount { get; set; }
    public int BlockCount { get; set; }
    public int EvadeCount { get; set; }
    public long Dps { get; set; }
    public long PartyDps { get; set; }
    public long WallDps { get; set; }
    public long MinDamage { get; set; }
    public long MaxDamage { get; set; }
    public long AvgDamage { get; set; }
}
