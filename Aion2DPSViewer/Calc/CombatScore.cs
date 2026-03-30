using Aion2DPSViewer.Api;
using Aion2DPSViewer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aion2DPSViewer.Calc;

public class CombatScore
{
    private static readonly HashSet<int> EquipmentSlots;
    private static readonly HashSet<int> AccessorySlots;
    private static readonly HashSet<int> ArcanaSlots;
    private static readonly Dictionary<string, string> ArcanaTypeMap;

    public static async Task<CombatScoreResult?> QueryCombatScore(
        int serverId,
        string name,
        int race = 1)
    {
        CharacterData data = await PlayncClient.FetchAll(name, serverId, race);
        if (data == null)
            return null;
        string calcJs = FileCache.ReadRaw("calc_js_cache.js");
        if (calcJs == null)
            throw new Exception("calc_js 로드 실패");
        FormulaConfig cfg = LoadFormulaConfig();
        SupplementResult supplement = Supplement.CalcSupplement(data.StatData, data.ItemDetails);
        JsonElement? skillPriorities = LoadSkillPriorities(data.ClassName);
        ExtractedStats stats = ExtractStats(CalcEngine.RunCalc(calcJs, BuildJsInput(data, supplement, skillPriorities)));
        double di = AccToDi(stats.AccMax, cfg);
        double num1 = CalcCritChance(stats.CritBreakdown);
        Dictionary<string, double> dictionary1 = new Dictionary<string, double>();
        if (stats.CombatSpeed > 0.0)
            dictionary1["combatSpeed"] = stats.CombatSpeed;
        if (stats.WeaponAmp > 0.0)
            dictionary1["weaponDamageAmp"] = stats.WeaponAmp * cfg.WeaponAmpCoeff;
        double num2 = stats.PveAmp + stats.NormalAmp;
        if (num2 > 0.0)
            dictionary1["damageAmp"] = num2;
        if (num1 > 0.0)
        {
            double num3 = num1 / 100.0;
            dictionary1["criticalDamageAmp"] = (1.0 - num3 + num3 * (cfg.BaseCriticalDamage + stats.CritAmp / 100.0) - 1.0) * 100.0;
        }
        if (stats.SkillDmg > 0.0)
            dictionary1["skillDamage"] = stats.SkillDmg;
        if (stats.Cooldown > 0.0)
            dictionary1["cooldownReduction"] = (100.0 / (100.0 - stats.Cooldown) - 1.0) * 100.0 * cfg.CooldownEfficiency;
        if (stats.StunHit > 0.0)
            dictionary1["stunHit"] = Math.Max(0.0, stats.StunHit - cfg.StunResistance);
        if (stats.Perfect > 0.0 && stats.Wmin > 0.0 && stats.Wmax > stats.Wmin)
            dictionary1["perfect"] = stats.Perfect * (stats.Wmax - stats.Wmin) / (stats.Wmax + stats.Wmin);
        if (stats.MultiHit > 0.0)
        {
            double[] poly = cfg.MultiHitPoly;
            double x1 = (double)cfg.BaseMultiHitPct / 100.0;
            double x2 = ((double)cfg.BaseMultiHitPct + stats.MultiHit) / 100.0;
            dictionary1["multiHit"] = ((1.0 + Fn(x2) / 100.0) / (1.0 + Fn(x1) / 100.0) - 1.0) * 100.0;

            double Fn(double x)
            {
                return poly[0] * x + poly[1] * Math.Pow(x, 2.0) + poly[2] * Math.Pow(x, 3.0) + poly[3] * Math.Pow(x, 4.0);
            }
        }
        if (di > 0.0)
            dictionary1["accuracy"] = di;
        double num4 = dictionary1.Values.Aggregate(1.0, (acc, v) => acc * (1.0 + v / 100.0));
        int num5 = (int)Math.Round(stats.Attack * num4);
        List<DpSkillInfo> list = data.SkillList
            .Where(s => s.GetString("category") == "Dp")
            .Select(s => new DpSkillInfo()
            {
                Name = s.GetString("skillName") ?? s.GetString("name") ?? "",
                Level = s.GetInt("skillLevel")
            })
            .Where(s => !string.IsNullOrEmpty(s.Name))
            .ToList();
        Dictionary<int, string> dictionary2 = new Dictionary<int, string>();
        foreach (JsonElement skill in data.SkillList)
        {
            int num6 = skill.GetInt("id");
            string str = skill.GetString("icon") ?? "";
            if (num6 > 0 && !string.IsNullOrEmpty(str))
                if (!dictionary2.ContainsKey(num6))
                    dictionary2[num6] = str;
        }
        bool flag1 = data.TitleList.Any(t => (t.GetString("name") ?? "").Contains("종족의 굴레"));
        bool flag2 = data.TitleList.Any(t => (t.GetString("name") ?? "").Contains("입을 옷이 없네"));
        int num7 = data.Profile.GetInt("combatPower");
        return new CombatScoreResult()
        {
            Score = num5,
            CombatPower = num7,
            ClassName = data.ClassName,
            DpSkills = list,
            SkillIcons = dictionary2,
            HasJonggul = flag1,
            HasNaked = flag2
        };
    }

