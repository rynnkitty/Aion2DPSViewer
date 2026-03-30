using System.Collections.Generic;

namespace Aion2DPSViewer.Calc;

public class CombatScoreResult
{
    public int Score { get; set; }

    public int CombatPower { get; set; }

    public string ClassName { get; set; } = "";

    public List<DpSkillInfo> DpSkills { get; set; } = new List<DpSkillInfo>();

    public Dictionary<int, string> SkillIcons { get; set; } = new Dictionary<int, string>();

    public bool HasJonggul { get; set; }

    public bool HasNaked { get; set; }
}
