using PacketDotNet;
using SharpPcap;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Aion2DPSViewer.Packet;

public class PacketSniffer
{
    private readonly PartyStreamParser _partyParser = new PartyStreamParser();
    private readonly List<ILiveDevice> _devices = new List<ILiveDevice>();
    private bool _running;
    private static readonly byte[] Magic = new byte[3]
    {
        (byte)6, (byte)0, (byte)54
    };
    private int _serverPort;
    private readonly object _portLock = new object();
    private DateTime _lastServerPacketTime;
    private const int PortTimeoutSeconds = 5;
    private Timer? _portWatchTimer;
    private static readonly byte[] NickAnchor = new byte[4]
    {
        (byte)79, (byte)54, (byte)0, (byte)0
    };
    private static readonly byte[] PartyTerminator = new byte[3]
    {
        (byte)6, (byte)0, (byte)54
    };
    private static readonly byte[] ServerSuffix = new byte[4]
    {
        (byte)45, (byte)0, (byte)1, (byte)0
    };
    private byte[]? _cvBuf;
    private const int CvMax = 4096;
    private readonly ConcurrentQueue<byte[]> _parseQueue = new ConcurrentQueue<byte[]>();
    private readonly ManualResetEventSlim _parseSignal = new ManualResetEventSlim(false);
    private Thread? _parseThread;
    private readonly ConcurrentDictionary<string, DateTime> _seen = new ConcurrentDictionary<string, DateTime>();
    private DateTime _lastSeenCleanup = DateTime.UtcNow;
    private double _rttSendTimeUs;
    private bool _rttPending;
    private int _currentPingMs = -1;
    private double _smoothedPing = -1.0;
    private const double PingAlpha = 0.125;
    private long _lastPingNotifyTicks;
    private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

    public event Action<List<PartyMember>>? PartyList;
    public event Action<List<PartyMember>>? PartyUpdate;
    public event Action<PartyMember>? PartyRequest;
    public event Action<PartyMember>? PartyAccept;
    public event Action? PartyLeft;
    public event Action? PartyEjected;
    public event Action<CharacterResult>? Character;
    public event Action<int, int>? DungeonDetected;
    public event Action<string, int, int>? CombatPowerDetected;
    public event Action<int, int, byte[], uint>? RawPacket;
    public event Action<int>? ServerPortDetected;
    public event Action? ServerPortReset;
    public event Action<int>? PingUpdated;

    public int PingMs => _currentPingMs;
    public int ServerPort => _serverPort;

    public Task StartAsync()
    {
        _partyParser.PartyList += m =>
        {
            Action<List<PartyMember>>? partyList = PartyList;
            if (partyList == null) return;
            partyList(m);
        };
        _partyParser.PartyUpdate += m =>
        {
            Action<List<PartyMember>>? partyUpdate = PartyUpdate;
            if (partyUpdate == null) return;
            partyUpdate(m);
        };
        _partyParser.PartyRequest += m =>
        {
            Action<PartyMember>? partyRequest = PartyRequest;
            if (partyRequest == null) return;
            partyRequest(m);
        };
        _partyParser.PartyAccept += m =>
        {
            Action<PartyMember>? partyAccept = PartyAccept;
            if (partyAccept == null) return;
            partyAccept(m);
        };
        _partyParser.PartyLeft += () =>
        {
            Action? partyLeft = PartyLeft;
            if (partyLeft == null) return;
            partyLeft();
        };
        _partyParser.DungeonDetected += (id, stage) =>
        {
            Action<int, int>? dungeonDetected = DungeonDetected;
            if (dungeonDetected == null) return;
            dungeonDetected(id, stage);
        };
        _partyParser.CombatPowerDetected += (nick, sid, cp) =>
        {
            Action<string, int, int>? combatPowerDetected = CombatPowerDetected;
            if (combatPowerDetected == null) return;
            combatPowerDetected(nick, sid, cp);
        };
        _partyParser.PartyEjected += () =>
        {
            Action? partyEjected = PartyEjected;
            if (partyEjected == null) return;
            partyEjected();
        };
        CaptureDeviceList instance = CaptureDeviceList.Instance;
        if (instance.Count == 0)
            throw new InvalidOperationException("Npcap 장치를 찾을 수 없습니다. Npcap이 설치되어 있는지 확인하세요.");
        foreach (ILiveDevice device in (ReadOnlyCollection<ILiveDevice>)instance)
            StartCapture(device);
        _running = true;
        _parseThread = new Thread(new ThreadStart(ParseLoop))
        {
            IsBackground = true,
            Name = "SnifferParse"
        };
        _parseThread.Start();
        _portWatchTimer = new Timer(new TimerCallback(CheckPortTimeout), null, 1000, 1000);
        Console.Error.WriteLine($"[sniffer] Npcap 캡처 시작 ({_devices.Count}개 장치)");
        return Task.CompletedTask;
    }

