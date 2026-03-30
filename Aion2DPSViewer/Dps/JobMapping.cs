using System.Collections.Generic;

namespace Aion2DPSViewer.Dps;

public static class JobMapping
{
    public static readonly Dictionary<string, int> NameToCode = new Dictionary<string, int>()
    {
        ["검성"] = 0, ["궁성"] = 1, ["마도성"] = 2, ["살성"] = 3,
        ["수호성"] = 4, ["정령성"] = 5, ["치유성"] = 6, ["호법성"] = 7
    };
    public static readonly Dictionary<int, int> GameToUi = new Dictionary<int, int>()
    {
        [7] = 0, [8] = 0, [15] = 1, [16] = 1, [28] = 2, [19] = 3, [20] = 3,
        [11] = 4, [12] = 4, [23] = 5, [24] = 5, [27] = 5, [31] = 6, [32] = 6, [35] = 7, [36] = 7
    };
    public static readonly Dictionary<int, int> SkillPrefixToJob = new Dictionary<int, int>()
    {
        [11] = 0, [12] = 4, [13] = 3, [14] = 1, [15] = 2, [16] = 5, [17] = 6, [18] = 7
    };
    public static readonly Dictionary<int, string> GameToName = new Dictionary<int, string>()
    {
        [7] = "검성", [8] = "검성", [11] = "수호성", [12] = "수호성",
        [15] = "궁성", [16] = "궁성", [19] = "살성", [20] = "살성",
        [23] = "정령성", [24] = "정령성", [27] = "정령성", [28] = "마도성",
        [31] = "치유성", [32] = "치유성", [35] = "호법성", [36] = "호법성"
    };
}