    private static CalcInput BuildJsInput(
        CharacterData data,
        SupplementResult supplement,
        JsonElement? skillPriorities)
    {
        List<object> objectList1 = new List<object>();
        List<object> objectList2 = new List<object>();
        Dictionary<string, int> dictionary1 = new Dictionary<string, int>()
        {
            ["magic"] = 0,
            ["vitality"] = 0,
            ["purity"] = 0,
            ["frenzy"] = 0
        };
        foreach (KeyValuePair<int, JsonElement> itemDetail in data.ItemDetails)
        {
            int slot = itemDetail.Key;
            JsonElement el = itemDetail.Value;
            int valueOrDefault = data.SlotExceed.TryGetValue(slot, out int exceedVal) ? exceedVal : 0;
            object obj = AdaptItem(el, slot, valueOrDefault);
            if (EquipmentSlots.Contains(slot) || ArcanaSlots.Contains(slot))
                objectList1.Add(obj);
            else if (AccessorySlots.Contains(slot))
                objectList2.Add(obj);
            if (ArcanaSlots.Contains(slot))
            {
                string str1 = el.GetString("name") ?? "";
                foreach (KeyValuePair<string, string> arcanaType in ArcanaTypeMap)
                {
                    string key = arcanaType.Key;
                    string val = arcanaType.Value;
                    if (str1.Contains(key))
                    {
                        dictionary1[val]++;
                        break;
                    }
                }
            }
        }
        List<object> objectList3 = new List<object>();
        List<object> objectList4 = new List<object>();
        foreach (JsonElement skill in data.SkillList)
        {
            string str = skill.GetString("category") ?? "";
            object obj = AdaptSkill(skill);
            if (str == "Active" || str == "Passive")
                objectList3.Add(obj);
            else if (str == "Dp")
                objectList4.Add(obj);
        }
        string str6 = "null";
        if (data.DaevanionDetails.Count > 0)
        {
            int num3 = data.DaevanionDetails.Keys.Min() - 41;
            Dictionary<int, JsonElement> dictionary3 = new Dictionary<int, JsonElement>();
            foreach (KeyValuePair<int, JsonElement> daevanionDetail in data.DaevanionDetails)
            {
                int key = daevanionDetail.Key;
                JsonElement jsonElement2 = daevanionDetail.Value;
                dictionary3[key - num3] = jsonElement2;
            }
            str6 = JsonSerializer.Serialize<Dictionary<int, JsonElement>>(dictionary3);
        }
        return new CalcInput()
        {
            EquipmentJson = JsonSerializer.Serialize<List<object>>(objectList1),
            AccessoriesJson = JsonSerializer.Serialize<List<object>>(objectList2),
            StatDataJson = data.StatData.GetRawText(),
            DaevanionDataJson = str6,
            TitlesJson = JsonSerializer.Serialize<IEnumerable<object>>(data.TitleList.Select(t => AdaptTitle(t))),
            WingName = data.WingName,
            SkillsJson = JsonSerializer.Serialize<List<object>>(objectList3),
            StigmasJson = JsonSerializer.Serialize<List<object>>(objectList4),
            JobName = data.ClassName,
            CharacterDataJson = JsonSerializer.Serialize(new
            {
                pure_power = supplement.PurePower,
                pure_agility = supplement.PureAgility,
                intelligent_pet_critical_min = supplement.IntelligentPetCriticalMin,
                intelligent_pet_critical_max = supplement.IntelligentPetCriticalMax,
                wild_pet_accuracy_min = supplement.WildPetAccuracyMin,
                wild_pet_accuracy_max = supplement.WildPetAccuracyMax
            }),
            SkillPrioritiesJson = skillPriorities?.GetRawText() ?? "null",
            ArcanaSetCounts = dictionary1
        };
    }

