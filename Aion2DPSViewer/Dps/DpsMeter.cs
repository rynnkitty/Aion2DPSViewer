using Aion2DPSViewer.Api;
using Aion2DPSViewer.Packet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aion2DPSViewer.Dps;

public class DpsMeter : IDisposable
{
    private PacketProcessor? _processor;
    private readonly SkillDatabase _db = new SkillDatabase();
    private readonly PacketLogger _logger = new PacketLogger();
    private readonly BuffTracker _buffTracker;
    private Timer? _updateTimer;
    private bool _disposed;
    private static readonly (long packetHp, long realHp)[] _emonHpTiers = new (long, long)[2]
    {
        (22200000L, 32200000L),
        (60750000L, 85100000L)
    };
    private readonly ConcurrentDictionary<int, ActorStats> _actors = new ConcurrentDictionary<int, ActorStats>();
    private readonly ConcurrentDictionary<int, int> _summons = new ConcurrentDictionary<int, int>();
    private readonly ConcurrentDictionary<int, MobInfo> _mobs = new ConcurrentDictionary<int, MobInfo>();
    private int _primaryTargetId;
    private long _firstBossHp;
    private int _firstBossHpEntityId;
    private bool _maxHpCorrected;
    private DateTime _combatStart = DateTime.UtcNow;
    private bool _combatActive;
    private DateTime _lastDamageTime;
    private double _accumulatedCombatSec;
    private const double COMBAT_IDLE_SEC = 3.0;
    private string? _selfKey;
    private int _selfEntityId;
    private int? _currentDungeonId;
    private readonly ConcurrentDictionary<string, int> _jobOverrides = new ConcurrentDictionary<string, int>();
    private readonly ConcurrentDictionary<string, int> _scoreCache = new ConcurrentDictionary<string, int>();
    private readonly ConcurrentDictionary<int, string> _skillIcons = new ConcurrentDictionary<int, string>();
    private const int MaxEntityNames = 20;
    private readonly Dictionary<int, string> _entityToName = new Dictionary<int, string>();
    private readonly Queue<int> _entityNameOrder = new Queue<int>();
    private readonly object _entityNameLock = new object();
    private readonly HashSet<int> _removedEntities = new HashSet<int>();
    private readonly ConcurrentDictionary<string, int> _cpCache = new ConcurrentDictionary<string, int>();
    private readonly ConcurrentDictionary<string, byte> _cpRequested = new ConcurrentDictionary<string, byte>();
    private static readonly HttpClient _httpClient = new HttpClient()
    {
        Timeout = TimeSpan.FromSeconds(3.0)
    };
    private const uint SPECIAL_BACK = 1;
    private const uint SPECIAL_SHIELD_BLOCK = 2;
    private const uint SPECIAL_PARRY = 4;
    private const uint SPECIAL_PERFECT = 8;
    private const uint SPECIAL_HARD_HIT = 16;
    private const uint SPECIAL_CRITICAL = 256;
    private readonly CombatRecordStore _recordStore = new CombatRecordStore();
    private bool _combatRecordSaved;
    private DateTime _combatRecordSavedAt;
    private readonly List<TimelineEntry> _timeline = new List<TimelineEntry>();
    private int _lastTimelineSec = -1;
    private readonly List<HitLogEntry> _hitLog = new List<HitLogEntry>();
    private bool _snapshotDirty;
    private int _logFlushCounter;

    public DpsMeter() => _buffTracker = new BuffTracker(_db);

    public static string PacketLogDirectory => PacketLogger.Directory;

    private void LogPacket(string msg) => _logger.Log(msg);

    private string FeedRef() => _logger.FeedRef();

    private void SetEntityName(int entityId, string name)
    {
        lock (_entityNameLock)
        {
            if (_entityToName.ContainsKey(entityId))
            {
                _entityToName[entityId] = name;
            }
            else
            {
                while (_entityNameOrder.Count >= 20 && _entityNameOrder.Count > 0)
                {
                    int num = _entityNameOrder.Dequeue();
                    if (num == _selfEntityId)
                    {
                        _entityNameOrder.Enqueue(num);
                        if (_entityNameOrder.Peek() == num)
                            break;
                    }
                    else if (_entityToName.ContainsKey(num))
                    {
                        _entityToName.Remove(num);
                        break;
                    }
                }
                _entityToName[entityId] = name;
                _entityNameOrder.Enqueue(entityId);
            }
        }
    }

    private string GetEntityName(int entityId, string fallback = "")
    {
        lock (_entityNameLock)
        {
            return _entityToName.TryGetValue(entityId, out string? str) ? str : fallback;
        }
    }

    private void RemoveEntityName(int entityId)
    {
        if (entityId == _selfEntityId)
            return;
        lock (_entityNameLock)
            _entityToName.Remove(entityId);
    }

    private static string StripServerSuffix(string name)
    {
        int num = name.IndexOf('[');
        return num <= 0 ? name : name.Substring(0, num);
    }

    public event Action<DpsSnapshot>? DpsUpdated;

    public event Action<CombatRecord>? CombatRecordSaved
    {
        add => _recordStore.RecordSaved += value;
        remove => _recordStore.RecordSaved -= value;
    }

    public event Action? CombatStarted;

    public event Action<string, int>? NewActorDetected;

    public event Action<string, int>? SelfDetected;

    public event Action<string>? LogMessage;

    public bool IsRunning => _processor != null;

    public void SetSelf(string nickname, int serverId)
    {
        string str = $"{nickname}:{serverId}";
        if (_selfKey != null && _selfKey != str)
            Reset();
        _selfKey = str;
    }

    public void UpdateActorInfo(
        string nickname,
        int serverId,
        string? jobName,
        int score = 0,
        int combatPower = 0)
    {
        string str = $"{nickname}:{serverId}";
        int num1;
        int num2 = jobName == null || !JobMapping.NameToCode.TryGetValue(jobName, out num1) ? -1 : num1;
        if (num2 >= 0)
            _jobOverrides[str] = num2;
        if (score > 0)
            _scoreCache[str] = score;
        if (combatPower > 0)
            _cpCache[str] = combatPower;
        foreach (KeyValuePair<int, ActorStats> actor in _actors)
        {
            if (actor.Value.WhitelistKey == str)
            {
                lock (actor.Value)
                {
                    if (num2 >= 0)
                        actor.Value.JobCode = num2;
                    if (score > 0)
                        actor.Value.CombatScore = score;
                    if (combatPower > 0)
                        actor.Value.CombatPower = combatPower;
                }
            }
        }
        _snapshotDirty = true;
    }

