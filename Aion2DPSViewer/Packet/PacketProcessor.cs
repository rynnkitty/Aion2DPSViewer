using Aion2DPSViewer.Dps;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Aion2DPSViewer.Packet;

public sealed class PacketProcessor : IDisposable
{
    private readonly int _serverPort;
    private readonly bool _tcpReorder;
    private readonly int _workerCount;
    private readonly int _maxBufferSize;
    private readonly int _maxReorderBytes;
    private readonly ConcurrentQueue<PacketEntry> _mainQueue = new ConcurrentQueue<PacketEntry>();
    private readonly WorkerChannel[] _workerChannels;
    private Thread? _dispatcherThread;
    private readonly Thread[] _workerThreads;
    private volatile bool _running;
    private readonly ManualResetEventSlim _mainSignal = new ManualResetEventSlim(false);
    private long _lastReassemblerDiagTick;
    private uint _combatPort = uint.MaxValue;
    private string _combatDevice = "";
    private readonly object _portLock = new object();
    private readonly ConcurrentDictionary<ulong, ChannelState> _channels = new ConcurrentDictionary<ulong, ChannelState>();

    public event DamageHandler? OnDamage;
    public event MobSpawnHandler? OnMobSpawn;
    public event SummonHandler? OnSummon;
    public event UserInfoHandler? OnUserInfo;
    public event CombatPowerHandler? OnCombatPower;
    public event CombatPowerByNameHandler? OnCombatPowerByName;
    public event EntityRemovedHandler? OnEntityRemoved;
    public event BossHpHandler? OnBossHp;
    public event BuffHandler? OnBuff;
    public event LogHandler? OnLog;

    public GetSkillNameFunc? GetSkillName { get; set; }
    public ContainsSkillCodeFunc? ContainsSkillCode { get; set; }
    public SkillDatabase? SkillDb { get; set; }
    public IsMobBossFunc? IsMobBoss { get; set; }
    public Func<int, string?>? GetServerName { get; set; }
    public HashSet<int>? ValidServerIds { get; set; }

    public PacketProcessor(
        int serverPort = 0,
        bool tcpReorder = true,
        int workerCount = 0,
        int maxBufferSize = 0,
        int maxReorderBytes = 0)
    {
        _serverPort = serverPort;
        _tcpReorder = tcpReorder;
        _workerCount = workerCount > 0 ? workerCount : Math.Max(2, Environment.ProcessorCount / 2);
        _maxBufferSize = maxBufferSize > 0 ? maxBufferSize : 2097152;
        _maxReorderBytes = maxReorderBytes > 0 ? maxReorderBytes : 131072;
        _workerChannels = new WorkerChannel[_workerCount];
        _workerThreads = new Thread[_workerCount];
        for (int index = 0; index < _workerCount; ++index)
            _workerChannels[index] = new WorkerChannel();
    }

    public void Start()
    {
        if (_running)
            return;
        _running = true;
        _dispatcherThread = new Thread(new ThreadStart(DispatcherLoop))
        {
            IsBackground = true,
            Name = "PP-Dispatcher"
        };
        _dispatcherThread.Start();
        for (int index = 0; index < _workerCount; ++index)
        {
            int idx = index; // capture for lambda
            Thread thread = new Thread(() => WorkerLoop(idx))
            {
                IsBackground = true,
                Name = $"PP-Worker-{idx}"
            };
            _workerThreads[index] = thread;
            _workerThreads[index].Start();
        }
    }

    public void Stop()
    {
        if (!_running)
            return;
        _running = false;
        _mainSignal.Set();
        foreach (WorkerChannel workerChannel in _workerChannels)
            workerChannel.Signal.Set();
        _dispatcherThread?.Join(2000);
        foreach (Thread workerThread in _workerThreads)
            workerThread?.Join(2000);
    }

    public void Enqueue(
        int srcPort,
        int dstPort,
        byte[] data,
        int dataLen,
        string? deviceName,
        uint seqNum)
    {
        if (!_running || data == null || dataLen < 1)
            return;
        byte[] Data = new byte[dataLen];
        Buffer.BlockCopy(data, 0, Data, 0, dataLen);
        _mainQueue.Enqueue(new PacketEntry(srcPort, dstPort, Data, deviceName ?? "", seqNum));
        _mainSignal.Set();
    }

