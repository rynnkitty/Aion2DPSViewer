using System.Collections.Generic;

namespace Aion2DPSViewer.Calc;

public class FormulaConfig
{
    public int AccuracyCapMin { get; set; }

    public int AccuracyCapMax { get; set; }

    public double AccuracyMaxDi { get; set; }

    public List<double[]> AccuracyIntervals { get; set; }

    public double CooldownEfficiency { get; set; }

    public double WeaponAmpCoeff { get; set; }

    public double StunResistance { get; set; }

    public double BaseCriticalDamage { get; set; }

    public int TitleCritOwn { get; set; }

    public double CritStatMultiplier { get; set; }

    public double CritStatDivisor { get; set; }

    public double CritChanceCap { get; set; }

    public int BaseMultiHitPct { get; set; }

    public double[] MultiHitPoly { get; set; }

    public FormulaConfig()
    {
        List<double[]> numArrayList = new List<double[]>();
        numArrayList.Add(new double[3] { 1200.0, 1250.0, 1.6 });
        numArrayList.Add(new double[3] { 1250.0, 1300.0, 1.6 });
        numArrayList.Add(new double[3] { 1300.0, 1350.0, 1.5 });
        numArrayList.Add(new double[3] { 1350.0, 1400.0, 1.5 });
        numArrayList.Add(new double[3] { 1400.0, 1450.0, 1.4 });
        numArrayList.Add(new double[3] { 1450.0, 1500.0, 1.4 });
        numArrayList.Add(new double[3] { 1500.0, 1550.0, 1.4 });
        numArrayList.Add(new double[3] { 1550.0, 1600.0, 1.3 });
        numArrayList.Add(new double[3] { 1600.0, 1650.0, 1.3 });
        numArrayList.Add(new double[3] { 1650.0, 1700.0, 1.2 });
        AccuracyIntervals = numArrayList;
        CooldownEfficiency = 0.3;
        WeaponAmpCoeff = 0.66;
        StunResistance = 5.0;
        BaseCriticalDamage = 1.5;
        TitleCritOwn = 80;
        CritStatMultiplier = 0.4;
        CritStatDivisor = 10.0;
        CritChanceCap = 80.0;
        BaseMultiHitPct = 18;
        MultiHitPoly = new double[4] { 11.1, 13.9, 17.8, 23.9 };
    }
}