    public Task<(int at, int cp)> GetCacheFallbackAsync(string nickname, int serverId)
    {
        string str = $"{nickname}:{serverId}";
        int num1;
        int num2;
        return Task.FromResult<(int, int)>((_scoreCache.TryGetValue(str, out num1) ? num1 : 0, _cpCache.TryGetValue(str, out num2) ? num2 : 0));
    }

    public void UpdateSkillIcons(Dictionary<int, string> icons)
    {
        foreach (KeyValuePair<int, string> icon in icons)
        {
            _skillIcons[icon.Key] = icon.Value;
        }
    }

    private static bool IsDummy(MobInfo mob) => IsDummyByName(mob.Name);

    private static bool IsDummyByName(string name) => name.Contains("허수아비") || name.Contains("샌드백");

    private bool IsDummyByCode(int mobCode)
    {
        return IsDummyByName(_db.GetMobName(mobCode) ?? "");
    }

    public void Start(int serverPort)
    {
        if (_processor != null)
            return;
        _processor = new PacketProcessor(serverPort);
        _processor.GetSkillName = code => _db.GetSkillName(code);
        _processor.ContainsSkillCode = code => _db.ContainsSkillCode(code);
        _processor.IsMobBoss = code => _db.IsMobBoss(code);
        _processor.SkillDb = _db;
        _processor.ValidServerIds = new HashSet<int>(ServerMap.Servers.Keys);
        _processor.OnDamage += new PacketProcessor.DamageHandler(OnDamage);
        _processor.OnMobSpawn += new PacketProcessor.MobSpawnHandler(OnMobSpawn);
        _processor.OnSummon += new PacketProcessor.SummonHandler(OnSummon);
        _processor.OnUserInfo += new PacketProcessor.UserInfoHandler(OnUserInfo);
        _processor.OnCombatPower += new PacketProcessor.CombatPowerHandler(OnCombatPower);
        _processor.OnCombatPowerByName += new PacketProcessor.CombatPowerByNameHandler(OnCombatPowerByName);
        _processor.OnEntityRemoved += new PacketProcessor.EntityRemovedHandler(OnEntityRemoved);
        _processor.OnBossHp += new PacketProcessor.BossHpHandler(OnBossHp);
        _processor.OnBuff += new PacketProcessor.BuffHandler(OnBuff);
        _processor.OnLog += new PacketProcessor.LogHandler(OnLog);
        _processor.Start();
        _combatStart = DateTime.UtcNow;
        _updateTimer = new Timer(_ => Tick(), null, TimeSpan.FromMilliseconds(200.0), TimeSpan.FromMilliseconds(200.0));
        _logger.Init();
        Console.Error.WriteLine($"[dps] DPS 미터 시작 (포트: {serverPort})");
    }

    public void Stop()
    {
        _updateTimer?.Dispose();
        _updateTimer = null;
        if (_processor != null)
        {
            _processor.Stop();
            _processor.Dispose();
            _processor = null;
        }
        _logger.Dispose();
        Console.Error.WriteLine("[dps] DPS 미터 정지");
    }

    public void Reset()
    {
        SaveCombatRecord();
        ResetCombatStats();
        EmitSnapshot();
        Console.Error.WriteLine("[dps] DPS 미터 리셋");
    }

    private void ResetCombatStats()
    {
        List<int> intList = new List<int>();
        ActorStats actorStats1 = null!;
        foreach (KeyValuePair<int, ActorStats> actor in _actors)
        {
            ActorStats actorStats2 = actor.Value;
            actorStats1 = actorStats2;
            bool flag = false;
            try
            {
                Monitor.Enter(actorStats1, ref flag);
                if (actorStats2.TotalDamage == 0L && actorStats2.HealTotal == 0L)
                {
                    if (string.IsNullOrEmpty(actorStats2.WhitelistKey))
                        intList.Add(actor.Key);
                }
                else
                {
                    actorStats2.TotalDamage = 0L;
                    actorStats2.HitCount = 0;
                    actorStats2.CritCount = 0;
                    actorStats2.HealTotal = 0L;
                    actorStats2.FirstHit = DateTime.MaxValue;
                    actorStats2.LastHit = DateTime.MinValue;
                    actorStats2.Skills.Clear();
                }
            }
            finally
            {
                if (flag)
                    Monitor.Exit(actorStats1);
            }
        }
        foreach (int num in intList)
        {
            ActorStats removed;
            _actors.TryRemove(num, out removed);
        }
        _primaryTargetId = 0;
        _firstBossHp = 0L;
        _firstBossHpEntityId = 0;
        _maxHpCorrected = false;
        _recordStore.ResetLastTarget();
        _combatActive = false;
        _combatStart = DateTime.UtcNow;
        _accumulatedCombatSec = 0.0;
        _combatRecordSaved = false;
        _timeline.Clear();
        _lastTimelineSec = -1;
        _hitLog.Clear();
        _cpRequested.Clear();
        _removedEntities.Clear();
        _buffTracker.Reset();
    }

    public void FeedPacket(int srcPort, int dstPort, byte[] payload, uint seqNum)
    {
        if (payload.Length == 0 || _processor == null)
            return;
        _logger.IncrementFeedSeq();
        long seq = _logger.ReadFeedSeq();
        byte[] data = new byte[payload.Length];
        Buffer.BlockCopy(payload, 0, data, 0, payload.Length);
        _logger.RecordFeed(seq, srcPort, dstPort, data);
        LogPacket($"FEED [{seq}] {DateTime.Now:HH:mm:ss.fff} src={srcPort} dst={dstPort} tcpseq={seqNum} len={payload.Length}");
        LogPacket("  " + BitConverter.ToString(data).Replace("-", " "));
        _processor.Enqueue(srcPort, dstPort, payload, payload.Length, null, seqNum);
    }

