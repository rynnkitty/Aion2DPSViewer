using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Aion2DPSViewer.Dps;

public sealed class BuffTracker
{
    private readonly ConcurrentDictionary<int, EntityBuffState> _entities = new ConcurrentDictionary<int, EntityBuffState>();
    private readonly ConcurrentDictionary<int, EntityBuffState> _casterBuffs = new ConcurrentDictionary<int, EntityBuffState>();
    private readonly SkillDatabase _db;
    private const uint PERMANENT_DURATION = 0xFFFFFFFF;
    private const uint MAX_REASONABLE_DURATION_MS = 3600000;

    public BuffTracker(SkillDatabase db) => _db = db;

    private static int BuffIdToSkillCode(int buffId)
    {
        return buffId >= 100000000 && buffId <= 999999999 ? buffId / 10 : buffId;
    }

    private int ResolveSkillCode(int buffId)
    {
        if (_db.GetSkillName(buffId) != null)
            return buffId;
        int skillCode1 = BuffIdToSkillCode(buffId);
        if (skillCode1 != buffId)
        {
            if (_db.GetSkillName(skillCode1) != null)
                return skillCode1;
            int skillCode2 = skillCode1 / 10000 * 10000;
            if (_db.GetSkillName(skillCode2) != null)
                return skillCode2;
        }
        return -1;
    }

    private string ResolveBuffName(int buffId)
    {
        string? buffName = _db.GetBuffName(buffId);
        if (buffName != null)
            return buffName;
        int skillCode = ResolveSkillCode(buffId);
        if (skillCode >= 0)
            return _db.GetSkillName(skillCode) ?? $"Buff#{(uint)buffId}";
        return $"Buff#{(uint)buffId}";
    }

    private string ResolveBuffIcon(int buffId)
    {
        int skillCode = ResolveSkillCode(buffId);
        return skillCode >= 0 ? _db.GetSkillIcon(skillCode) ?? "" : "";
    }

    public void Track(int entityId, int buffId, int type, uint durationMs, int casterId = 0)
    {
        if (durationMs == uint.MaxValue || durationMs == 0U || durationMs > 3600000U)
            return;
        string? buffName = _db.GetBuffName(buffId);
        if (buffName == null && ResolveSkillCode(buffId) < 0 || (buffName ?? ResolveBuffName(buffId)).Contains("테스트"))
            return;
        DateTime utcNow = DateTime.UtcNow;
        double durationSec = (double)durationMs / 1000.0;
        TrackInDict(_entities, entityId, buffId, utcNow, durationSec, casterId);
        if (casterId == 0)
            return;
        TrackInDict(_casterBuffs, casterId, buffId, utcNow, durationSec, casterId);
    }

    private static void TrackInDict(
        ConcurrentDictionary<int, EntityBuffState> dict,
        int key,
        int buffId,
        DateTime now,
        double durationSec,
        int casterId)
    {
        EntityBuffState orAdd = dict.GetOrAdd(key, _ => new EntityBuffState());
        if (orAdd.Buffs.TryGetValue(buffId, out BuffInfo buffInfo))
        {
            if (now >= buffInfo.ExpiresAt)
            {
                buffInfo.AccumulatedSec += (buffInfo.ExpiresAt - buffInfo.StartedAt).TotalSeconds;
                buffInfo.StartedAt = now;
            }
            buffInfo.ExpiresAt = now + TimeSpan.FromSeconds(durationSec);
            if (casterId == 0)
                return;
            buffInfo.CasterId = casterId;
        }
        else
        {
            orAdd.Buffs[buffId] = new BuffInfo()
            {
                StartedAt = now,
                ExpiresAt = now + TimeSpan.FromSeconds(durationSec),
                AccumulatedSec = 0.0,
                CasterId = casterId
            };
        }
    }

    public List<BuffUptimeEntry> GetUptime(int entityId, double elapsedSec)
    {
        return BuildUptimeList(_entities, entityId, elapsedSec);
    }

    private List<BuffUptimeEntry> BuildUptimeList(
        ConcurrentDictionary<int, EntityBuffState> dict,
        int key,
        double elapsedSec)
    {
        if (!dict.TryGetValue(key, out EntityBuffState entityBuffState) || elapsedSec <= 0.0)
            return new List<BuffUptimeEntry>();
        DateTime utcNow = DateTime.UtcNow;
        List<BuffUptimeEntry> buffUptimeEntryList = new List<BuffUptimeEntry>();
        foreach (KeyValuePair<int, BuffInfo> buff in entityBuffState.Buffs)
        {
            int buffId = buff.Key;
            BuffInfo buffInfo2 = buff.Value;
            double accumulatedSec = buffInfo2.AccumulatedSec;
            double num2;
            if (utcNow < buffInfo2.ExpiresAt)
                num2 = accumulatedSec + (utcNow - buffInfo2.StartedAt).TotalSeconds;
            else
                num2 = accumulatedSec + (buffInfo2.ExpiresAt - buffInfo2.StartedAt).TotalSeconds;
            if (num2 > 0.0)
            {
                string name = ResolveBuffName(buffId);
                BuffUptimeEntry? buffUptimeEntry = buffUptimeEntryList.Find(b => b.Name == name);
                if (buffUptimeEntry != null)
                {
                    if (num2 > buffUptimeEntry.UptimeSeconds)
                    {
                        buffUptimeEntry.BuffId = buffId;
                        buffUptimeEntry.IconUrl = ResolveBuffIcon(buffId);
                        buffUptimeEntry.UptimeSeconds = num2;
                        buffUptimeEntry.UptimePercent = Math.Min(100.0, num2 / elapsedSec * 100.0);
                        buffUptimeEntry.CasterId = buffInfo2.CasterId;
                    }
                }
                else
                {
                    buffUptimeEntryList.Add(new BuffUptimeEntry()
                    {
                        BuffId = buffId,
                        Name = name,
                        IconUrl = ResolveBuffIcon(buffId),
                        UptimeSeconds = num2,
                        UptimePercent = Math.Min(100.0, num2 / elapsedSec * 100.0),
                        CasterId = buffInfo2.CasterId
                    });
                }
            }
        }
        return buffUptimeEntryList.OrderByDescending(b => b.UptimePercent).ToList();
    }

    public List<BuffUptimeEntry> GetUptimeByCaster(int casterId, double elapsedSec)
    {
        return BuildUptimeList(_casterBuffs, casterId, elapsedSec);
    }

    public void Reset()
    {
        _entities.Clear();
        _casterBuffs.Clear();
    }

    private sealed class EntityBuffState
    {
        public Dictionary<int, BuffInfo> Buffs { get; } = new Dictionary<int, BuffInfo>();
    }

    private sealed class BuffInfo
    {
        public DateTime StartedAt;
        public DateTime ExpiresAt;
        public double AccumulatedSec;
        public int CasterId;
    }
}
