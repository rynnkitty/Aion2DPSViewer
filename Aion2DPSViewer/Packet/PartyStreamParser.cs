using Aion2DPSViewer.Dps;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Aion2DPSViewer.Packet;

public class PartyStreamParser
{
    private static readonly byte[] Magic = new byte[3]
    {
        (byte)6,
        (byte)0,
        (byte)54
    };
    private readonly List<byte> _buffer = new List<byte>();
    private bool _justLeft;
    private bool _boardRefreshing;
    private int _lastDungeonId;

    public event Action<List<PartyMember>>? PartyList;
    public event Action<List<PartyMember>>? PartyUpdate;
    public event Action<PartyMember>? PartyRequest;
    public event Action<PartyMember>? PartyAccept;
    public event Action? PartyLeft;
    public event Action? PartyEjected;
    public event Action<int, int>? DungeonDetected;
    public event Action<string, int, int>? CombatPowerDetected;

    public void Feed(ReadOnlySpan<byte> data)
    {
        _buffer.AddRange((IEnumerable<byte>)data.ToArray());
        Flush();
    }

    public void Reset() => _buffer.Clear();

    private void Flush()
    {
        while (true)
        {
            int num = ProtocolUtils.IndexOf(_buffer, Magic);
            if (num < 0)
                break;
            if (num > 0)
            {
                List<byte> range = _buffer.GetRange(0, num + 3);
                _buffer.RemoveRange(0, num + 3);
                if (range.Count > 3)
                {
                    try
                    {
                        ProcessPacket(range.ToArray());
                    }
                    catch { }
                }
            }
            else
            {
                _buffer.RemoveRange(0, 3);
            }
        }
        if (_buffer.Count > 524288)
        {
            Console.Error.WriteLine($"[party] 버퍼 오버플로우 ({_buffer.Count}B) → 클리어");
            _buffer.Clear();
        }
    }

    private void ProcessPacket(byte[] packet)
    {
        ScanDungeonIdRaw(packet);
        ScanCombatPowerRaw(packet);
        int num1 = 256;
        Span<byte> span1 = packet.AsSpan();
        for (int index = 0; index < num1 && span1.Length > 3; ++index)
        {
            (int value, int length) = ReadVarInt((ReadOnlySpan<byte>)span1);
            if (length < 0)
                break;
            if (span1.Length == value)
            {
                if (length + 1 < span1.Length && span1[length] == byte.MaxValue && span1[length + 1] == byte.MaxValue)
                {
                    if (span1.Length < 10)
                        break;
                    span1 = span1.Slice(10, span1.Length - 10);
                }
                else
                {
                    ParsePerfectPacket((ReadOnlySpan<byte>)span1.Slice(0, span1.Length - 3));
                    break;
                }
            }
            else if (value > span1.Length)
            {
                if (span1.Length < 10 || span1[2] != byte.MaxValue || span1[3] != byte.MaxValue)
                    break;
                span1 = span1.Slice(10, span1.Length - 10);
            }
            else if (value <= 3)
            {
                span1 = span1.Slice(1, span1.Length - 1);
            }
            else
            {
                Span<byte> span2 = span1.Slice(0, value - 3);
                if (span2.Length > 3)
                    ParsePerfectPacket((ReadOnlySpan<byte>)span2);
                int num2 = value - 3;
                span1 = span1.Slice(num2, span1.Length - num2);
            }
        }
    }