    private static object AdaptItem(JsonElement item, int slot, int exceedLevel)
    {
        return new
        {
            slotPos = slot,
            name = (item.GetString("name") ?? ""),
            enchantLevel = item.GetInt("enchantLevel"),
            enhance_level = item.GetInt("enchantLevel"),
            exceedLevel = exceedLevel,
            exceed_level = exceedLevel,
            main_stats = AdaptStatArray(item, "mainStats"),
            sub_stats = AdaptStatArray(item, "subStats"),
            magic_stone_stat = AdaptStatArray(item, "magicStoneStat")
        };
    }

    private static List<object> AdaptStatArray(JsonElement item, string prop)
    {
        List<object> objectList = new List<object>();
        JsonElement jsonElement1;
        if (!item.TryGetProperty(prop, out jsonElement1) || jsonElement1.ValueKind != JsonValueKind.Array)
            return objectList;
        foreach (JsonElement enumerate in jsonElement1.EnumerateArray())
        {
            JsonElement jsonElement2;
            objectList.Add(new
            {
                id = (enumerate.GetString("id") ?? ""),
                name = (enumerate.GetString("name") ?? ""),
                value = GetStatValue(enumerate, "value"),
                extra = GetStatValue(enumerate, "extra"),
                minValue = GetStatValue(enumerate, "minValue"),
                exceed = (enumerate.TryGetProperty("exceed", out jsonElement2) && jsonElement2.ValueKind == JsonValueKind.True)
            });
        }
        return objectList;
    }

    private static object GetStatValue(JsonElement el, string prop)
    {
        JsonElement jsonElement;
        if (!el.TryGetProperty(prop, out jsonElement))
            return 0;
        if (jsonElement.ValueKind == JsonValueKind.Number)
        {
            int num;
            return jsonElement.TryGetInt32(out num) ? (object)num : jsonElement.GetDouble();
        }
        if (jsonElement.ValueKind != JsonValueKind.String)
            return 0;
        string str = jsonElement.GetString() ?? "";
        int num1;
        return int.TryParse(str, out num1) ? (object)num1 : str;
    }

    private static object AdaptSkill(JsonElement s)
    {
        int num = s.GetInt("skillLevel");
        if (num == 0)
            num = s.GetInt("level_int");
        return new
        {
            name = (s.GetString("skillName") ?? s.GetString("name") ?? ""),
            skillName = (s.GetString("skillName") ?? s.GetString("name") ?? ""),
            category = (s.GetString("category") ?? ""),
            level_int = num,
            level = num.ToString(),
            group = (s.GetString("category") ?? "Active").ToLower(),
            skillLevel = num
        };
    }

