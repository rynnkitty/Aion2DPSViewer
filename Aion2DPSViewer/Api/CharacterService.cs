using Aion2DPSViewer.Calc;
using Aion2DPSViewer.Dps;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Aion2DPSViewer.Api;

internal class CharacterService
{
    private readonly Dictionary<string, ScoreCacheEntry> _cache = new Dictionary<string, ScoreCacheEntry>();
    private readonly Action<string, object?> _sendToJs;
    private readonly Action<Action> _invokeOnUi;
    private readonly Func<int> _getEpoch;

    public DpsMeter? DpsMeter { get; set; }

    public CharacterService(
        Action<string, object?> sendToJs,
        Action<Action> invokeOnUi,
        Func<int> getEpoch)
    {
        _sendToJs = sendToJs;
        _invokeOnUi = invokeOnUi;
        _getEpoch = getEpoch;
    }

    public void ClearCache()
    {
        lock (_cache)
            _cache.Clear();
    }

    public void SendCharacterLoading(string nickname, int? serverId, string? serverName, string type)
    {
        if (serverId.HasValue)
        {
            string str = $"{nickname}:{serverId}";
            lock (_cache)
            {
                ScoreCacheEntry scoreCacheEntry;
                if (_cache.TryGetValue(str, out scoreCacheEntry))
                {
                    Console.Error.WriteLine($"[char] {type}: {nickname} → 캐시 히트 (AT={scoreCacheEntry.Score} CP={scoreCacheEntry.CombatPower})");
                    _sendToJs("character", new
                    {
                        nickname = nickname,
                        server_id = serverId,
                        server_name = serverName,
                        type = type,
                        loading = false,
                        error = false,
                        score = (object)new
                        {
                            score = scoreCacheEntry.Score,
                            combatPower = scoreCacheEntry.CombatPower,
                            @class = scoreCacheEntry.ClassName,
                            dpSkills = ToDpSkillList(scoreCacheEntry.DpSkills),
                            hasJonggul = scoreCacheEntry.HasJonggul,
                            hasNaked = scoreCacheEntry.HasNaked
                        }
                    });
                    return;
                }
            }
        }
        Console.Error.WriteLine($"[char] {type}: {nickname} (서버: {serverName ?? "?"}/{serverId}) → 점수 조회 시작");
        _sendToJs("character", new
        {
            nickname = nickname,
            server_id = serverId,
            server_name = serverName,
            type = type,
            score = (object?)null,
            loading = true
        });
        QueryAsync(nickname, serverId, serverName ?? "", type);
    }

    private void QueryAsync(string nickname, int? serverId, string serverName, string type)
    {
        if (!serverId.HasValue)
            return;
        string cacheKey = $"{nickname}:{serverId}";
        int epoch = _getEpoch();
        Task.Run((Func<Task>)(async () =>
        {
            try
            {
                CombatScoreResult result = await CombatScore.QueryCombatScore(serverId.Value, nickname);
                if (_getEpoch() != epoch)
                {
                    Console.Error.WriteLine($"[score] {nickname} 결과 무시 (파티 해산)");
                    return;
                }
                if (result != null)
                    Console.Error.WriteLine($"[score] {nickname} → {result.ClassName} CP{result.Score}");
                else
                    Console.Error.WriteLine($"[score] {nickname} → null (서버: {serverName}/{serverId}, 닉hex: {BitConverter.ToString(Encoding.UTF8.GetBytes(nickname))})");
                if (result != null)
                {
                    lock (_cache)
                        _cache[cacheKey] = ScoreCacheEntry.FromResult(result);
                }
                if (result != null && serverId.HasValue)
                {
                    DpsMeter?.UpdateActorInfo(nickname, serverId.Value, result.ClassName, result.Score, result.CombatPower);
                    if (result.SkillIcons.Count > 0)
                        DpsMeter?.UpdateSkillIcons(result.SkillIcons);
                }
                _invokeOnUi(() => _sendToJs("character", new
                {
                    nickname = nickname,
                    server_id = serverId,
                    server_name = serverName,
                    type = type,
                    loading = false,
                    error = false,
                    score = (result != null ? (object)new
                    {
                        score = result.Score,
                        combatPower = result.CombatPower,
                        @class = result.ClassName,
                        dpSkills = ToDpSkillList(result.DpSkills),
                        hasJonggul = result.HasJonggul,
                        hasNaked = result.HasNaked
                    } : null)
                }));
            }
            catch (Exception ex)
            {
                if (_getEpoch() != epoch)
                    return;
                string errMsg = !string.IsNullOrEmpty(ex.Message) ? ex.Message : ex.InnerException?.Message ?? ex.GetType().Name;
                Console.Error.WriteLine($"[score] {nickname} 조회 실패: {errMsg}");
                object? fallbackScore = null;
                if (DpsMeter != null && serverId.HasValue)
                {
                    try
                    {
                        (int score2, int combatPower2) = await DpsMeter.GetCacheFallbackAsync(nickname, serverId.Value);
                        if (score2 > 0 || combatPower2 > 0)
                        {
                            Console.Error.WriteLine($"[score] {nickname} cache fallback: AT={score2} CP={combatPower2}");
                            DpsMeter.UpdateActorInfo(nickname, serverId.Value, null, score2, combatPower2);
                            fallbackScore = new
                            {
                                score = score2,
                                combatPower = combatPower2,
                                @class = "",
                                dpSkills = Array.Empty<object>(),
                                hasJonggul = false,
                                hasNaked = false
                            };
                            lock (_cache)
                                _cache[cacheKey] = new ScoreCacheEntry()
                                {
                                    Score = score2,
                                    CombatPower = combatPower2,
                                    ClassName = "",
                                    DpSkills = null,
                                    HasJonggul = false,
                                    HasNaked = false
                                };
                        }
                    }
                    catch
                    {
                    }
                }
                string errorType = ex is HttpRequestException ? "server" : "unknown";
                _invokeOnUi(() => _sendToJs("character", new
                {
                    nickname = nickname,
                    server_id = serverId,
                    server_name = serverName,
                    type = type,
                    loading = false,
                    error = (fallbackScore == null),
                    errorType = (fallbackScore != null ? null : errorType),
                    errorMessage = (fallbackScore != null ? null : errMsg),
                    score = fallbackScore
                }));
            }
        }));
    }

    private static List<object> ToDpSkillList(List<DpSkillInfo>? skills)
    {
        List<object> dpSkillList = new List<object>();
        if (skills == null)
            return dpSkillList;
        foreach (DpSkillInfo skill in skills)
            dpSkillList.Add(new
            {
                name = skill.Name,
                level = skill.Level
            });
        return dpSkillList;
    }
}
