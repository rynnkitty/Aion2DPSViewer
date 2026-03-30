using Aion2DPSViewer.Api;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Aion2DPSViewer.Calc;

public static class Supplement
{
    private const int TotalIntPets = 41;
    private const int TotalWildPets = 65;

    public static SupplementResult CalcSupplement(
        JsonElement statData,
        Dictionary<int, JsonElement> itemDetails)
    {
        int num1 = 0;
        int num2 = 0;
        int num3 = 0;
        JsonElement jsonElement1;
        if (statData.ValueKind == JsonValueKind.Object && statData.TryGetProperty("statList", out jsonElement1))
        {
            foreach (JsonElement enumerate in jsonElement1.EnumerateArray())
            {
                string str = enumerate.GetString("type") ?? "";
                int num4 = ParseInt(enumerate, "value");
                if (str == "STR")
                    num1 = num4;
                else if (str == "DEX")
                    num2 = num4;
                else if (str == "INT")
                    num3 = num4;
            }
        }
        int str1 = 0;
        int dex = 0;
        int intStat = 0;
        foreach (JsonElement jsonElement2 in itemDetails.Values)
        {
            AccumulateStats(jsonElement2, "mainStats", ref str1, ref dex, ref intStat, true);
            AccumulateStats(jsonElement2, "subStats", ref str1, ref dex, ref intStat, false);
        }
        int num5 = Math.Max(num1 - str1, 0);
        int num6 = Math.Max(num2 - dex, 0);
        int num7 = Math.Max(num3 - intStat, 0);
        int num8 = Math.Min(num5, 41);
        int num9 = Math.Min(num7, num8);
        return new SupplementResult()
        {
            PurePower = num5,
            PureAgility = num6,
            PureInt = num7,
            IntelligentPetCriticalMin = (num8 - num9) * 2 + num9 * 5,
            IntelligentPetCriticalMax = Math.Max(num8 * 5, 41),
            WildPetAccuracyMin = 0,
            WildPetAccuracyMax = Math.Max(num6 * 5, 65)
        };
    }

    private static void AccumulateStats(
        JsonElement item,
        string arrayName,
        ref int str,
        ref int dex,
        ref int intStat,
        bool skipExceed)
    {
        JsonElement jsonElement1;
        if (!item.TryGetProperty(arrayName, out jsonElement1) || jsonElement1.ValueKind != JsonValueKind.Array)
            return;
        foreach (JsonElement enumerate in jsonElement1.EnumerateArray())
        {
            JsonElement jsonElement2;
            if (!skipExceed || !enumerate.TryGetProperty("exceed", out jsonElement2) || jsonElement2.ValueKind != JsonValueKind.True)
            {
                int num = ParseInt(enumerate, "value") + ParseInt(enumerate, "extra");
                string str1 = enumerate.GetString("id") ?? "";
                string str2 = enumerate.GetString("name") ?? "";
                if (str1 == "STR" || str2 == "위력")
                    str += num;
                else if (str1 == "DEX" || str2 == "민첩")
                    dex += num;
                else if (str1 == "INT" || str2 == "지식")
                    intStat += num;
            }
        }
    }

    private static int ParseInt(JsonElement el, string prop)
    {
        JsonElement jsonElement;
        if (!el.TryGetProperty(prop, out jsonElement))
            return 0;
        if (jsonElement.ValueKind == JsonValueKind.Number)
            return jsonElement.GetInt32();
        int num;
        return jsonElement.ValueKind == JsonValueKind.String && int.TryParse(jsonElement.GetString(), out num) ? num : 0;
    }
}
