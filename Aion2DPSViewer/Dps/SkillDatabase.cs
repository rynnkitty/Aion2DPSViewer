using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Aion2DPSViewer.Dps;

public class SkillDatabase
{
    private readonly Dictionary<int, string> _skills = new Dictionary<int, string>();
    private readonly Dictionary<int, SkillInfo> _skillInfo = new Dictionary<int, SkillInfo>();
    private readonly Dictionary<int, string> _buffs = new Dictionary<int, string>();
    private readonly Dictionary<int, MobData> _mobs = new Dictionary<int, MobData>();
    private readonly ConcurrentDictionary<int, IntPtr> _skillNamePtrs = new ConcurrentDictionary<int, IntPtr>();
    private static readonly (uint Min, uint Max)[] SkillRanges = new (uint, uint)[]
    {
        (11000000U, 20000000U),
        (1000000U, 10000000U),
        (100000U, 200000U),
        (29000000U, 30000000U)
    };

    public SkillDatabase()
    {
        if (!LoadSkillsDb("skills_db.json"))
            LoadSkillsLegacy("skills_ko.json");
        LoadBuffs("buffs_ko.json");
        LoadMobs("mobs.json");
    }

    private static Stream? GetResource(string name)
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
    }

    private bool LoadSkillsDb(string resourceName)
    {
        try
        {
            using (Stream? resource = GetResource(resourceName))
            {
                if (resource == null)
                    return false;
                using (StreamReader streamReader = new StreamReader(resource))
                {
                    Dictionary<string, JsonElement>? dictionary = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(streamReader.ReadToEnd());
                    if (dictionary == null)
                        return false;
                    foreach (KeyValuePair<string, JsonElement> keyValuePair in dictionary)
                    {
                        string str2 = keyValuePair.Key;
                        JsonElement jsonElement2 = keyValuePair.Value;
                        if (int.TryParse(str2, out int num))
                        {
                            JsonElement jsonElement3;
                            string Name = jsonElement2.TryGetProperty("name", out jsonElement3) ? jsonElement3.GetString() ?? "" : "";
                            _skills[num] = Name;
                            JsonElement jsonElement4;
                            string? Icon = jsonElement2.TryGetProperty("icon", out jsonElement4) ? jsonElement4.GetString() : null;
                            JsonElement jsonElement5;
                            string? Type = jsonElement2.TryGetProperty("type", out jsonElement5) ? jsonElement5.GetString() : null;
                            JsonElement jsonElement6;
                            string? Job = jsonElement2.TryGetProperty("job", out jsonElement6) ? jsonElement6.GetString() : null;
                            _skillInfo[num] = new SkillInfo(Name, Icon, Type, Job);
                        }
                    }
                    Console.Error.WriteLine($"[dps] {_skills.Count}개 스킬 로드 (skills_db)");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dps] skills_db 로드 실패: " + ex.Message);
            return false;
        }
    }

    private void LoadSkillsLegacy(string resourceName)
    {
        try
        {
            using (Stream? resource = GetResource(resourceName))
            {
                if (resource == null)
                {
                    Console.Error.WriteLine($"[dps] {resourceName} 리소스 없음");
                }
                else
                {
                    using (StreamReader streamReader = new StreamReader(resource))
                    {
                        Dictionary<string, string>? dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(streamReader.ReadToEnd());
                        if (dictionary == null)
                            return;
                        foreach (KeyValuePair<string, string> keyValuePair in dictionary)
                        {
                            if (int.TryParse(keyValuePair.Key, out int num))
                                _skills[num] = keyValuePair.Value;
                        }
                        Console.Error.WriteLine($"[dps] {_skills.Count}개 스킬 로드 (legacy)");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dps] 스킬 로드 실패: " + ex.Message);
        }
    }

    private void LoadBuffs(string resourceName)
    {
        try
        {
            using (Stream? resource = GetResource(resourceName))
            {
                if (resource == null)
                {
                    Console.Error.WriteLine($"[dps] {resourceName} 리소스 없음");
                }
                else
                {
                    using (StreamReader streamReader = new StreamReader(resource))
                    {
                        Dictionary<string, JsonElement>? dictionary = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(streamReader.ReadToEnd());
                        if (dictionary == null)
                            return;
                        foreach (KeyValuePair<string, JsonElement> keyValuePair in dictionary)
                        {
                            if (int.TryParse(keyValuePair.Key, out int num))
                            {
                                JsonElement jsonElement3;
                                string str3 = keyValuePair.Value.TryGetProperty("name", out jsonElement3) ? jsonElement3.GetString() ?? "" : "";
                                _buffs[num] = str3;
                            }
                        }
                        Console.Error.WriteLine($"[dps] {_buffs.Count}개 버프 로드 (buffs_ko)");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dps] 버프 로드 실패: " + ex.Message);
        }
    }

    private void LoadMobs(string resourceName)
    {
        try
        {
            using (Stream? resource = GetResource(resourceName))
            {
                if (resource == null)
                {
                    Console.Error.WriteLine($"[dps] {resourceName} 리소스 없음");
                }
                else
                {
                    using (StreamReader streamReader = new StreamReader(resource))
                    {
                        Dictionary<string, JsonElement>? dictionary = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(streamReader.ReadToEnd());
                        if (dictionary == null)
                            return;
                        foreach (KeyValuePair<string, JsonElement> keyValuePair in dictionary)
                        {
                            if (int.TryParse(keyValuePair.Key, out int num))
                            {
                                JsonElement jsonElement3;
                                string Name = keyValuePair.Value.TryGetProperty("name", out jsonElement3) ? jsonElement3.GetString() ?? "" : "";
                                JsonElement jsonElement4;
                                bool IsBoss = keyValuePair.Value.TryGetProperty("isBoss", out jsonElement4) && jsonElement4.GetBoolean();
                                _mobs[num] = new MobData(Name, IsBoss);
                            }
                        }
                        Console.Error.WriteLine($"[dps] {_mobs.Count}개 몹 로드");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dps] 몹 로드 실패: " + ex.Message);
        }
    }

    public string? GetSkillName(int skillCode)
    {
        return !_skills.TryGetValue(skillCode, out string? str) ? null : str;
    }

    public string? GetBuffName(int buffId)
    {
        return !_buffs.TryGetValue(buffId, out string? str) ? null : str;
    }

    public bool ContainsSkillCode(int skillCode) => _skills.ContainsKey(skillCode);

    public string? GetSkillIcon(int skillCode)
    {
        if (_skillInfo.TryGetValue(skillCode, out SkillInfo? skillInfo1) && skillInfo1.Icon != null)
            return skillInfo1.Icon;
        return _skillInfo.TryGetValue(skillCode / 10000 * 10000, out SkillInfo? skillInfo2) && skillInfo2.Icon != null ? skillInfo2.Icon : null;
    }

    public string? GetSkillType(int skillCode)
    {
        if (_skillInfo.TryGetValue(skillCode, out SkillInfo? skillInfo1) && skillInfo1.Type != null)
            return skillInfo1.Type;
        return _skillInfo.TryGetValue(skillCode / 10000 * 10000, out SkillInfo? skillInfo2) && skillInfo2.Type != null ? skillInfo2.Type : null;
    }

    public SkillInfo? GetSkillInfo(int skillCode)
    {
        if (_skillInfo.TryGetValue(skillCode, out SkillInfo? skillInfo1))
            return skillInfo1;
        return _skillInfo.TryGetValue(skillCode / 10000 * 10000, out SkillInfo? skillInfo2) ? skillInfo2 : null;
    }

    public bool IsMobBoss(int mobCode)
    {
        return _mobs.TryGetValue(mobCode, out MobData? mobData) && mobData.IsBoss;
    }

    public string GetMobName(int mobCode)
    {
        return !_mobs.TryGetValue(mobCode, out MobData? mobData) ? "" : mobData.Name;
    }

    public int ResolveFromPacketBytes(byte[] data, ref int pos, int end)
    {
        LastRawSkillCode = 0;
        for (int index = 0; index < 7 && pos + index + 4 <= end; ++index)
        {
            int int32 = BitConverter.ToInt32(data, pos + index);
            if ((uint)int32 < 2147483648U)
            {
                int num1 = ResolveRawSkillValue(int32);
                if (num1 != 0)
                {
                    pos += index + 5;
                    return num1;
                }
                if (int32 > 0 && int32 % 100 == 0)
                {
                    int num2 = ResolveRawSkillValue(int32 / 100);
                    if (num2 != 0)
                    {
                        pos += index + 5;
                        return num2;
                    }
                }
            }
        }
        return 0;
    }

    private int ResolveRawSkillValue(int baseVal)
    {
        if (baseVal <= 0)
            return 0;
        long num1 = (long)baseVal * 10L + 1L;
        if (num1 > 0L && num1 < 2147483648L && ContainsSkillCode((int)num1))
        {
            LastRawSkillCode = (int)num1;
            int baseSkill = NormalizeToBaseSkill((int)num1);
            if (IsSkillCodeInRange(baseSkill))
                return baseSkill;
        }
        long num2 = (long)baseVal * 10L;
        if (num2 > 0L && num2 < 2147483648L && ContainsSkillCode((int)num2))
        {
            LastRawSkillCode = (int)num2;
            int baseSkill = NormalizeToBaseSkill((int)num2);
            if (IsSkillCodeInRange(baseSkill))
                return baseSkill;
        }
        int baseSkill1 = NormalizeToBaseSkill(baseVal);
        if (!IsSkillCodeInRange(baseSkill1))
            return 0;
        if (LastRawSkillCode == 0)
            LastRawSkillCode = baseVal;
        return baseSkill1;
    }

    internal int LastRawSkillCode { get; private set; }

    private int NormalizeToBaseSkill(int code)
    {
        if (code < 29000000 || code >= 30000000)
        {
            int skillCode = code / 10000 * 10000;
            if (skillCode != code && ContainsSkillCode(skillCode))
            {
                if (!ContainsSkillCode(code))
                    return skillCode;
                string? skillName1 = GetSkillName(skillCode);
                string? skillName2 = GetSkillName(code);
                if (skillName1 != null && skillName2 != null && skillName1 == skillName2)
                    return skillCode;
            }
        }
        return code;
    }

    public static int[]? DecodeSpecializations(int rawCode, int baseCode)
    {
        int num1 = (rawCode - baseCode) / 10;
        if (num1 <= 0 || num1 > 999)
            return null;
        List<int> intList = new List<int>(3);
        for (; num1 > 0; num1 /= 10)
        {
            int num2 = num1 % 10;
            if (num2 < 1 || num2 > 5)
                return null;
            intList.Add(num2);
        }
        if (intList.Count == 0)
            return null;
        for (int index = 1; index < intList.Count; ++index)
        {
            if (intList[index] >= intList[index - 1])
                return null;
        }
        intList.Sort();
        return intList.ToArray();
    }

    public static bool IsSkillCodeInRange(int code)
    {
        uint num = (uint)code;
        foreach ((uint Min, uint Max) range in SkillRanges)
        {
            if (num >= range.Min && num < range.Max)
                return true;
        }
        return false;
    }

    public IntPtr GetSkillNameCallback(int skillCode, IntPtr _1)
    {
        return !_skills.TryGetValue(skillCode, out string? name) ? IntPtr.Zero : _skillNamePtrs.GetOrAdd(skillCode, _2 => Marshal.StringToCoTaskMemUTF8(name));
    }

    public int ContainsSkillCodeCallback(int skillCode, IntPtr _) => _skills.ContainsKey(skillCode) ? 1 : 0;

    public int IsMobBossCallback(int mobCode, IntPtr _) => IsMobBoss(mobCode) ? 1 : 0;

    public void Dispose()
    {
        foreach (IntPtr ptr in _skillNamePtrs.Values)
            Marshal.FreeCoTaskMem(ptr);
        _skillNamePtrs.Clear();
    }

    private record MobData(string Name, bool IsBoss);

    public record SkillInfo(string Name, string? Icon, string? Type, string? Job);
}
