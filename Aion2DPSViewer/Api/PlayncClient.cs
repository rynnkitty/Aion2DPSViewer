using Aion2DPSViewer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Aion2DPSViewer.Api;

public class PlayncClient
{
    private static readonly HttpClient Http = new HttpClient()
    {
        BaseAddress = new Uri(BaseUrl),
        Timeout = TimeSpan.FromSeconds(10.0)
    };

    private static string BaseUrl => Secrets.PlayncBaseUrl;

    static PlayncClient()
    {
        Http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
        Http.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    private static async Task<JsonElement> GetJson(string path)
    {
        HttpResponseMessage response = await Http.GetAsync(path);
        int statusCode = (int)response.StatusCode;
        if (statusCode >= 400)
            Console.Error.WriteLine($"[api] HTTP {statusCode}: {path}");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    public static async Task<(int race, string charId)?> SearchCharacter(
        string name,
        int serverId,
        int race = 1)
    {
        JsonElement jsonElement1;
        if (!(await GetJson($"/ko-kr/api/search/aion2/search/v2/character?keyword={Uri.EscapeDataString(name)}&race={race}&serverId={serverId}")).TryGetProperty("list", out jsonElement1))
            return null;
        JsonElement? nullable = null;
        foreach (JsonElement enumerate in jsonElement1.EnumerateArray())
        {
            string str = Regex.Replace(enumerate.GetProperty("name").GetString() ?? "", "<[^>]+>", "");
            if (str.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                JsonElement property = enumerate.GetProperty("characterId");
                return (race, property.ValueKind == JsonValueKind.String ? Uri.UnescapeDataString(property.GetString() ?? "") : property.GetRawText());
            }
            if (!nullable.HasValue && str.StartsWith(name, StringComparison.OrdinalIgnoreCase) && str.Length > name.Length)
                nullable = enumerate;
        }
        if (!nullable.HasValue)
            return null;
        JsonElement jsonElement2 = nullable.Value;
        JsonElement property1 = jsonElement2.GetProperty("characterId");
        string str1 = property1.ValueKind == JsonValueKind.String ? Uri.UnescapeDataString(property1.GetString() ?? "") : property1.GetRawText();
        string str2 = Regex.Replace(jsonElement2.GetProperty("name").GetString() ?? "", "<[^>]+>", "");
        Console.Error.WriteLine($"[api] prefix 매칭: \"{name}\" → \"{str2}\"");
        return (race, str1);
    }

    public static Task<JsonElement> FetchInfo(string charId, int serverId)
    {
        return GetJson($"/api/character/info?lang=ko&characterId={Uri.EscapeDataString(charId)}&serverId={serverId}");
    }

    public static Task<JsonElement> FetchEquipment(string charId, int serverId)
    {
        return GetJson($"/api/character/equipment?lang=ko&characterId={Uri.EscapeDataString(charId)}&serverId={serverId}");
    }

    public static Task<JsonElement> FetchItem(
        string itemId,
        int enchant,
        string charId,
        int serverId,
        int slot,
        int exceed = 0)
    {
        string path = $"/api/character/equipment/item?id={Uri.EscapeDataString(itemId)}&enchantLevel={enchant}&characterId={Uri.EscapeDataString(charId)}&serverId={serverId}&slotPos={slot}&lang=ko";
        if (exceed > 0)
            path += $"&exceedLevel={exceed}";
        return GetJson(path);
    }

    public static Task<JsonElement> FetchDaevanion(string charId, int serverId, int boardId)
    {
        return GetJson($"/api/character/daevanion/detail?lang=ko&characterId={Uri.EscapeDataString(charId)}&serverId={serverId}&boardId={boardId}");
    }

    public static async Task<CharacterData?> FetchAll(string name, int serverId, int race = 1)
    {
        (int, string)? nullable;
        if (race == 1)
        {
            (int, string)?[] nullableArray = await Task.WhenAll<(int, string)?>(new Task<(int, string)?>[2]
            {
                SearchCharacter(name, serverId),
                SearchCharacter(name, serverId, 2)
            });
            nullable = nullableArray[0] ?? nullableArray[1];
        }
        else
            nullable = await SearchCharacter(name, serverId, race);
        if (!nullable.HasValue)
            return null;
        string charId = nullable.Value.Item2;
        Task<JsonElement> infoTask = FetchInfo(charId, serverId);
        Task<JsonElement> equipTask = FetchEquipment(charId, serverId);
        JsonElement[] jsonElementArray = await Task.WhenAll<JsonElement>(new Task<JsonElement>[2]
        {
            infoTask,
            equipTask
        });
        JsonElement result1 = infoTask.Result;
        JsonElement result2 = equipTask.Result;
        JsonElement profile = result1.GetProp("profile");
        JsonElement statData = result1.GetProp("stat");
        List<JsonElement> titleList = result1.GetPropArray("title", "titleList");
        List<JsonElement> skillList = result2.GetPropArray("skill", "skillList");
        string wingName = result2.GetProp("petwing").GetProp("wing").GetString("name") ?? "";
        string className = profile.GetString("className") ?? "";
        List<int> intList = new List<int>();
        foreach (JsonElement prop in result1.GetPropArray("daevanion", "boardList"))
        {
            JsonElement jsonElement;
            if (prop.TryGetProperty("id", out jsonElement) && jsonElement.ValueKind == JsonValueKind.Number)
                intList.Add(jsonElement.GetInt32());
        }
        List<JsonElement> propArray = result2.GetPropArray("equipment", "equipmentList");
        List<Task<(int, JsonElement)?>> itemTasks = new List<Task<(int, JsonElement)?>>();
        Dictionary<int, int> slotExceed = new Dictionary<int, int>();
        foreach (JsonElement el in propArray)
        {
            JsonElement jsonElement1;
            JsonElement jsonElement2;
            if (el.TryGetProperty("slotPos", out jsonElement1) && el.TryGetProperty("id", out jsonElement2))
            {
                int num;
                int slot = jsonElement1.ValueKind == JsonValueKind.Number ? jsonElement1.GetInt32() : (int.TryParse(jsonElement1.GetString(), out num) ? num : 0);
                string iid = jsonElement2.ValueKind == JsonValueKind.String ? jsonElement2.GetString() ?? "" : jsonElement2.GetRawText();
                int enc = el.GetInt("enchantLevel");
                int exc = el.GetInt("exceedLevel");
                slotExceed[slot] = exc;
                itemTasks.Add(Task.Run<(int, JsonElement)?>(async () =>
                {
                    try
                    {
                        JsonElement jsonElement3 = await FetchItem(iid, enc, charId, serverId, slot, exc);
                        return (slot, jsonElement3);
                    }
                    catch
                    {
                        return null;
                    }
                }));
            }
        }
        List<Task<(int, JsonElement)?>> daevTasks = intList.Select(bid => Task.Run<(int, JsonElement)?>(async () =>
        {
            try
            {
                JsonElement jsonElement = await FetchDaevanion(charId, serverId, bid);
                return (bid, jsonElement);
            }
            catch
            {
                return null;
            }
        })).ToList();
        (int, JsonElement)?[][] nullableArray1 = await Task.WhenAll<(int, JsonElement)?[]>(new Task<(int, JsonElement)?[]>[2]
        {
            Task.WhenAll<(int, JsonElement)?>((IEnumerable<Task<(int, JsonElement)?>>) itemTasks),
            Task.WhenAll<(int, JsonElement)?>((IEnumerable<Task<(int, JsonElement)?>>) daevTasks)
        });
        Dictionary<int, JsonElement> dictionary1 = new Dictionary<int, JsonElement>();
        foreach (Task<(int, JsonElement)?> task in itemTasks)
        {
            (int, JsonElement)? result3 = task.Result;
            if (result3.HasValue)
                dictionary1[result3.Value.Item1] = result3.Value.Item2;
        }
        Dictionary<int, JsonElement> dictionary2 = new Dictionary<int, JsonElement>();
        foreach (Task<(int, JsonElement)?> task in daevTasks)
        {
            (int, JsonElement)? result4 = task.Result;
            if (result4.HasValue)
                dictionary2[result4.Value.Item1] = result4.Value.Item2;
        }
        return new CharacterData()
        {
            Profile = profile,
            StatData = statData,
            TitleList = titleList,
            SkillList = skillList,
            WingName = wingName,
            ClassName = className,
            ItemDetails = dictionary1,
            DaevanionDetails = dictionary2,
            SlotExceed = slotExceed
        };
    }
}