    private void ScanDungeonIdRaw(byte[] packet)
    {
        for (int index1 = 0; index1 < packet.Length - 10; ++index1)
        {
            if (packet[index1] == (byte)2 && packet[index1 + 1] == (byte)151)
            {
                int num = index1 + 2;
                if (num + 4 < packet.Length && packet[num + 3] == (byte)0)
                {
                    int start1 = num + 4;
                    (int value, int length) = ReadVarInt((ReadOnlySpan<byte>)packet.AsSpan(start1));
                    if (length >= 0 && value >= 1 && value <= 200)
                    {
                        int index2 = start1 + (length + value);
                        if (index2 + 5 <= packet.Length)
                        {
                            switch (packet[index2])
                            {
                                case 4:
                                case 8:
                                    int start2 = index2 + 1;
                                    int dungeonId = BinaryPrimitives.ReadInt32LittleEndian((ReadOnlySpan<byte>)packet.AsSpan(start2));
                                    if (dungeonId >= 600000 && dungeonId < 700000 && dungeonId != _lastDungeonId)
                                    {
                                        _lastDungeonId = dungeonId;
                                        int stage = start2 + 4 < packet.Length ? (int)packet[start2 + 4] : 0;
                                        EmitDungeon(dungeonId, stage, "raw");
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
        }
    }

    private void ScanCombatPowerRaw(byte[] packet)
    {
        bool flag1 = false;
        for (int index = 0; index < packet.Length - 1; ++index)
        {
            if (packet[index] == (byte)0 && packet[index + 1] == (byte)146)
            {
                flag1 = true;
                break;
            }
        }
        if (!flag1)
            return;
        for (int index1 = packet.Length - 3; index1 >= 21; --index1)
        {
            if (packet[index1] == (byte)6 && packet[index1 + 1] == (byte)0 && packet[index1 + 2] == (byte)54)
            {
                if (index1 < 21)
                    break;
                bool flag2 = true;
                for (int index2 = index1 - 5; index2 < index1; ++index2)
                {
                    if (packet[index2] != (byte)0)
                    {
                        flag2 = false;
                        break;
                    }
                }
                if (flag2)
                {
                    int num1 = BinaryPrimitives.ReadInt32LittleEndian((ReadOnlySpan<byte>)packet.AsSpan(index1 - 9));
                    if (num1 >= 10000 && num1 <= 999999)
                    {
                        int num2 = BinaryPrimitives.ReadInt32LittleEndian((ReadOnlySpan<byte>)packet.AsSpan(index1 - 13));
                        if (num2 >= 1000 && num2 <= 5000 && BinaryPrimitives.ReadInt32LittleEndian((ReadOnlySpan<byte>)packet.AsSpan(index1 - 17)) == 0)
                        {
                            int num3 = BinaryPrimitives.ReadInt32LittleEndian((ReadOnlySpan<byte>)packet.AsSpan(index1 - 21));
                            if (num3 >= 1 && num3 <= 55)
                            {
                                for (int index3 = 0; index3 + 3 < index1 - 21; ++index3)
                                {
                                    int num4 = (int)packet[index3] | (int)packet[index3 + 1] << 8;
                                    if (num4 >= 1001 && num4 <= 2021)
                                    {
                                        int num5 = (int)packet[index3 + 2];
                                        if (num5 >= 3 && num5 <= 48)
                                        {
                                            if (index3 + 3 + num5 <= index1 - 21)
                                            {
                                                try
                                                {
                                                    string str = Encoding.UTF8.GetString(packet, index3 + 3, num5);
                                                    if (!string.IsNullOrEmpty(str) && str.Length >= 2)
                                                    {
                                                        bool flag3 = true;
                                                        foreach (char ch in str)
                                                        {
                                                            if ((ch < '가' || ch > '힣') && (ch < 'a' || ch > 'z') && (ch < 'A' || ch > 'Z') && (ch < '0' || ch > '9'))
                                                            {
                                                                flag3 = false;
                                                                break;
                                                            }
                                                        }
                                                        if (flag3)
                                                        {
                                                            Console.Error.WriteLine($"[party] CP 패킷감지: {str}:{num4} CP={num1} (IL={num2})");
                                                            Action<string, int, int>? combatPowerDetected = CombatPowerDetected;
                                                            if (combatPowerDetected == null)
                                                                return;
                                                            combatPowerDetected(str, num4, num1);
                                                            return;
                                                        }
                                                    }
                                                }
                                                catch { }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private void ParsePerfectPacket(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 3)
            return;
        int length = ReadVarInt(packet).length;
        if (length < 0)
            return;
        int num1 = length;
        if (num1 + 1 >= packet.Length)
            return;
        byte num2 = packet[num1];
        if (packet[num1 + 1] != (byte)151)
            return;
        int dataOffset = num1 + 2;
        switch (num2)
        {
            case 1:
                if (_justLeft)
                {
                    _justLeft = false;
                    break;
                }
                if (dataOffset + 1 < packet.Length && packet[dataOffset] == (byte)0 && packet[dataOffset + 1] == (byte)0)
                {
                    if (dataOffset + 6 <= packet.Length)
                    {
                        int num3 = dataOffset + 2;
                        int dungeonId = BinaryPrimitives.ReadInt32LittleEndian(packet.Slice(num3, packet.Length - num3));
                        if (dungeonId >= 600000 && dungeonId < 700000)
                        {
                            int stage = dataOffset + 6 < packet.Length ? (int)packet[dataOffset + 6] : 0;
                            EmitDungeon(dungeonId, stage, "01 97");
                        }
                    }
                    if (_boardRefreshing)
                    {
                        _boardRefreshing = false;
                        break;
                    }
                    Console.Error.WriteLine("[party] 01 97 추방 (빈 목록)");
                    Action? partyEjected = PartyEjected;
                    if (partyEjected == null)
                        break;
                    partyEjected();
                    break;
                }
                List<PartyMember> partyMemberBlocks1 = ParsePartyMemberBlocks(packet, dataOffset);
                if (partyMemberBlocks1.Count > 0)
                {
                    Console.Error.WriteLine($"[party] 01 97 파티목록 {partyMemberBlocks1.Count}명: {string.Join(", ", partyMemberBlocks1.Select(m => $"{m.Nickname}({m.JobName} Lv{m.Level} CP{m.CombatPower})"))}");
                    Action<List<PartyMember>>? partyList = PartyList;
                    if (partyList == null)
                        break;
                    partyList(partyMemberBlocks1);
                    break;
                }
                Console.Error.WriteLine($"[party] 01 97 파싱실패 ({packet.Length}B): {ProtocolUtils.HexDump(packet)}");
                break;
            case 2:
                TryParseDungeonId(packet, dataOffset);
                List<PartyMember> partyMemberBlocks2 = ParsePartyMemberBlocks(packet, dataOffset);
                if (partyMemberBlocks2.Count > 0)
                {
                    Console.Error.WriteLine($"[party] 02 97 업데이트 {partyMemberBlocks2.Count}명: {string.Join(", ", partyMemberBlocks2.Select(m => $"{m.Nickname}({m.JobName})"))}");
                    Action<List<PartyMember>>? partyUpdate = PartyUpdate;
                    if (partyUpdate == null)
                        break;
                    partyUpdate(partyMemberBlocks2);
                    break;
                }
                Console.Error.WriteLine($"[party] 02 97 파싱실패 ({packet.Length}B): {ProtocolUtils.HexDump(packet)}");
                break;
            case 4:
                if (_lastDungeonId == 0)
                    break;
                Console.Error.WriteLine("[party] 04 97 던전 퇴장 감지");
                _lastDungeonId = 0;
                Action<int, int>? dungeonDetected1 = DungeonDetected;
                if (dungeonDetected1 == null)
                    break;
                dungeonDetected1(0, 0);
                break;
            case 7:
                Console.Error.WriteLine($"[party] 07 97 hex ({packet.Length}B): {ProtocolUtils.HexDump(packet)}");
                PartyMember? partyRequest1 = ParsePartyRequest(packet, dataOffset);
                if (partyRequest1 != null)
                {
                    Console.Error.WriteLine($"[party] 07 97 신청: {partyRequest1.Nickname}({partyRequest1.JobName} Lv{partyRequest1.Level} CP{partyRequest1.CombatPower})");
                    Action<PartyMember>? partyRequest2 = PartyRequest;
                    if (partyRequest2 == null)
                        break;
                    partyRequest2(partyRequest1);
                    break;
                }
                Console.Error.WriteLine("[party] 07 97 파싱실패");
                break;
            case 11:
                PartyMember? partyAcceptMember = ParsePartyAcceptMember(packet, dataOffset);
                if (partyAcceptMember != null)
                {
                    Console.Error.WriteLine($"[party] 0B 97 수락: {partyAcceptMember.Nickname}({partyAcceptMember.JobName} Lv{partyAcceptMember.Level} CP{partyAcceptMember.CombatPower})");
                    Action<PartyMember>? partyAccept = PartyAccept;
                    if (partyAccept == null)
                        break;
                    partyAccept(partyAcceptMember);
                    break;
                }
                Console.Error.WriteLine($"[party] 0B 97 파싱실패 ({packet.Length}B): {ProtocolUtils.HexDump(packet)}");
                break;
            case 29:
                _justLeft = true;
                _lastDungeonId = 0;
                Console.Error.WriteLine("[party] 1D 97 퇴장");
                Action? partyLeft = PartyLeft;
                if (partyLeft == null)
                    break;
                partyLeft();
                break;
            case 42:
                _boardRefreshing = true;
                break;
            default:
                int dOffset = dataOffset;
                Console.Error.WriteLine($"[party] {num2:X2} 97 미지 opcode ({packet.Length}B): {ProtocolUtils.HexDump(packet.Slice(dOffset, packet.Length - dOffset), maxBytes: 64)}");
                break;
        }
    }

    private void TryParseDungeonId(ReadOnlySpan<byte> packet, int dataOffset)
    {
        try
        {
            int num1 = dataOffset;
            if (num1 + 4 >= packet.Length)
                return;
            int num2 = num1 + 3;
            if (packet[num2] != (byte)0)
                return;
            int num3 = num2 + 1;
            int num4 = num3;
            (int value, int length) = ReadVarInt(packet.Slice(num4, packet.Length - num4));
            if (length < 0 || value < 0)
                return;
            int num5 = num3 + length + value;
            if (num5 >= packet.Length)
                return;
            int num6 = num5 + 1;
            if (num6 + 4 > packet.Length)
                return;
            int num7 = num6;
            int dungeonId = BinaryPrimitives.ReadInt32LittleEndian(packet.Slice(num7, packet.Length - num7));
            int num8 = num6 + 4;
            Console.Error.WriteLine($"[party] 02 97 dungeonId raw={dungeonId} (0x{dungeonId:X8}) last={_lastDungeonId}");
            if (dungeonId == 0)
            {
                if (_lastDungeonId == 0)
                    return;
                _lastDungeonId = 0;
                Console.Error.WriteLine("[party] 02 97 던전 퇴장 감지 (dungeonId=0)");
                Action<int, int>? dungeonDetected = DungeonDetected;
                if (dungeonDetected == null)
                    return;
                dungeonDetected(0, 0);
            }
            else
            {
                if (dungeonId < 600000 || dungeonId >= 700000)
                    return;
                int stage = num8 < packet.Length ? (int)packet[num8] : 0;
                EmitDungeon(dungeonId, stage, "02 97");
            }
        }
        catch { }
    }

    private void EmitDungeon(int dungeonId, int stage, string source)
    {
        string name = DungeonMap.GetName(dungeonId);
        Console.Error.WriteLine($"[party] {source} 던전감지: {name}");
        try
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "dungeon_log.txt"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {name}\n");
        }
        catch { }
        Action<int, int>? dungeonDetected = DungeonDetected;
        if (dungeonDetected == null)
            return;
        dungeonDetected(dungeonId, stage);
    }

    private static List<PartyMember> ParsePartyMemberBlocks(ReadOnlySpan<byte> packet, int dataOffset)
    {
        List<PartyMember> partyMemberBlocks = new List<PartyMember>();
        HashSet<uint> uintSet = new HashSet<uint>();
        int num1 = dataOffset + 11;
        while (num1 < packet.Length)
        {
            int num2 = (int)packet[num1];
            if (num2 < 1 || num2 > 48)
                ++num1;
            else if (num1 + 1 + num2 + 12 > packet.Length)
            {
                ++num1;
            }
            else
            {
                string nick;
                try
                {
                    nick = Encoding.UTF8.GetString(packet.Slice(num1 + 1, num2).ToArray());
                }
                catch
                {
                    ++num1;
                    continue;
                }
                if (!IsValidNickname(nick))
                {
                    ++num1;
                }
                else
                {
                    int num3 = num1 + 1 + num2;
                    int num4 = num3;
                    uint num5 = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(num4, packet.Length - num4));
                    int num6 = num3 + 4;
                    uint num7 = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(num6, packet.Length - num6));
                    int num8 = num3 + 8;
                    uint num9 = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(num8, packet.Length - num8));
                    if (num7 < 1U || num7 > 55U)
                        ++num1;
                    else if (num9 > 9999999U)
                        ++num1;
                    else if (num1 < 8)
                    {
                        ++num1;
                    }
                    else
                    {
                        int num10 = num1 - 8;
                        uint num11 = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(num10, packet.Length - num10));
                        int id = (int)packet[num1 - 2] | (int)packet[num1 - 1] << 8;
                        if (string.IsNullOrEmpty(ServerMap.GetName(id)))
                        {
                            if (num3 + 14 <= packet.Length)
                                id = (int)packet[num3 + 12] | (int)packet[num3 + 13] << 8;
                            if (string.IsNullOrEmpty(ServerMap.GetName(id)))
                            {
                                ++num1;
                                continue;
                            }
                        }
                        if (num11 == 0U || uintSet.Contains(num11))
                        {
                            ++num1;
                        }
                        else
                        {
                            uintSet.Add(num11);
                            string jobName;
                            if (!JobMapping.GameToName.TryGetValue((int)num5, out jobName))
                                jobName = "직업불명";
                            partyMemberBlocks.Add(new PartyMember()
                            {
                                CharacterId = num11,
                                ServerId = id,
                                ServerName = ServerMap.GetName(id),
                                Nickname = nick,
                                JobCode = (int)num5,
                                JobName = jobName,
                                Level = (int)num7,
                                CombatPower = (int)num9
                            });
                            num1 = num3 + 12;
                        }
                    }
                }
            }
        }
        return partyMemberBlocks;
    }

    private static PartyMember? ParsePartyRequest(ReadOnlySpan<byte> packet, int dataOffset)
    {
        int length = packet.Length;
        for (int index1 = 0; index1 <= 40; ++index1)
        {
            for (int index2 = 1; index2 <= 48; ++index2)
            {
                int num1 = length - index2 - 1 - index1;
                if (num1 >= dataOffset + 12)
                {
                    if ((int)packet[num1] == index2)
                    {
                        string nick;
                        try
                        {
                            nick = Encoding.UTF8.GetString(packet.Slice(num1 + 1, index2).ToArray());
                        }
                        catch
                        {
                            continue;
                        }
                        if (IsValidNickname(nick))
                        {
                            int num2 = num1 - 12;
                            if (num2 >= dataOffset)
                            {
                                int num3 = num2;
                                uint num4 = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(num3, packet.Length - num3));
                                int num5 = num2 + 4;
                                uint num6 = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(num5, packet.Length - num5));
                                if (num6 >= 1U && num6 <= 55U)
                                {
                                    int num7 = num1 + 1 + index2 + 6;
                                    uint num8 = 0;
                                    if (num7 + 4 <= length)
                                    {
                                        int num9 = num7;
                                        num8 = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(num9, packet.Length - num9));
                                    }
                                    int id = 0;
                                    if (dataOffset + 11 < length)
                                        id = (int)packet[dataOffset + 10] | (int)packet[dataOffset + 11] << 8;
                                    string jobName;
                                    if (!JobMapping.GameToName.TryGetValue((int)num4, out jobName))
                                        jobName = "직업불명";
                                    return new PartyMember()
                                    {
                                        ServerId = id,
                                        ServerName = ServerMap.GetName(id),
                                        Nickname = nick,
                                        JobCode = (int)num4,
                                        JobName = jobName,
                                        Level = (int)num6,
                                        CombatPower = (int)num8
                                    };
                                }
                            }
                        }
                    }
                }
            }
        }
        return null;
    }

    private static PartyMember? ParsePartyAcceptMember(ReadOnlySpan<byte> packet, int dataOffset)
    {
        if (dataOffset + 25 > packet.Length)
            return null;
        if (packet[dataOffset] != (byte)26)
            return null;
        int num1 = dataOffset + 2;
        uint num2 = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(num1, packet.Length - num1));
        int id = (int)packet[dataOffset + 8] | (int)packet[dataOffset + 9] << 8;
        int num3 = (int)packet[dataOffset + 10];
        if (num3 < 1 || num3 > 48)
            return null;
        if (dataOffset + 11 + num3 + 12 > packet.Length)
            return null;
        string nick;
        try
        {
            nick = Encoding.UTF8.GetString(packet.Slice(dataOffset + 11, num3).ToArray());
        }
        catch
        {
            return null;
        }
        if (!IsValidNickname(nick))
            return null;
        int num4 = dataOffset + 11 + num3;
        int num5 = num4;
        uint num6 = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(num5, packet.Length - num5));
        int num7 = num4 + 4;
        uint num8 = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(num7, packet.Length - num7));
        int num9 = num4 + 8;
        uint num10 = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(num9, packet.Length - num9));
        if (num8 < 1U || num8 > 55U)
            return null;
        if (num10 > 9999999U)
            return null;
        string jobName2;
        if (!JobMapping.GameToName.TryGetValue((int)num6, out jobName2))
            jobName2 = "직업불명";
        return new PartyMember()
        {
            CharacterId = num2,
            ServerId = id,
            ServerName = ServerMap.GetName(id),
            Nickname = nick,
            JobCode = (int)num6,
            JobName = jobName2,
            Level = (int)num8,
            CombatPower = (int)num10
        };
    }

    private static (int value, int length) ReadVarInt(ReadOnlySpan<byte> buf)
    {
        int num1 = 0;
        int num2 = 0;
        int num3 = 0;
        while (num3 < buf.Length)
        {
            byte num4 = buf[num3];
            ++num3;
            num1 |= ((int)num4 & (int)sbyte.MaxValue) << num2;
            if (((int)num4 & 128) == 0)
                return (num1, num3);
            num2 += 7;
            if (num2 >= 32)
                return (-1, -1);
        }
        return (-1, -1);
    }

    private static bool IsValidNickname(string? nick)
    {
        return !string.IsNullOrEmpty(nick) && !Regex.IsMatch(nick, "^[0-9]+$") && Regex.IsMatch(nick, "^[가-힣a-zA-Z0-9]+$");
    }
}
