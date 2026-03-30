using Aion2DPSViewer.Calc;
using System.Collections.Generic;

namespace Aion2DPSViewer.Api;

internal class ScoreCacheEntry
{
    public int Score { get; set; }

    public int CombatPower { get; set; }

    public string? ClassName { get; set; }

    public List<DpSkillInfo>? DpSkills { get; set; }

    public bool HasJonggul { get; set; }

    public bool HasNaked { get; set; }

    public static ScoreCacheEntry FromResult(CombatScoreResult r)
    {
        return new ScoreCacheEntry()
        {
            Score = r.Score,
            CombatPower = r.CombatPower,
            ClassName = r.ClassName,
            DpSkills = r.DpSkills,
            HasJonggul = r.HasJonggul,
            HasNaked = r.HasNaked
        };
    }
}