    private void StartCapture(ILiveDevice device)
    {
        try
        {
            device.OnPacketArrival += new PacketArrivalEventHandler(OnPacketArrival);
            device.Open(read_timeout: 250);
            device.Filter = "tcp";
            device.StartCapture();
            _devices.Add(device);
            Console.Error.WriteLine("[sniffer] 장치 캡처 시작: " + device.Name);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[sniffer] 장치 캡처 실패: {device.Name} — {ex.Message}");
        }
    }

    private void CheckPortTimeout(object? _)
    {
        if (_serverPort == 0 || (DateTime.UtcNow - _lastServerPacketTime).TotalSeconds <= 5.0)
            return;
        lock (_portLock)
        {
            if (_serverPort == 0)
                return;
            Console.Error.WriteLine($"[sniffer] 서버 패킷 타임아웃 ({5}초) — 포트 리셋");
            _serverPort = 0;
            _currentPingMs = -1;
            _smoothedPing = -1.0;
            _rttPending = false;
            ApplyPortFilter(0);
            Action? serverPortReset = ServerPortReset;
            if (serverPortReset == null)
                return;
            serverPortReset();
        }
    }

    public void Stop()
    {
        _running = false;
        _parseSignal.Set();
        _parseThread?.Join(2000);
        _parseThread = null;
        _portWatchTimer?.Dispose();
        _portWatchTimer = null;
        foreach (ILiveDevice device in _devices)
        {
            try
            {
                device.StopCapture();
                device.Close();
            }
            catch { }
        }
        _devices.Clear();
        _serverPort = 0;
    }