    private void OnDamage(
        int actorId,
        int targetId,
        int skillCode,
        byte dmgType,
        int damage,
        uint flags,
        int multiCount,
        int multiDmg,
        int heal,
        int isDot)
    {
        try
        {
            OnDamageCore(actorId, targetId, skillCode, dmgType, damage, flags, multiCount, multiDmg, heal, isDot);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dps] OnDamage 예외: " + ex.Message);
        }
    }

    private void OnDamageCore(
        int actorId,
        int targetId,
        int skillCode,
        byte dmgType,
        int damage,
        uint flags,
        int multiCount,
        int multiDmg,
        int heal,
        int isDot)
    {
        string entityName1 = GetEntityName(actorId, $"#{actorId}");
        string entityName2 = GetEntityName(targetId);
        string str2;
        if (entityName2 == null || entityName2.Length <= 0)
        {
            MobInfo mobInfo;
            str2 = !_mobs.TryGetValue(targetId, out mobInfo) ? $"#{targetId}" : mobInfo.Name;
        }
        else
            str2 = entityName2;
        LogPacket($"CB OnDamage {FeedRef()} actor={actorId}({entityName1}) target={targetId}({str2}) skill={skillCode} type={dmgType} dmg={damage} flags=0x{flags:X4} multi={multiCount}/{multiDmg} heal={heal} dot={isDot}");
        int total = damage + multiDmg;
        int num1;
        int resolvedActor = _summons.TryGetValue(actorId, out num1) ? num1 : actorId;
        if (total > 0 && resolvedActor != targetId)
            TryAccumulateDamage();
        if (heal <= 0)
            return;
        ActorStats healOrAdd = _actors.GetOrAdd(resolvedActor, id => new ActorStats() { EntityId = id });
        lock (healOrAdd)
            healOrAdd.HealTotal += (long)heal;

        void TryAccumulateDamage()
        {
            ActorStats orAdd = _actors.GetOrAdd(resolvedActor, id => new ActorStats() { EntityId = id });
            DateTime utcNow = DateTime.UtcNow;
            MobInfo mob1;
            if (!_mobs.TryGetValue(targetId, out mob1))
                return;
            bool flag1 = IsDummy(mob1);
            if (!mob1.IsBoss && !flag1)
                return;
            if (flag1)
            {
                if (_selfKey == null)
                    return;
                string entityName = GetEntityName(resolvedActor);
                if (orAdd.WhitelistKey != _selfKey && (string.IsNullOrEmpty(entityName) || !_selfKey.StartsWith(entityName + ":")))
                    return;
            }
            if (_primaryTargetId == 0)
            {
                _primaryTargetId = targetId;
                if (mob1.IsBoss)
                    RequestMissingActorLookups();
            }
            else if (_primaryTargetId != targetId)
            {
                bool flag2 = _removedEntities.Contains(_primaryTargetId);
                MobInfo mobInfo;
                bool prevBossAlive = _mobs.TryGetValue(_primaryTargetId, out mobInfo) && mobInfo.IsBoss && mobInfo.CurrentHp > 0L;
                if (prevBossAlive)
                {
                    if (!flag2)
                        return;
                    Console.Error.WriteLine($"[dps] 타겟변경 차단: prev={_primaryTargetId}({mobInfo.Name}) hp={mobInfo.CurrentHp}/{mobInfo.MaxHp} removed=true → new={targetId}({mob1.Name})");
                    return;
                }
                if (!(!_combatActive | flag2) || !(mob1.IsBoss | flag1))
                    return;
                SaveCombatRecord();
                ResetCombatStats();
                orAdd = _actors.GetOrAdd(resolvedActor, id => new ActorStats() { EntityId = id });
                _primaryTargetId = targetId;
                if (mob1.IsBoss)
                    RequestMissingActorLookups();
            }
            if (!_combatActive)
            {
                if (_combatRecordSaved && (utcNow - _combatRecordSavedAt).TotalSeconds >= 60.0)
                {
                    ResetCombatStats();
                    orAdd = _actors.GetOrAdd(resolvedActor, id => new ActorStats() { EntityId = id });
                    _primaryTargetId = targetId;
                    if (mob1.IsBoss)
                        RequestMissingActorLookups();
                }
                _combatRecordSaved = false;
                _combatActive = true;
                _combatStart = utcNow;
                Action? combatStarted = CombatStarted;
                if (combatStarted != null)
                    combatStarted();
            }
            _lastDamageTime = utcNow;
            bool flag3;
            lock (orAdd)
            {
                flag3 = orAdd.TotalDamage == 0L;
                int[]? numArray = null;
                if (skillCode >= 10000000)
                {
                    int num1 = skillCode / 1000000;
                    int num2;
                    if (JobMapping.SkillPrefixToJob.TryGetValue(num1, out num2) && (orAdd.JobCode < 0 || orAdd.JobCode == 2 && num2 == 5 || orAdd.JobCode == 5 && num2 == 2))
                        orAdd.JobCode = num2;
                    if (isDot == 0)
                    {
                        int lastRawSkillCode = _db.LastRawSkillCode;
                        if (lastRawSkillCode != 0 && lastRawSkillCode != skillCode)
                            numArray = SkillDatabase.DecodeSpecializations(lastRawSkillCode, skillCode);
                    }
                }
                orAdd.TotalDamage += (long)total;
                ++orAdd.HitCount;
                if (((int)flags & 256) != 0)
                    ++orAdd.CritCount;
                if (utcNow < orAdd.FirstHit)
                    orAdd.FirstHit = utcNow;
                if (utcNow > orAdd.LastHit)
                    orAdd.LastHit = utcNow;
                string? skillNameStr = _db.GetSkillName(skillCode);
                if (skillNameStr == null)
                    skillNameStr = $"스킬#{skillCode}";
                string str2 = skillNameStr;
                SkillDamage? skillDamage = null;
                foreach (KeyValuePair<int, SkillDamage> skill in orAdd.Skills)
                {
                    if (skill.Value.SkillName == str2)
                    {
                        skillDamage = skill.Value;
                        break;
                    }
                }
                if (skillDamage == null)
                {
                    int num = _db.ContainsSkillCode(skillCode) ? skillCode : skillCode / 10000 * 10000;
                    skillDamage = new SkillDamage()
                    {
                        SkillCode = num,
                        SkillName = str2
                    };
                    orAdd.Skills[num] = skillDamage;
                }
                if (numArray != null && (skillDamage.Specs == null || numArray.Length > skillDamage.Specs.Length))
                    skillDamage.Specs = numArray;
                skillDamage.TotalDamage += (long)total;
                ++skillDamage.HitCount;
                int num3 = (flags & 256U) > 0U ? 1 : 0;
                bool flag4 = (flags & 1U) > 0U;
                bool flag5 = (flags & 16U) > 0U;
                bool flag6 = (flags & 8U) > 0U;
                bool flag7 = (flags & 6U) > 0U;
                bool flag8 = total == 0;
                bool flag9 = multiCount >= 1;
                bool flag10 = num3 == 0 && !flag4 && !flag5 && !flag6 && !flag7 && !flag8;
                if (num3 != 0)
                    ++skillDamage.CritCount;
                if (flag4)
                    ++skillDamage.BackCount;
                if (flag5)
                    ++skillDamage.HardHitCount;
                if (flag6)
                    ++skillDamage.PerfectCount;
                if (flag7)
                    ++skillDamage.BlockCount;
                if (flag8)
                    ++skillDamage.EvadeCount;
                if (flag9)
                    ++skillDamage.MultiHitCount;
                if (flag10)
                    ++skillDamage.NormalCount;
                if (total > 0 && (long)total < skillDamage.MinDamage)
                    skillDamage.MinDamage = (long)total;
                if ((long)total > skillDamage.MaxDamage)
                    skillDamage.MaxDamage = (long)total;
            }
            double num4 = _combatActive ? _accumulatedCombatSec + (DateTime.UtcNow - _combatStart).TotalSeconds : _accumulatedCombatSec;
            SkillDatabase.SkillInfo? skillInfo = _db.GetSkillInfo(skillCode);
            List<HitLogEntry> hitLog = _hitLog;
            HitLogEntry hitLogEntry = new HitLogEntry();
            hitLogEntry.T = Math.Round(num4, 2);
            hitLogEntry.EntityId = resolvedActor;
            string? str3 = skillInfo?.Name;
            if (str3 == null)
            {
                string? skillName = _db.GetSkillName(skillCode);
                str3 = skillName ?? $"스킬#{skillCode}";
            }
            hitLogEntry.SkillName = str3;
            string? str4;
            hitLogEntry.SkillIcon = _skillIcons.TryGetValue(skillCode / 10000 * 10000, out str4) ? str4 : skillInfo?.Icon;
            hitLogEntry.SkillType = skillInfo?.Type;
            hitLogEntry.Damage = total;
            hitLogEntry.Flags = flags;
            hitLog.Add(hitLogEntry);
            MobInfo mob2;
            if (_primaryTargetId != 0 && _mobs.TryGetValue(_primaryTargetId, out mob2) && mob2.IsBoss)
                TryCorrectMaxHp(mob2);
            _snapshotDirty = true;
            if (!flag3 || string.IsNullOrEmpty(orAdd.WhitelistKey))
                return;
            string key = orAdd.WhitelistKey;
            if (orAdd.CombatScore <= 0)
            {
                int num5;
                int num6 = _scoreCache.TryGetValue(key, out num5) ? num5 : 0;
                if (num6 > 0)
                {
                    lock (orAdd)
                        orAdd.CombatScore = num6;
                }
            }
            if (orAdd.CombatPower > 0 || !_cpRequested.TryAdd(key, (byte)1))
                return;
            int num7;
            int num8 = _cpCache.TryGetValue(key, out num7) ? num7 : 0;
            if (num8 > 0)
            {
                lock (orAdd)
                    orAdd.CombatPower = num8;
            }
            else
            {
                string[] strArray = key.Split(new[] { ':' }, StringSplitOptions.None);
                int sid;
                if (strArray.Length != 2 || !int.TryParse(strArray[1], out sid))
                    return;
                string cpName = strArray[0];
                int cpEntityId = resolvedActor;
                Task.Run(async () =>
                {
                    int num2 = await FetchCombatPowerFromApi(cpName, sid);
                    if (num2 <= 0)
                        return;
                    _cpCache[key] = num2;
                    ActorStats actorStats;
                    if (!_actors.TryGetValue(cpEntityId, out actorStats))
                        return;
                    lock (actorStats)
                        actorStats.CombatPower = num2;
                    _snapshotDirty = true;
                });
            }
        }
    }

    private void OnMobSpawn(int mobId, int mobCode, int hp, int isBoss)
    {
        try
        {
            LogPacket($"CB OnMobSpawn {FeedRef()} id={mobId} code={mobCode} name={_db.GetMobName(mobCode)} hp={hp} boss={isBoss}");
            _removedEntities.Remove(mobId);
            string name = _db.GetMobName(mobCode);
            if (string.IsNullOrEmpty(name))
                name = $"몹#{mobCode}";
            bool flag1 = isBoss == 1 || _db.IsMobBoss(mobCode);
            bool flag2 = IsDummyByName(name);
            if (!flag1 && !flag2)
            {
                MobInfo mobInfo;
                _mobs.TryRemove(mobId, out mobInfo);
            }
            else
            {
                MobInfo orAdd = _mobs.GetOrAdd(mobId, _ => new MobInfo() { MobId = mobId });
                orAdd.MobCode = mobCode;
                orAdd.Name = name;
                orAdd.IsBoss = flag1;
                if (hp <= 0)
                    return;
                orAdd.MaxHp = name == "가라앉은 에몬" ? ResolveEmonHp((long)hp) : (long)hp;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dps] OnMobSpawn 예외: " + ex.Message);
        }
    }

    private static long ResolveEmonHp(long packetHp)
    {
        foreach ((long packetHp1, long realHp) in _emonHpTiers)
        {
            if ((double)Math.Abs(packetHp - packetHp1) < (double)packetHp1 * 0.05)
                return realHp;
        }
        return packetHp < 15000000L ? packetHp : (long)((double)packetHp * 1.4);
    }

    private void OnSummon(int actorId, int petId)
    {
        try
        {
            string entityName = GetEntityName(actorId, $"#{actorId}");
            LogPacket($"CB OnSummon {FeedRef()} actor={actorId}({entityName}) pet={petId}");
            _summons[petId] = actorId;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dps] OnSummon 예외: " + ex.Message);
        }
    }

    private void OnUserInfo(int entityId, string nickname, int serverId, int jobCode, int isSelf)
    {
        try
        {
            string name1 = nickname ?? $"#{entityId}";
            LogPacket($"CB OnUserInfo {FeedRef()} entity={entityId} name=\"{name1}\" server={serverId} job={jobCode} isSelf={isSelf}");
            string name2 = StripServerSuffix(name1);
            if (name2.Length < 1 || serverId < 1001 || serverId > 2999 || name2.StartsWith("#"))
            {
                SetEntityName(entityId, name2);
            }
            else
            {
                string str2 = $"{name2}:{serverId}";
                if (isSelf == 1 && _selfKey != str2)
                {
                    _selfKey = str2;
                    _selfEntityId = entityId;
                    Console.Error.WriteLine($"[dps] self 확정 (DLL isSelf): {name2} server={serverId}");
                    Action<string, int>? selfDetected = SelfDetected;
                    if (selfDetected != null)
                        selfDetected(name2, serverId);
                }
                else if (isSelf == 1)
                    _selfEntityId = entityId;
                SetEntityName(entityId, name2);
                bool flag1 = !_actors.ContainsKey(entityId);
                ActorStats orAdd = _actors.GetOrAdd(entityId, id => new ActorStats() { EntityId = id });
                int num1;
                int num2 = _jobOverrides.TryGetValue(str2, out num1) ? num1 : -1;
                int num3;
                int num4 = _scoreCache.TryGetValue(str2, out num3) ? num3 : 0;
                int num5;
                int num6 = _cpCache.TryGetValue(str2, out num5) ? num5 : 0;
                if (num4 > 0 || num6 > 0)
                    Console.Error.WriteLine($"[dps] OnUserInfo 캐시 히트: {str2} AT={num4} CP={num6}");
                int num7;
                int num8 = JobMapping.GameToUi.TryGetValue(jobCode, out num7) ? num7 : -1;
                string name3 = ServerMap.GetName(serverId);
                string str3 = !string.IsNullOrEmpty(name3) ? $"{name2}[{name3}]" : name2;
                lock (orAdd)
                {
                    orAdd.Name = str3;
                    orAdd.WhitelistKey = str2;
                    orAdd.IsPlayer = true;
                    orAdd.ServerId = serverId;
                    if (num8 >= 0)
                        orAdd.JobCode = num8;
                    else if (num2 >= 0)
                        orAdd.JobCode = num2;
                    if (num4 > 0)
                        orAdd.CombatScore = num4;
                    if (num6 > 0)
                        orAdd.CombatPower = num6;
                }
                if (!flag1 || _jobOverrides.ContainsKey(str2))
                    return;
                MobInfo mobInfo;
                bool flag2 = _primaryTargetId != 0 && _mobs.TryGetValue(_primaryTargetId, out mobInfo) && mobInfo.IsBoss;
                if (!(str2 == _selfKey | flag2))
                    return;
                Action<string, int>? newActorDetected = NewActorDetected;
                if (newActorDetected == null)
                    return;
                newActorDetected(name2, serverId);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dps] OnUserInfo 예외: " + ex.Message);
        }
    }

    private void RequestMissingActorLookups()
    {
        foreach (KeyValuePair<int, ActorStats> actor in _actors)
        {
            ActorStats actorStats = actor.Value;
            if (!string.IsNullOrEmpty(actorStats.WhitelistKey) && actorStats.WhitelistKey != _selfKey && !_jobOverrides.ContainsKey(actorStats.WhitelistKey))
            {
                string[] strArray = actorStats.WhitelistKey.Split(new[] { ':' }, StringSplitOptions.None);
                int num;
                if (strArray.Length == 2 && int.TryParse(strArray[1], out num))
                {
                    Action<string, int>? newActorDetected = NewActorDetected;
                    if (newActorDetected != null)
                        newActorDetected(strArray[0], num);
                }
            }
        }
    }

    private async Task<int> FetchCombatPowerFromApi(string name, int serverId)
    {
        try
        {
            (int, string)? nullable = await PlayncClient.SearchCharacter(name, serverId, serverId >= 2000 ? 2 : 1);
            if (!nullable.HasValue)
            {
                Console.Error.WriteLine($"[dps-cp] API 검색 실패: {name}:{serverId}");
                return 0;
            }
            JsonElement profile;
            JsonElement fetchResult = await PlayncClient.FetchInfo(nullable.Value.Item2, serverId);
            if (fetchResult.TryGetProperty("profile", out profile))
            {
                JsonElement jsonElement2;
                if (profile.TryGetProperty("combatPower", out jsonElement2))
                {
                    int num1;
                    int num2 = jsonElement2.ValueKind == JsonValueKind.Number
                        ? jsonElement2.GetInt32()
                        : (int.TryParse(jsonElement2.GetString(), out num1) ? num1 : 0);
                    if (num2 > 0)
                        Console.Error.WriteLine($"[dps-cp] API 전투력: {name}:{serverId} CP={num2}");
                    return num2;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[dps-cp] API 조회 실패 {name}:{serverId}: {ex.Message}");
        }
        return 0;
    }

    private void OnCombatPower(int entityId, int combatPower)
    {
        try
        {
            string entityName = GetEntityName(entityId, $"#{entityId}");
            LogPacket($"CB OnCombatPower {FeedRef()} entity={entityId}({entityName}) CP={combatPower}");
            Console.Error.WriteLine($"[dps] CP 패킷감지: {entityName} CP={combatPower}");
            foreach (KeyValuePair<int, ActorStats> actor in _actors)
            {
                if (actor.Key == entityId)
                {
                    lock (actor.Value)
                        actor.Value.CombatPower = combatPower;
                    _snapshotDirty = true;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dps] OnCombatPower 예외: " + ex.Message);
        }
    }

    private void OnCombatPowerByName(string nickname, int serverId, int combatPower)
    {
        try
        {
            LogPacket($"CB OnCombatPowerByName {FeedRef()} nick={nickname} server={serverId} CP={combatPower}");
            Console.Error.WriteLine($"[dps] CP 패킷감지: {nickname}:{serverId} CP={combatPower}");
            lock (_entityNameLock)
            {
                foreach (KeyValuePair<int, string> keyValuePair in _entityToName)
                {
                    if (StripServerSuffix(keyValuePair.Value) == nickname)
                    {
                        int key = keyValuePair.Key;
                        ActorStats actorStats;
                        if (!_actors.TryGetValue(key, out actorStats))
                            break;
                        lock (actorStats)
                            actorStats.CombatPower = combatPower;
                        _snapshotDirty = true;
                        Console.Error.WriteLine($"[dps] CP 적용: {nickname}:{serverId} → entity={key} CP={combatPower}");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dps] OnCombatPowerByName 예외: " + ex.Message);
        }
    }

    private void OnEntityRemoved(int entityId)
    {
        try
        {
            string entityName = GetEntityName(entityId, $"#{entityId}");
            LogPacket($"CB OnEntityRemoved {FeedRef()} entity={entityId}({entityName})");
            int num;
            _summons.TryRemove(entityId, out num);
            RemoveEntityName(entityId);
            _removedEntities.Add(entityId);
            MobInfo mobInfo;
            if (!_mobs.TryGetValue(entityId, out mobInfo) || !mobInfo.IsBoss)
                return;
            mobInfo.CurrentHp = 0L;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dps] OnEntityRemoved 예외: " + ex.Message);
        }
    }

    private void OnBossHp(int entityId, int currentHp)
    {
        try
        {
            MobInfo mob;
            if (!_mobs.TryGetValue(entityId, out mob) || !mob.IsBoss)
                return;
            long currentHp1 = mob.CurrentHp;
            mob.CurrentHp = (long)currentHp;
            _snapshotDirty = true;
            if (currentHp1 == 0L && _firstBossHp == 0L && (long)currentHp < mob.MaxHp)
            {
                _firstBossHp = (long)currentHp;
                _firstBossHpEntityId = entityId;
            }
            long maxHp = mob.MaxHp;
            if (entityId == _primaryTargetId)
                TryCorrectMaxHp(mob);
            if ((long)currentHp > mob.MaxHp)
                mob.MaxHp = (long)currentHp;
            int num1 = mob.MaxHp > maxHp ? 1 : 0;
            double num2 = mob.MaxHp > 0L ? (double)currentHp / (double)mob.MaxHp * 100.0 : 0.0;
            if (num2 < 10.0)
                Console.Error.WriteLine($"[dps] BossHP: {mob.Name} {currentHp:N0}/{mob.MaxHp:N0} ({num2:F1}%)");
            if (num1 != 0 || entityId != _primaryTargetId || (long)currentHp < mob.MaxHp || IsDummy(mob))
                return;
            long num3 = 0;
            foreach (ActorStats actorStats in _actors.Values)
                num3 += actorStats.TotalDamage;
            if (num3 <= 0L)
                return;
            Console.Error.WriteLine($"[dps] 보스 HP 리셋 감지: {mob.Name} HP={currentHp:N0}/{mob.MaxHp:N0} (이전={currentHp1:N0}, dmg={num3:N0}) → 전투 리셋");
            SaveCombatRecord();
            ResetCombatStats();
            _primaryTargetId = entityId;
            mob.CurrentHp = 0L;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dps] OnBossHp 예외: " + ex.Message);
        }
    }

    private void TryCorrectMaxHp(MobInfo mob)
    {
        if (_maxHpCorrected || _firstBossHp <= 0L || _firstBossHpEntityId != mob.MobId)
            return;
        long num1 = 0;
        foreach (ActorStats actorStats in _actors.Values)
            num1 += actorStats.TotalDamage;
        if (num1 <= 0L)
            return;
        _maxHpCorrected = true;
        long num2 = _firstBossHp + num1;
        if (num2 == mob.MaxHp)
            return;
        Console.Error.WriteLine($"[dps] maxHP 보정: {mob.Name} {mob.MaxHp:N0} → {num2:N0} (firstHP={_firstBossHp:N0} + dmg={num1:N0})");
        mob.MaxHp = num2;
    }

    private void OnBuff(
        int entityId,
        int buffId,
        int type,
        uint durationMs,
        long timestamp,
        int casterId)
    {
        try
        {
            _buffTracker.Track(entityId, buffId, type, durationMs, casterId);
            string entityName = GetEntityName(entityId, $"#{entityId}");
            string str2 = casterId == 0 ? "?" : GetEntityName(casterId, $"#{casterId}");
            LogPacket($"CB OnBuff {FeedRef()} entity={entityId}({entityName}) buffId={buffId} type=0x{type:X2} dur={durationMs}ms ts={timestamp} caster={casterId}({str2})");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dps] OnBuff 예외: " + ex.Message);
        }
    }

    public List<BuffUptimeEntry> GetBuffUptime(int entityId, double elapsedSec)
    {
        return _buffTracker.GetUptime(entityId, elapsedSec);
    }

    private void OnLog(int level, string message)
    {
        try
        {
            string str = message ?? "";
            LogPacket($"CB OnLog {FeedRef()} level={level} msg=\"{str}\"");
            if (level < 3)
                return;
            Action<string>? logMessage = LogMessage;
            if (logMessage == null)
                return;
            logMessage("[DLL] " + str);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dps] OnLog 예외: " + ex.Message);
        }
    }

    private void Tick()
    {
        try
        {
            if (_snapshotDirty)
            {
                _snapshotDirty = false;
                EmitSnapshot();
            }
            if (_combatActive)
            {
                double num1 = _accumulatedCombatSec + (_lastDamageTime - _combatStart).TotalSeconds;
                int num2 = (int)num1;
                if (num2 > _lastTimelineSec && num2 > 0)
                {
                    _lastTimelineSec = num2;
                    TimelineEntry timelineEntry = new TimelineEntry() { T = num2 };
                    foreach (ActorStats actorStats in _actors.Values)
                    {
                        if (actorStats.TotalDamage > 0L && !string.IsNullOrEmpty(actorStats.WhitelistKey))
                            timelineEntry.Players.Add(new TimelinePlayer()
                            {
                                EntityId = actorStats.EntityId,
                                Damage = actorStats.TotalDamage,
                                Dps = num1 > 0.0 ? (long)((double)actorStats.TotalDamage / num1) : 0L
                            });
                    }
                    if (timelineEntry.Players.Count > 0)
                        _timeline.Add(timelineEntry);
                }
            }
            if (_combatActive)
            {
                TimeSpan timeSpan = DateTime.UtcNow - _lastDamageTime;
                MobInfo mobInfo;
                bool primaryBossAlive = _primaryTargetId != 0
                    && _mobs.TryGetValue(_primaryTargetId, out mobInfo)
                    && mobInfo.IsBoss
                    && mobInfo.CurrentHp > 0L
                    && !_removedEntities.Contains(_primaryTargetId);
                if (timeSpan.TotalSeconds > 3.0 && !primaryBossAlive)
                {
                    double accumulatedCombatSec = _accumulatedCombatSec;
                    double totalSeconds = (_lastDamageTime - _combatStart).TotalSeconds;
                    _accumulatedCombatSec = accumulatedCombatSec + totalSeconds;
                    _combatActive = false;
                    SaveCombatRecord();
                }
            }
            if (++_logFlushCounter < 5)
                return;
            _logFlushCounter = 0;
            _logger.Flush();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dps] Tick 예외: " + ex.Message);
        }
    }

    public DpsSnapshot? BuildCurrentSnapshot() => BuildSnapshot();

    private DpsSnapshot? BuildSnapshot()
    {
        double elapsed = _combatActive
            ? _accumulatedCombatSec + (_lastDamageTime - _combatStart).TotalSeconds
            : _accumulatedCombatSec;
        MobInfo mob;
        bool flag = _primaryTargetId != 0 && _mobs.TryGetValue(_primaryTargetId, out mob) && IsDummy(mob);
        Dictionary<string, ActorStats> dictionary = new Dictionary<string, ActorStats>(16);
        foreach (ActorStats actorStats1 in _actors.Values)
        {
            ActorStats actorStats2;
            if (actorStats1.TotalDamage > 0L
                && !string.IsNullOrEmpty(actorStats1.WhitelistKey)
                && (!flag || actorStats1.WhitelistKey == _selfKey)
                && (!dictionary.TryGetValue(actorStats1.WhitelistKey!, out actorStats2) || actorStats1.TotalDamage > actorStats2.TotalDamage))
                dictionary[actorStats1.WhitelistKey!] = actorStats1;
        }
        List<ActorStats> actorStatsList = new List<ActorStats>(dictionary.Values);
        actorStatsList.Sort((a, b) => b.TotalDamage.CompareTo(a.TotalDamage));
        if (actorStatsList.Count > 12)
            actorStatsList.RemoveRange(12, actorStatsList.Count - 12);
        long totalPartyDamage = 0;
        foreach (ActorStats actorStats in actorStatsList)
            totalPartyDamage += actorStats.TotalDamage;
        MobTarget? mobTarget = null;
        MobInfo mobInfo;
        if (_primaryTargetId != 0 && _mobs.TryGetValue(_primaryTargetId, out mobInfo))
            mobTarget = new MobTarget()
            {
                Name = mobInfo.Name,
                MaxHp = mobInfo.MaxHp,
                CurrentHp = mobInfo.CurrentHp,
                TotalDamageReceived = totalPartyDamage,
                IsBoss = mobInfo.IsBoss,
                Debuffs = _buffTracker.GetUptime(_primaryTargetId, elapsed)
            };
        long maxHp = mobTarget != null ? mobTarget.MaxHp : 0L;
        DateTime dateTime1 = DateTime.MaxValue;
        DateTime dateTime2 = DateTime.MinValue;
        foreach (ActorStats actorStats in actorStatsList)
        {
            if (actorStats.FirstHit < dateTime1)
                dateTime1 = actorStats.FirstHit;
            if (actorStats.LastHit > dateTime2)
                dateTime2 = actorStats.LastHit;
        }
        double wallElapsedTotal = dateTime1 < dateTime2 ? (dateTime2 - dateTime1).TotalSeconds : elapsed;
        return new DpsSnapshot()
        {
            ElapsedSeconds = elapsed,
            WallElapsedSeconds = wallElapsedTotal,
            TotalPartyDamage = totalPartyDamage,
            Target = mobTarget,
            Players = actorStatsList.Select(p => BuildActorDps(p, totalPartyDamage, maxHp, elapsed, wallElapsedTotal)).ToList()
        };
    }

    private DpsSnapshot? BuildSnapshotForRecord()
    {
        double elapsed;
        if (!_combatActive)
        {
            elapsed = _accumulatedCombatSec;
        }
        else
        {
            elapsed = _accumulatedCombatSec + (_lastDamageTime - _combatStart).TotalSeconds;
        }
        List<ActorStats> list = _actors.Values
            .Where(a => a.TotalDamage > 0L && !string.IsNullOrEmpty(a.WhitelistKey))
            .GroupBy(a => a.WhitelistKey!)
            .Select(g => g.OrderByDescending(a => a.TotalDamage).First())
            .OrderByDescending(a => a.TotalDamage)
            .Take(12)
            .ToList();
        if (list.Count == 0)
            return null;
        long totalPartyDamage = list.Sum(p => p.TotalDamage);
        MobTarget? mobTarget = null;
        MobInfo mobInfo;
        if (_primaryTargetId != 0 && _mobs.TryGetValue(_primaryTargetId, out mobInfo))
            mobTarget = new MobTarget()
            {
                Name = mobInfo.Name,
                MaxHp = mobInfo.MaxHp,
                CurrentHp = mobInfo.CurrentHp,
                TotalDamageReceived = totalPartyDamage,
                IsBoss = mobInfo.IsBoss,
                Debuffs = _buffTracker.GetUptime(_primaryTargetId, elapsed)
            };
        long maxHp = mobTarget != null ? mobTarget.MaxHp : 0L;
        DateTime dateTime1 = list.Where(p => p.FirstHit < DateTime.MaxValue).Select(p => p.FirstHit).DefaultIfEmpty(DateTime.MaxValue).Min();
        DateTime dateTime2 = list.Where(p => p.LastHit > DateTime.MinValue).Select(p => p.LastHit).DefaultIfEmpty(DateTime.MinValue).Max();
        double num3 = dateTime1 < dateTime2 ? (dateTime2 - dateTime1).TotalSeconds : elapsed;
        return new DpsSnapshot()
        {
            ElapsedSeconds = elapsed,
            WallElapsedSeconds = num3,
            TotalPartyDamage = totalPartyDamage,
            Target = mobTarget,
            Players = list.Select(p =>
            {
                double wallElapsed = p.FirstHit < p.LastHit ? (p.LastHit - p.FirstHit).TotalSeconds : elapsed;
                return BuildActorDps(p, totalPartyDamage, maxHp, elapsed, wallElapsed);
            }).ToList()
        };
    }

    private ActorDps BuildActorDps(
        ActorStats p,
        long totalPartyDamage,
        long maxHp,
        double elapsed,
        double wallElapsed)
    {
        double num1 = totalPartyDamage > 0L ? (double)p.TotalDamage / (double)totalPartyDamage * 100.0 : 0.0;
        double num2 = maxHp > 0L ? (double)p.TotalDamage / (double)maxHp * 100.0 : num1;
        List<SkillDps> list;
        lock (p)
        {
            list = p.Skills.Values
                .OrderByDescending(s => s.TotalDamage)
                .Select(s =>
                {
                    string? iconStr;
                    return new SkillDps()
                    {
                        Name = s.SkillName,
                        IconUrl = _skillIcons.TryGetValue(s.SkillCode / 10000 * 10000, out iconStr) ? iconStr : _db.GetSkillIcon(s.SkillCode) ?? "",
                        SkillType = _db.GetSkillType(s.SkillCode),
                        Specs = s.Specs,
                        TotalDamage = s.TotalDamage,
                        Percent = p.TotalDamage > 0L ? (double)s.TotalDamage / (double)p.TotalDamage * 100.0 : 0.0,
                        HitCount = s.HitCount,
                        NormalCount = s.NormalCount,
                        CritCount = s.CritCount,
                        BackCount = s.BackCount,
                        HardHitCount = s.HardHitCount,
                        PerfectCount = s.PerfectCount,
                        MultiHitCount = s.MultiHitCount,
                        BlockCount = s.BlockCount,
                        EvadeCount = s.EvadeCount,
                        Dps = wallElapsed > 0.0 ? (long)((double)s.TotalDamage / wallElapsed) : 0L,
                        PartyDps = wallElapsed > 0.0 ? (long)((double)s.TotalDamage / wallElapsed) : 0L,
                        WallDps = wallElapsed > 0.0 ? (long)((double)s.TotalDamage / wallElapsed) : 0L,
                        MinDamage = s.MinDamage == long.MaxValue ? 0L : s.MinDamage,
                        MaxDamage = s.MaxDamage,
                        AvgDamage = s.HitCount > 0 ? s.TotalDamage / (long)s.HitCount : 0L
                    };
                })
                .ToList();
        }
        return new ActorDps()
        {
            EntityId = p.EntityId,
            Name = p.Name,
            JobCode = p.JobCode,
            ServerId = p.ServerId,
            CombatScore = p.CombatScore,
            CombatPower = p.CombatPower,
            TotalDamage = p.TotalDamage,
            Dps = wallElapsed > 0.0 ? (long)((double)p.TotalDamage / wallElapsed) : 0L,
            PartyDps = wallElapsed > 0.0 ? (long)((double)p.TotalDamage / wallElapsed) : 0L,
            WallDps = wallElapsed > 0.0 ? (long)((double)p.TotalDamage / wallElapsed) : 0L,
            DamagePercent = num1,
            BossHpPercent = num2,
            CritRate = p.HitCount > 0 ? (double)p.CritCount / (double)p.HitCount * 100.0 : 0.0,
            HealTotal = p.HealTotal,
            TopSkills = list,
            BuffUptime = _buffTracker.GetUptimeByCaster(p.EntityId, wallElapsed)
        };
    }

    private void EmitSnapshot()
    {
        try
        {
            DpsSnapshot? dpsSnapshot = BuildSnapshot();
            if (dpsSnapshot == null)
                return;
            Action<DpsSnapshot>? dpsUpdated = DpsUpdated;
            if (dpsUpdated == null)
                return;
            dpsUpdated(dpsSnapshot);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dps] 스냅샷 오류: " + ex.Message);
        }
    }

    private void SaveCombatRecord()
    {
        if (_combatRecordSaved)
            return;
        try
        {
            DpsSnapshot? dpsSnapshot = BuildSnapshotForRecord();
            if (dpsSnapshot == null || !dpsSnapshot.Players.Any(p => p.TotalDamage > 0L))
                return;
            _combatRecordSaved = true;
            _combatRecordSavedAt = DateTime.UtcNow;
            foreach (ActorDps player in dpsSnapshot.Players)
                player.IsUploader = _selfKey != null && $"{player.Name}:{player.ServerId}" == _selfKey;
            CombatRecord record = new CombatRecord();
            record.ElapsedSeconds = dpsSnapshot.ElapsedSeconds;
            record.TotalPartyDamage = dpsSnapshot.TotalPartyDamage;
            record.Target = dpsSnapshot.Target?.Name ?? "";
            MobTarget? target = dpsSnapshot.Target;
            record.TargetMaxHp = target != null ? target.MaxHp : 0L;
            record.Players = dpsSnapshot.Players;
            record.Timeline = _timeline.Count > 0 ? new List<TimelineEntry>(_timeline) : null;
            record.HitLog = _hitLog.Count > 0 ? new List<HitLogEntry>(_hitLog) : null;
            record.BossDebuffs = dpsSnapshot.Target?.Debuffs;
            record.DungeonId = IsDummyByName(dpsSnapshot.Target?.Name ?? "") ? (int?)null : _currentDungeonId;
            _recordStore.SaveOrUpdate(record, _primaryTargetId);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dps] 전투기록 저장 오류: " + ex.Message);
        }
    }

    public List<CombatRecordSummary> GetCombatRecords() => _recordStore.GetAll();

    public DpsSnapshot? GetCombatRecord(string id) => _recordStore.Get(id);

    public void SetDungeonId(int dungeonId) => _currentDungeonId = dungeonId;

    public void ClearDungeonId() => _currentDungeonId = null;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Stop();
        _db.Dispose();
    }
}
