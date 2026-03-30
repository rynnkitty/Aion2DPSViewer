using System;
using System.Collections.Generic;
using System.Linq;

namespace Aion2DPSViewer.Dps;

internal class CombatRecordStore
{
    private const int MaxRecords = 50;
    private readonly List<CombatRecord> _records = new List<CombatRecord>();
    private int _lastSavedTargetId;

    public event Action<CombatRecord>? RecordSaved;

    public void SaveOrUpdate(CombatRecord record, int primaryTargetId)
    {
        lock (_records)
        {
            if (primaryTargetId != 0 && primaryTargetId == _lastSavedTargetId && _records.Count > 0)
            {
                CombatRecord combatRecord = _records[_records.Count - 1];
                combatRecord.ElapsedSeconds = record.ElapsedSeconds;
                combatRecord.TotalPartyDamage = record.TotalPartyDamage;
                combatRecord.Players = record.Players;
                Console.Error.WriteLine($"[dps] 전투기록 갱신: {combatRecord.Id} ({combatRecord.Players.Count}명, {combatRecord.ElapsedSeconds:F1}s, {combatRecord.TotalPartyDamage} dmg)");
                RecordSaved?.Invoke(combatRecord);
            }
            else
            {
                _records.Add(record);
                if (_records.Count > MaxRecords)
                    _records.RemoveAt(0);
                _lastSavedTargetId = primaryTargetId;
                Console.Error.WriteLine($"[dps] 전투기록 저장: {record.Id} ({record.Players.Count}명, {record.ElapsedSeconds:F1}s, {record.TotalPartyDamage} dmg)");
                RecordSaved?.Invoke(record);
            }
        }
    }

    public List<CombatRecordSummary> GetAll()
    {
        lock (_records)
            return _records.Select(r => new CombatRecordSummary
            {
                Id = r.Id,
                Timestamp = r.Timestamp,
                ElapsedSeconds = r.ElapsedSeconds,
                TotalPartyDamage = r.TotalPartyDamage,
                Target = r.Target ?? "",
                TargetMaxHp = r.TargetMaxHp,
                PlayerCount = r.Players?.Count ?? 0
            }).ToList();
    }

    public DpsSnapshot? Get(string id)
    {
        CombatRecord? combatRecord;
        lock (_records)
            combatRecord = _records.FirstOrDefault(r => r.Id == id);
        if (combatRecord == null)
            return null;
        return new DpsSnapshot()
        {
            ElapsedSeconds = combatRecord.ElapsedSeconds,
            TotalPartyDamage = combatRecord.TotalPartyDamage,
            Target = string.IsNullOrEmpty(combatRecord.Target) ? null : new MobTarget() { Name = combatRecord.Target, MaxHp = combatRecord.TargetMaxHp },
            Players = combatRecord.Players ?? new List<ActorDps>()
        };
    }

    public void ResetLastTarget() => _lastSavedTargetId = 0;
}