    private void OnPacketArrival(object sender, PacketCapture e)
    {
        try
        {
            RawCapture packet = e.GetPacket();
            TcpPacket tcp = PacketDotNet.Packet.ParsePacket(packet.LinkLayerType, packet.Data).Extract<TcpPacket>();
            if (tcp == null)
                return;
            int sourcePort = (int)tcp.SourcePort;
            int destinationPort = (int)tcp.DestinationPort;
            PosixTimeval timeval = packet.Timeval;
            double captureTimeUs = (double)timeval.Seconds * 1000000.0 + (double)timeval.MicroSeconds;
            MeasureRtt(tcp, sourcePort, destinationPort, captureTimeUs);
            byte[] payloadData = tcp.PayloadData;
            if (payloadData == null || payloadData.Length == 0)
                return;
            uint sequenceNumber = tcp.SequenceNumber;
            ProcessPacket(sourcePort, destinationPort, payloadData, sequenceNumber);
        }
        catch (ArgumentException) { }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[sniffer] OnPacketArrival 예외: " + ex.Message);
        }
    }

    private void MeasureRtt(TcpPacket tcp, int srcPort, int dstPort, double captureTimeUs)
    {
        int serverPort = _serverPort;
        if (serverPort == 0)
            return;
        byte[] payloadData = tcp.PayloadData;
        bool flag = payloadData != null && payloadData.Length != 0;
        if (dstPort == serverPort & flag && !_rttPending)
        {
            _rttSendTimeUs = captureTimeUs;
            _rttPending = true;
        }
        else
        {
            if (!(srcPort == serverPort & flag) || !_rttPending)
                return;
            _rttPending = false;
            double num = (captureTimeUs - _rttSendTimeUs) / 1000.0;
            if (num <= 0.5 || num >= 5000.0)
                return;
            _smoothedPing = _smoothedPing < 0.0 ? num : 0.125 * num + 0.875 * _smoothedPing;
            _currentPingMs = (int)Math.Round(_smoothedPing);
            long timestamp = Stopwatch.GetTimestamp();
            if (timestamp - _lastPingNotifyTicks < Stopwatch.Frequency)
                return;
            _lastPingNotifyTicks = timestamp;
            Action<int>? pingUpdated = PingUpdated;
            if (pingUpdated == null)
                return;
            pingUpdated(_currentPingMs);
        }
    }

    private void ProcessPacket(int srcPort, int dstPort, byte[] payload, uint seqNum)
    {
        if (!_running)
            return;
        try
        {
            if (_serverPort == 0)
            {
                if (srcPort <= 1024)
                    return;
                if (ContainsMagic(payload))
                {
                    lock (_portLock)
                    {
                        if (_serverPort == 0)
                        {
                            _serverPort = srcPort;
                            _lastServerPacketTime = DateTime.UtcNow;
                            Console.Error.WriteLine($"[sniffer] 서버 포트 감지: {srcPort}");
                            ApplyPortFilter(srcPort);
                            Action<int>? serverPortDetected = ServerPortDetected;
                            if (serverPortDetected != null)
                                serverPortDetected(srcPort);
                        }
                    }
                }
                if (_serverPort == 0)
                    return;
            }
            if (srcPort == _serverPort || dstPort == _serverPort)
            {
                _lastServerPacketTime = DateTime.UtcNow;
                Action<int, int, byte[], uint>? rawPacket = RawPacket;
                if (rawPacket != null)
                    rawPacket(srcPort, dstPort, payload, seqNum);
            }
            else if (_serverPort == 0 && srcPort > 1024 && dstPort > 1024)
            {
                Action<int, int, byte[], uint>? rawPacket = RawPacket;
                if (rawPacket != null)
                    rawPacket(srcPort, dstPort, payload, seqNum);
            }
            if (srcPort != _serverPort)
                return;
            _parseQueue.Enqueue(payload);
            _parseSignal.Set();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[sniffer] ProcessPacket 예외: " + ex.Message);
        }
    }

    private void ApplyPortFilter(int port)
    {
        string str2 = port <= 0 ? "tcp" : $"tcp port {port}";
        foreach (ILiveDevice device in _devices)
        {
            try
            {
                device.Filter = str2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[sniffer] 필터 변경 실패: {device.Name} — {ex.Message}");
            }
        }
        Console.Error.WriteLine($"[sniffer] BPF 필터 변경: \"{str2}\"");
    }

    private void ParseLoop()
    {
        while (_running)
        {
            _parseSignal.Wait(100);
            _parseSignal.Reset();
            byte[] numArray1;
            while (_parseQueue.TryDequeue(out numArray1))
            {
                try
                {
                    bool flag = ProtocolUtils.IndexOf(numArray1, NickAnchor) >= 0;
                    if (numArray1.Length > 4)
                    {
                        for (int index1 = 0; index1 < numArray1.Length - 28; ++index1)
                        {
                            if (numArray1[index1] == (byte)107 && numArray1[index1 + 1] == (byte)226)
                            {
                                int index2 = index1 + 26;
                                if (index2 + 4 <= numArray1.Length && numArray1[index2] == (byte)0 && numArray1[index2 + 1] == (byte)0 && numArray1[index2 + 2] == (byte)0 && numArray1[index2 + 3] == (byte)0)
                                {
                                    Console.Error.WriteLine("[sniffer] 존 이동 감지 (0xE26B 퇴장) → 던전 ID 초기화");
                                    Action<int, int>? dungeonDetected = DungeonDetected;
                                    if (dungeonDetected != null)
                                        dungeonDetected(0, 0);
                                    break;
                                }
                                break;
                            }
                        }
                    }
                    if (ProtocolUtils.IndexOf(numArray1, PartyTerminator) >= 0)
                        _partyParser.Feed((ReadOnlySpan<byte>)numArray1);
                    if (flag)
                    {
                        CharacterResult? charView = ParseCharView(numArray1);
                        if (charView != null)
                        {
                            EmitCharView(charView, numArray1);
                            _cvBuf = null;
                        }
                        else
                            _cvBuf = (byte[])numArray1.Clone();
                    }
                    else if (_cvBuf != null)
                    {
                        byte[] numArray2 = new byte[_cvBuf.Length + numArray1.Length];
                        Buffer.BlockCopy(_cvBuf, 0, numArray2, 0, _cvBuf.Length);
                        Buffer.BlockCopy(numArray1, 0, numArray2, _cvBuf.Length, numArray1.Length);
                        CharacterResult? charView = ParseCharView(numArray2);
                        if (charView != null)
                        {
                            EmitCharView(charView, numArray2);
                            _cvBuf = null;
                        }
                        else
                            _cvBuf = numArray2.Length <= 4096 ? numArray2 : null;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[sniffer] ParseLoop 예외: " + ex.Message);
                }
            }
        }
    }

    private static bool ContainsMagic(byte[] data)
    {
        for (int index = 0; index < data.Length - 2; ++index)
        {
            if (data[index] == (byte)6 && data[index + 1] == (byte)0 && data[index + 2] == (byte)54)
                return true;
        }
        return false;
    }

    private static bool IsCleanNickname(string nick)
    {
        foreach (char ch in nick)
        {
            if (ch == '\uFFFD' || (ch < ' ' || ch >= '\u007F' && ch < '가' || ch > '\uD7AF' && ch < '！') && !char.IsLetterOrDigit(ch))
                return false;
        }
        return true;
    }

    private void EmitCharView(CharacterResult result, byte[] rawPayload)
    {
        string str = $"{result.Nickname}:{result.ServerId}";
        DateTime utcNow = DateTime.UtcNow;
        DateTime dateTime1;
        if (_seen.TryGetValue(str, out dateTime1) && (utcNow - dateTime1).TotalMilliseconds < 3000.0)
            return;
        _seen[str] = utcNow;
        if ((utcNow - _lastSeenCleanup).TotalSeconds > 30.0)
        {
            _lastSeenCleanup = utcNow;
            DateTime dateTime2 = utcNow.AddSeconds(-10.0);
            foreach (string key in _seen.Keys)
            {
                DateTime dateTime3;
                if (_seen.TryGetValue(key, out dateTime3) && dateTime3 < dateTime2)
                {
                    DateTime dateTime4;
                    _seen.TryRemove(key, out dateTime4);
                }
            }
        }
        if (IsCleanNickname(result.Nickname) && !result.NickTrimmed)
        {
            Console.Error.WriteLine($"[sniffer] char_view: {result.Nickname} (서버: {result.ServerName}/{result.ServerId})");
            Action<CharacterResult>? character = Character;
            if (character == null)
                return;
            character(result);
        }
        else
        {
            int num = ProtocolUtils.IndexOf(rawPayload, NickAnchor);
            int offset = Math.Max(0, num);
            int maxBytes = Math.Min(128, rawPayload.Length - offset);
            Console.Error.WriteLine($"[sniffer] char_view 닉네임 이상 → 무시: \"{result.Nickname}\" trimmed={result.NickTrimmed} (서버: {result.ServerName}/{result.ServerId}) anchor@{num} raw[{offset}..+{maxBytes}]: {ProtocolUtils.HexDump(rawPayload, offset, maxBytes)}");
        }
    }

    private static CharacterResult? ParseCharView(byte[] payload)
    {
        try
        {
            int num1 = ProtocolUtils.IndexOf(payload, NickAnchor);
            if (num1 < 0)
                return null;
            int index = num1 + 4;
            if (index + 2 > payload.Length || payload[index] != (byte)7)
                return null;
            int len1 = (int)payload[index + 1];
            if (len1 == 0 || index + 2 + len1 > payload.Length)
                return null;
            bool trimmed1;
            string str1 = DecodeNick(payload, index + 2, len1, out trimmed1);
            int start1 = index + 2 + len1;
            if (start1 + 4 <= payload.Length)
            {
                int len2 = BinaryPrimitives.ReadInt32LittleEndian((ReadOnlySpan<byte>)payload.AsSpan(start1));
                if (len2 >= 1 && len2 <= 60 && start1 + 4 + len2 <= payload.Length)
                {
                    bool trimmed2;
                    string str2 = DecodeNick(payload, start1 + 4, len2, out trimmed2);
                    if (str2 != null && str2.Length >= (str1 != null ? str1.Length : 0) && str2.StartsWith(str1 ?? ""))
                    {
                        str1 = str2;
                        start1 = start1 + 4 + len2;
                        trimmed1 = trimmed2;
                    }
                }
            }
            if (str1 == null)
                return null;
            int? nullable = null;
            string str3 = "알 수 없음";
            int start2 = start1 + 16;
            if (start2 + 2 <= payload.Length)
            {
                int id = (int)BinaryPrimitives.ReadUInt16LittleEndian((ReadOnlySpan<byte>)payload.AsSpan(start2));
                string name = ServerMap.GetName(id);
                if (!string.IsNullOrEmpty(name))
                {
                    nullable = id;
                    str3 = name;
                }
            }
            if (!nullable.HasValue)
            {
                int num2 = ProtocolUtils.IndexOf(payload, ServerSuffix);
                for (int start3 = num2 >= 0 ? num2 + 4 : start1; start3 + 2 <= payload.Length; ++start3)
                {
                    int id = (int)BinaryPrimitives.ReadUInt16LittleEndian((ReadOnlySpan<byte>)payload.AsSpan(start3));
                    string name = ServerMap.GetName(id);
                    if (!string.IsNullOrEmpty(name))
                    {
                        nullable = id;
                        str3 = name;
                        break;
                    }
                }
            }
            return new CharacterResult()
            {
                Nickname = str1,
                ServerId = nullable,
                ServerName = str3,
                Type = "char_view",
                NickTrimmed = trimmed1
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? DecodeNick(byte[] buf, int offset, int len, out bool trimmed)
    {
        trimmed = false;
        try
        {
            try
            {
                string str = CleanNickChars(((Encoding)StrictUtf8).GetString(buf, offset, len));
                if (str != null)
                {
                    if (Encoding.UTF8.GetByteCount(str) == len)
                        return str;
                }
            }
            catch (DecoderFallbackException) { }
            if (len >= 9)
            {
                int start = offset + len - 6;
                if (start > offset && start + 5 < buf.Length && buf[start + 2] == (byte)130 && buf[start + 4] == (byte)0 && buf[start + 5] == (byte)0)
                {
                    int num1 = (int)BinaryPrimitives.ReadUInt16LittleEndian((ReadOnlySpan<byte>)buf.AsSpan(start));
                    int num2 = len - 6;
                    int num3 = num2;
                    if (num1 == num3 && num2 >= 3)
                    {
                        if (len % num2 == 0)
                        {
                            try
                            {
                                string str1 = CleanNickChars(((Encoding)StrictUtf8).GetString(buf, offset, num2));
                                if (str1 != null)
                                {
                                    int num4 = len / num2;
                                    string str2 = string.Concat(Enumerable.Repeat<string>(str1, num4));
                                    Console.Error.WriteLine($"[sniffer] 전체반복 압축해제: \"{str1}\"×{num4} → \"{str2}\"");
                                    return str2;
                                }
                            }
                            catch (DecoderFallbackException) { }
                        }
                    }
                }
            }
            if (len >= 5)
            {
                int index = offset + len - 4;
                if (index > offset && index + 3 < buf.Length && buf[index] == (byte)130 && buf[index + 2] == (byte)0 && buf[index + 3] == (byte)0)
                {
                    int num5 = (int)buf[index + 1];
                    int num6 = len - 4;
                    if (num5 >= 2 && num5 <= 30)
                    {
                        if (num6 >= 1)
                        {
                            try
                            {
                                string str3 = CleanNickChars(((Encoding)StrictUtf8).GetString(buf, offset, num6));
                                if (str3 != null && str3.Length >= 2)
                                {
                                    char ch = str3[str3.Length - 1];
                                    string str6 = str3.Substring(0, str3.Length - 1) + new string(ch, num5);
                                    Console.Error.WriteLine($"[sniffer] 부분반복 압축해제: \"{str3}\" → \"{str6}\" ('{ch}'×{num5})");
                                    return str6;
                                }
                            }
                            catch (DecoderFallbackException) { }
                        }
                    }
                }
            }
            for (int baseLen = Math.Min(len - 2, 36); baseLen >= 3; --baseLen)
            {
                try
                {
                    string str7 = CleanNickChars(((Encoding)StrictUtf8).GetString(buf, offset, baseLen));
                    if (str7 != null)
                    {
                        int idx = offset + baseLen;
                        if (idx + 1 < offset + len)
                        {
                            if ((int)buf[idx] == (int)(byte)baseLen)
                            {
                                if (buf[idx + 1] == (byte)0)
                                {
                                    int num7 = idx + 2;
                                    int dataLen = len - baseLen - 2;
                                    string? text = DecompressNick(buf, offset, baseLen, num7, dataLen);
                                    if (text != null)
                                    {
                                        string str8 = CleanNickChars(text);
                                        if (str8 != null)
                                        {
                                            Console.Error.WriteLine($"[sniffer] 압축해제(패턴C): \"{str7}\" → \"{str8}\" nickLen={len}");
                                            return str8;
                                        }
                                    }
                                    if (len % baseLen == 0 && len > baseLen)
                                    {
                                        int num8 = len / baseLen;
                                        string str9 = string.Concat(Enumerable.Repeat<string>(str7, num8));
                                        Console.Error.WriteLine($"[sniffer] 압축해제 반복fallback: \"{str7}\"×{num8} → \"{str9}\" nickLen={len} data={ProtocolUtils.HexDump(buf, num7, Math.Min(dataLen, 16))}");
                                        return str9;
                                    }
                                    Console.Error.WriteLine($"[sniffer] 압축 닉네임(패턴C): base=\"{str7}\" baseLen={baseLen} nickLen={len} (접두어 반환) data={ProtocolUtils.HexDump(buf, num7, Math.Min(dataLen, 16))}");
                                    return str7;
                                }
                            }
                        }
                    }
                }
                catch (DecoderFallbackException) { }
            }
            for (int index = len - 1; index >= 3; --index)
            {
                try
                {
                    string str = CleanNickChars(((Encoding)StrictUtf8).GetString(buf, offset, index));
                    if (str != null)
                    {
                        trimmed = true;
                        return str;
                    }
                }
                catch (DecoderFallbackException) { }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? DecompressNick(
        byte[] buf,
        int baseOffset,
        int baseLen,
        int dataOffset,
        int dataLen)
    {
        if (dataLen <= 0)
            return null;
        try
        {
            List<byte> byteList = new List<byte>(baseLen * 6);
            for (int index = 0; index < baseLen; ++index)
                byteList.Add(buf[baseOffset + index]);
            int num1 = dataOffset;
            int num2 = dataOffset + dataLen;
            while (num1 < num2)
            {
                byte num3 = buf[num1++];
                switch (num3)
                {
                    case 130:
                        if (num1 < num2)
                        {
                            int index2 = num1 + 1;
                            while (index2 < num2 && buf[index2] == (byte)0)
                                ++index2;
                            for (int index3 = baseLen + dataLen + 2 - byteList.Count; index3 >= baseLen; index3 -= baseLen)
                            {
                                for (int index4 = 0; index4 < baseLen; ++index4)
                                    byteList.Add(buf[baseOffset + index4]);
                            }
                        }
                        break;
                    case 240:
                        if (num1 < num2)
                        {
                            int num5 = (int)buf[num1++];
                            if (num5 <= baseLen && num1 + num5 <= num2)
                            {
                                for (int index = 0; index < baseLen - num5; ++index)
                                    byteList.Add(buf[baseOffset + index]);
                                for (int index = 0; index < num5; ++index)
                                    byteList.Add(buf[num1++]);
                                continue;
                            }
                        }
                        break;
                    case 248:
                        for (int index = 0; index < baseLen; ++index)
                            byteList.Add(buf[baseOffset + index]);
                        continue;
                    default:
                        Console.Error.WriteLine($"[sniffer] 알 수 없는 압축 opcode: 0x{num3:X2} baseLen={baseLen} data={ProtocolUtils.HexDump(buf, dataOffset, Math.Min(num2 - dataOffset, 32))}");
                        return null;
                }
            }
            return ((Encoding)StrictUtf8).GetString(byteList.ToArray(), 0, byteList.Count);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[sniffer] 압축해제 실패: " + ex.Message);
            return null;
        }
    }

    private static string? CleanNickChars(string text)
    {
        StringBuilder stringBuilder = new StringBuilder();
        foreach (char ch in text)
        {
            if (ch > '\u001F' && ch != '\u007F')
                stringBuilder.Append(ch);
        }
        return stringBuilder.Length <= 0 ? null : stringBuilder.ToString();
    }
}