    public int GetCombatPort()
    {
        lock (_portLock)
            return (int)_combatPort;
    }

    public string GetCombatDevice()
    {
        lock (_portLock)
            return _combatDevice;
    }

    public bool DumpUnparsedMessages { get; set; }

    public void Reset()
    {
        lock (_portLock)
        {
            _combatPort = uint.MaxValue;
            _combatDevice = "";
        }
        _channels.Clear();
        PacketEntry packetEntry;
        while (_mainQueue.TryDequeue(out packetEntry)) ;
    }

    public void Dispose()
    {
        Stop();
        _mainSignal.Dispose();
        foreach (WorkerChannel workerChannel in _workerChannels)
            workerChannel.Signal.Dispose();
    }

    private void DispatcherLoop()
    {
        while (_running)
        {
            _mainSignal.Wait(100);
            _mainSignal.Reset();
            PacketEntry packetEntry;
            while (_mainQueue.TryDequeue(out packetEntry))
            {
                WorkerChannel workerChannel = _workerChannels[GetChannelIndex(packetEntry.SrcPort, packetEntry.DstPort, _workerCount)];
                workerChannel.Queue.Enqueue(packetEntry);
                workerChannel.Signal.Set();
            }
        }
    }

    private void WorkerLoop(int channelIdx)
    {
        WorkerChannel workerChannel = _workerChannels[channelIdx];
        while (_running)
        {
            workerChannel.Signal.Wait(100);
            workerChannel.Signal.Reset();
            PacketEntry pkt;
            while (workerChannel.Queue.TryDequeue(out pkt))
                ProcessPacket(pkt);
        }
    }

    private void ProcessPacket(PacketEntry pkt)
    {
        uint combatPort = _combatPort;
        if (combatPort != uint.MaxValue)
        {
            if (pkt.SrcPort != (int)combatPort && pkt.DstPort != (int)combatPort)
                return;
        }
        else
        {
            lock (_portLock)
            {
                if (_combatPort == uint.MaxValue)
                {
                    if (_serverPort > 0)
                    {
                        if (pkt.SrcPort == _serverPort || pkt.DstPort == _serverPort)
                        {
                            _combatPort = (uint)_serverPort;
                            _combatDevice = pkt.DeviceName;
                        }
                    }
                    else
                    {
                        if (!ProtocolUtils.IsGamePacket(pkt.Data))
                            return;
                        _combatPort = (uint)Math.Max(pkt.SrcPort, pkt.DstPort);
                        _combatDevice = pkt.DeviceName;
                    }
                    if (_combatPort == uint.MaxValue)
                        return;
                    Log(0, $"[PacketProcessor] Combat port detected: {_combatPort} on {_combatDevice}");
                }
                else if (pkt.SrcPort != (int)_combatPort)
                {
                    if (pkt.DstPort != (int)_combatPort)
                        return;
                }
            }
        }
        ChannelState orAdd = _channels.GetOrAdd(GetChannelKey(pkt.SrcPort, pkt.DstPort), _ => new ChannelState(this));
        bool flag = pkt.SrcPort == (int)_combatPort;
        TcpReassembler tcpReassembler = flag ? orAdd.ServerToClient : orAdd.ClientToServer;
        if (_tcpReorder)
        {
            tcpReassembler.Feed(pkt.SeqNum, pkt.Data);
            if (!flag || tcpReassembler.DiagInfo == null)
                return;
            long tickCount64 = (long)(uint)Environment.TickCount;
            if (tickCount64 - _lastReassemblerDiagTick <= 30000L)
                return;
            _lastReassemblerDiagTick = tickCount64;
            Log(0, "[TcpReassembler] S2C: " + tcpReassembler.DiagInfo);
        }
        else
            orAdd.StreamProcessor.ProcessData(pkt.Data);
    }

    internal static int GetChannelIndex(int srcPort, int dstPort, int workerCount)
    {
        return (int)((uint)(Math.Min(srcPort, dstPort) * 397 ^ Math.Max(srcPort, dstPort)) % (uint)workerCount);
    }

