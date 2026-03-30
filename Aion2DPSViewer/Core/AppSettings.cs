using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace Aion2DPSViewer.Core;

public class AppSettings
{
    private static readonly string CacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "A2Viewer");
    private static AppSettings? _instance;
    private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static AppSettings Instance
    {
        get => _instance ?? (_instance = Load());
    }

    public bool OverlayOnlyWhenAion { get; set; } = true;

    public string GpuMode { get; set; } = "off";

    [JsonPropertyName("disableGpu")]
    public bool? LegacyDisableGpu
    {
        get => null;
        set
        {
            bool? nullable = value;
            bool flag = false;
            if (!(nullable.GetValueOrDefault() == flag & nullable.HasValue))
                return;
            this.GpuMode = "on";
        }
    }

    public int Opacity { get; set; } = 50;

    public int TextScale { get; set; } = 100;

    public int FontScale { get; set; } = 100;

    public ShortcutSettings Shortcuts { get; set; } = new ShortcutSettings();

    public bool KeepPartyOnRefresh { get; set; } = true;

    public bool KeepSelfOnRefresh { get; set; } = true;

    public bool AutoTabSwitch { get; set; } = true;

    public Dictionary<string, List<string>> TrackedSkills { get; set; } = new Dictionary<string, List<string>>();

    public Dictionary<string, List<string>> KnownDpSkills { get; set; } = GetDefaultDpSkills();

    public int DetailPanelWidth { get; set; } = 900;

    public int DetailPanelHeight { get; set; } = 400;

    public string DpsPercentMode { get; set; } = "party";

    public string ScoreDisplay { get; set; } = "both";

    public string ScoreFormat { get; set; } = "full";

    public string DpsTimeMode { get; set; } = "wallclock";

    public string? ThemeJson { get; set; }

    [JsonIgnore]
    public WindowState WindowState { get; set; } = new WindowState();

    private static AppSettings Load()
    {
        Directory.CreateDirectory(CacheDir);
        AppSettings data = LoadJson<AppSettings>("app_settings.json") ?? new AppSettings();
        WindowState windowState = LoadJson<WindowState>("window_state.json");
        if (windowState != null)
        {
            if (windowState.X <= -9000 || windowState.Y <= -9000)
            {
                windowState.X = -1;
                windowState.Y = 20;
            }
            if (windowState.X == -1)
            {
                Screen primaryScreen = Screen.PrimaryScreen;
                windowState.X = (primaryScreen != null ? primaryScreen.WorkingArea.Width : 1920) - windowState.Width - 20;
            }
            data.WindowState = windowState;
        }
        else
        {
            Screen primaryScreen = Screen.PrimaryScreen;
            data.WindowState.X = (primaryScreen != null ? primaryScreen.WorkingArea.Width : 1920) - 340;
        }
        SaveJson<AppSettings>("app_settings.json", data);
        foreach (KeyValuePair<string, List<string>> defaultDpSkill in GetDefaultDpSkills())
        {
            var str2 = defaultDpSkill.Key;
            var stringList2 = defaultDpSkill.Value;
            if (!data.KnownDpSkills.ContainsKey(str2))
            {
                data.KnownDpSkills[str2] = stringList2;
            }
            else
            {
                HashSet<string> stringSet = new HashSet<string>(data.KnownDpSkills[str2]);
                foreach (string str3 in stringList2)
                    stringSet.Add(str3);
                data.KnownDpSkills[str2] = new List<string>(stringSet);
            }
        }
        return data;
    }

    public void Save()
    {
        SaveJson<AppSettings>("app_settings.json", this);
        SaveJson<WindowState>("window_state.json", this.WindowState);
    }

    private static T? LoadJson<T>(string filename) where T : class
    {
        string str = Path.Combine(CacheDir, filename);
        if (!File.Exists(str))
            return default(T);
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(str), JsonOpts);
        }
        catch
        {
            return default(T);
        }
    }

    private static void SaveJson<T>(string filename, T data)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            string str = JsonSerializer.Serialize<T>(data, JsonOpts);
            File.WriteAllText(Path.Combine(CacheDir, filename), str);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[settings] save error: " + ex.Message);
        }
    }

    private static Dictionary<string, List<string>> GetDefaultDpSkills()
    {
        Dictionary<string, List<string>> defaultDpSkills = new Dictionary<string, List<string>>();
        defaultDpSkills["검성"] = new List<string> { "분노의 파동", "돌격 자세", "지켈의 축복", "집중 막기", "균형의 갑옷", "칼날 날리기", "근성", "흡혈의 검", "격노 폭발", "파동의 갑주", "강제 결박", "강습 일격" };
        defaultDpSkills["궁성"] = new List<string> { "화살 폭풍", "바이젤의 권능", "축복의 활", "기습 차기", "결박의 덫", "수면 화살", "은신", "봉인 화살", "대자연의 숨결", "그리폰 화살", "폭발 화살", "강습 강타" };
        defaultDpSkills["마도성"] = new List<string> { "신성 폭발", "강철 보호막", "원소 강화", "저주: 나무", "빙설의 갑주", "영혼 동결", "냉기 폭풍", "불의 장벽", "루미엘의 공간", "지연 폭발", "빙하 강타", "강습 폭격" };
        defaultDpSkills["살성"] = new List<string> { "맹수의 송곳니", "신속의 계약", "연막탄", "회피 자세", "나선 베기", "그림자 보행", "암검 투척", "트리니엘의 비수", "공중 포박", "환영 분신", "회피의 계약", "강습 습격" };
        defaultDpSkills["수호성"] = new List<string> { "주신의 징벌", "네자칸의 방패", "보호의 방패", "도발", "균형의 갑옷", "이중 갑옷", "파멸의 방패", "고결의 갑주", "처형의 검", "전우 보호", "나포", "강습 맹격" };
        defaultDpSkills["정령성"] = new List<string> { "협공: 파멸의 공세", "강화: 정령의 가호", "불길의 축복", "소환: 고대의 정령", "협공: 부식", "카이시넬의 권능", "흡인", "공포의 절규", "저주의 구름", "마법 강탈", "마법 차단", "강습 공포" };
        defaultDpSkills["치유성"] = new List<string> { "권능 폭발", "면죄", "치유의 기운", "증폭의 기도", "소환 부활", "대지의 징벌", "구원", "속박", "보호의 빛", "유스티엘의 권능", "파멸의 목소리", "강습 낙인" };
        defaultDpSkills["호법성"] = new List<string> { "멸화", "불패의 진언", "집중 방어", "질주의 진언", "분쇄격", "마르쿠탄의 분노", "차단의 권능", "결박의 낙인", "쾌유의 손길", "질풍의 권능", "수호의 축복", "강습 충격" };
        return defaultDpSkills;
    }
}