    private static object AdaptTitle(JsonElement t)
    {
        List<string> stringList = new List<string>();
        JsonElement jsonElement;
        if (t.TryGetProperty("equipStatList", out jsonElement) && jsonElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement enumerate in jsonElement.EnumerateArray())
            {
                string str = enumerate.GetString("desc");
                if (!string.IsNullOrEmpty(str))
                    stringList.Add(str);
            }
        }
        return new
        {
            name = (t.GetString("name") ?? ""),
            equip_effects = stringList
        };
    }

    private static ExtractedStats ExtractStats(CalcResult r)
    {
        double num = GetDouble(r.AttackPower, "finalAttack");
        if (r.IsAttackPowerOverCap && r.CappedAttackPower.HasValue)
            num = r.CappedAttackPower.Value;
        ExtractedStats stats = new ExtractedStats();
        stats.Attack = num;
        stats.Wmin = r.WeaponMinAttack;
        stats.Wmax = r.WeaponMaxAttack;
        stats.CombatSpeed = GetDouble(r.CombatSpeed, "totalCombatSpeed");
        stats.WeaponAmp = GetDouble(GetSub(r.DamageAmplification, "weaponDamageAmp"), "totalPercent");
        stats.PveAmp = GetDouble(GetSub(r.DamageAmplification, "pveDamageAmp"), "totalPercent");
        stats.NormalAmp = GetDouble(GetSub(r.DamageAmplification, "damageAmp"), "totalPercent");
        stats.CritAmp = GetDouble(GetSub(r.DamageAmplification, "criticalDamageAmp"), "totalPercent");
        JsonElement jsonElement1 = r.CriticalHit;
        JsonElement jsonElement2;
        if (jsonElement1.ValueKind != JsonValueKind.Object)
        {
            jsonElement2 = new JsonElement();
        }
        else
            jsonElement2 = GetSub(r.CriticalHit, "breakdown");
        stats.CritBreakdown = jsonElement2;
        stats.SkillDmg = GetDouble(r.SkillDamage, "totalSkillDamage");
        stats.Cooldown = GetDouble(r.CooldownReduction, "totalCooldownReduction");
        stats.StunHit = GetDouble(r.StunHit, "totalStunHitPercent");
        stats.Perfect = GetDouble(r.Perfect, "totalPerfectPercent");
        stats.MultiHit = GetDouble(r.MultiHit, "totalMultiHitPercent");
        stats.AccMax = GetDouble(r.Accuracy, "finalAccuracyMax") > 0.0 ? GetDouble(r.Accuracy, "finalAccuracyMax") : GetDouble(r.Accuracy, "totalIntegerAccuracyMax");
        return stats;

        static double GetDouble(JsonElement el, string prop)
        {
            JsonElement jsonElement;
            return el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(prop, out jsonElement) || jsonElement.ValueKind != JsonValueKind.Number ? 0.0 : jsonElement.GetDouble();
        }

        static JsonElement GetSub(JsonElement el, string prop)
        {
            if (el.ValueKind != JsonValueKind.Object)
                return new JsonElement();
            JsonElement jsonElement;
            return !el.TryGetProperty(prop, out jsonElement) ? new JsonElement() : jsonElement;
        }
    }

    private static double CalcCritChance(JsonElement breakdown)
    {
        if (breakdown.ValueKind != JsonValueKind.Object)
            return 0.0;
        double num1 = Get("baseCriticalHitInteger") + Get("soulCriticalHitInteger") + Get("stoneCriticalHitInteger") + Get("daevanionCriticalHitInteger") + Get("wingCriticalHitInteger") + Get("titleEquipCriticalHit");
        double num2 = Get("intelligentPetCriticalMax");
        if (num2 == 0.0)
            num2 = 41.0;
        double num3 = num2 + 80.0;
        return Math.Min(Math.Round((num1 + num3) * (1.0 + (Get("deathCriticalHitPercent") + Get("accuracyCriticalHitPercent")) / 100.0)) * 0.4 / 10.0, 80.0);

        double Get(string prop)
        {
            JsonElement jsonElement;
            return breakdown.TryGetProperty(prop, out jsonElement) && jsonElement.ValueKind == JsonValueKind.Number ? jsonElement.GetDouble() : 0.0;
        }
    }

    private static double AccToDi(double acc, FormulaConfig cfg)
    {
        if (acc <= (double)cfg.AccuracyCapMin)
            return 0.0;
        if (acc >= (double)cfg.AccuracyCapMax)
            return cfg.AccuracyMaxDi;
        double di = 0.0;
        foreach (double[] accuracyInterval in cfg.AccuracyIntervals)
        {
            double num1 = accuracyInterval[0];
            double num2 = accuracyInterval[1];
            double num3 = accuracyInterval[2];
            if (acc > num1)
            {
                if (acc >= num2)
                {
                    di += num3;
                }
                else
                {
                    di += num3 * (acc - num1) / (num2 - num1);
                    break;
                }
            }
            else
                break;
        }
        return di;
    }

    private static FormulaConfig LoadFormulaConfig()
    {
        try
        {
            JsonElement? nullable = FileCache.LoadCache("formula_cache.json");
            if (nullable.HasValue)
                return JsonSerializer.Deserialize<FormulaConfig>(nullable.Value.GetRawText()) ?? new FormulaConfig();
        }
        catch
        {
        }
        return new FormulaConfig();
    }

    private static JsonElement? LoadSkillPriorities(string job)
    {
        JsonElement? nullable = FileCache.LoadCache("skill_priorities_cache.json");
        JsonElement jsonElement;
        return nullable.HasValue && nullable.Value.TryGetProperty(job, out jsonElement) ? jsonElement : (JsonElement?)null;
    }

    static CombatScore()
    {
        EquipmentSlots = new HashSet<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 17, 19 };
        AccessorySlots = new HashSet<int> { 10, 11, 12, 13, 14, 15, 16, 22, 23, 24 };
        ArcanaSlots = new HashSet<int> { 41, 42, 43, 44, 45, 46 };
        ArcanaTypeMap = new Dictionary<string, string>()
        {
            ["마법"] = "magic",
            ["활력"] = "vitality",
            ["순수"] = "purity",
            ["광분"] = "frenzy"
        };
    }

    private class ExtractedStats
    {
        public double Attack;
        public double Wmin;
        public double Wmax;
        public double CombatSpeed;
        public double WeaponAmp;
        public double PveAmp;
        public double NormalAmp;
        public double CritAmp;
        public double SkillDmg;
        public double Cooldown;
        public double StunHit;
        public double Perfect;
        public double MultiHit;
        public double AccMax;
        public JsonElement CritBreakdown;
    }
}