    internal static ulong GetChannelKey(int srcPort, int dstPort)
    {
        return (ulong)(uint)Math.Min(srcPort, dstPort) << 32 | (ulong)(uint)Math.Max(srcPort, dstPort);
    }

    internal void Log(int level, string message)
    {
        LogHandler? onLog = OnLog;
        if (onLog == null)
            return;
        onLog(level, message);
    }

    internal void FireDamage(
        int actorId,
        int targetId,
        int skillCode,
        byte damageType,
        int damage,
        uint specialFlags,
        int multiHitCount,
        int multiHitDamage,
        int healAmount,
        int isDot)
    {
        DamageHandler? onDamage = OnDamage;
        if (onDamage == null)
            return;
        onDamage(actorId, targetId, skillCode, damageType, damage, specialFlags, multiHitCount, multiHitDamage, healAmount, isDot);
    }

    internal void FireMobSpawn(int mobId, int mobCode, int hp, int isBoss)
    {
        MobSpawnHandler? onMobSpawn = OnMobSpawn;
        if (onMobSpawn == null)
            return;
        onMobSpawn(mobId, mobCode, hp, isBoss);
    }

    internal void FireSummon(int actorId, int petId)
    {
        SummonHandler? onSummon = OnSummon;
        if (onSummon == null)
            return;
        onSummon(actorId, petId);
    }

    internal void FireUserInfo(
        int entityId,
        string nickname,
        int serverId,
        int jobCode,
        int isSelf)
    {
        UserInfoHandler? onUserInfo = OnUserInfo;
        if (onUserInfo == null)
            return;
        onUserInfo(entityId, nickname, serverId, jobCode, isSelf);
    }

    internal void FireCombatPower(int entityId, int combatPower)
    {
        CombatPowerHandler? onCombatPower = OnCombatPower;
        if (onCombatPower == null)
            return;
        onCombatPower(entityId, combatPower);
    }

    internal void FireCombatPowerByName(string nickname, int serverId, int combatPower)
    {
        CombatPowerByNameHandler? combatPowerByName = OnCombatPowerByName;
        if (combatPowerByName == null)
            return;
        combatPowerByName(nickname, serverId, combatPower);
    }

    internal void FireEntityRemoved(int entityId)
    {
        EntityRemovedHandler? onEntityRemoved = OnEntityRemoved;
        if (onEntityRemoved == null)
            return;
        onEntityRemoved(entityId);
    }

    internal void FireBossHp(int entityId, int currentHp)
    {
        BossHpHandler? onBossHp = OnBossHp;
        if (onBossHp == null)
            return;
        onBossHp(entityId, currentHp);
    }

    internal void FireBuff(
        int entityId,
        int buffId,
        int type,
        uint durationMs,
        long timestamp,
        int casterId)
    {
        BuffHandler? onBuff = OnBuff;
        if (onBuff == null)
            return;
        onBuff(entityId, buffId, type, durationMs, timestamp, casterId);
    }

    public delegate void DamageHandler(
        int actorId,
        int targetId,
        int skillCode,
        byte damageType,
        int damage,
        uint specialFlags,
        int multiHitCount,
        int multiHitDamage,
        int healAmount,
        int isDot);

    public delegate void MobSpawnHandler(int mobId, int mobCode, int hp, int isBoss);
    public delegate void SummonHandler(int actorId, int petId);

    public delegate void UserInfoHandler(
        int entityId,
        string nickname,
        int serverId,
        int jobCode,
        int isSelf);

    public delegate void CombatPowerHandler(int entityId, int combatPower);
    public delegate void CombatPowerByNameHandler(string nickname, int serverId, int combatPower);
    public delegate void EntityRemovedHandler(int entityId);
    public delegate void BossHpHandler(int entityId, int currentHp);

    public delegate void BuffHandler(
        int entityId,
        int buffId,
        int type,
        uint durationMs,
        long timestamp,
        int casterId);

    public delegate void LogHandler(int level, string message);
    public delegate string? GetSkillNameFunc(int skillCode);
    public delegate bool ContainsSkillCodeFunc(int skillCode);
    public delegate bool IsMobBossFunc(int mobCode);
}
