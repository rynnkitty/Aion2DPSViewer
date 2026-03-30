using Aion2DPSViewer.Dps;
using System;
using System.Collections.Generic;
using System.Text;

namespace Aion2DPSViewer.Packet;

internal sealed class PacketDispatcher
{
    private readonly PacketProcessor _owner;
    private bool _dumpUnparsed;
    private const byte TAG_DAMAGE_1 = 4;
    private const byte TAG_DAMAGE_2 = 56;
    private const byte TAG_DOT_1 = 5;
    private const byte TAG_DOT_2 = 56;
    private const byte TAG_BATTLE_STATS_1 = 42;
    private const byte TAG_BATTLE_STATS_2 = 56;
    private const byte TAG_BATTLE_STATS_ALT_1 = 43;
    private const byte TAG_SELF_INFO_1 = 51;
    private const byte TAG_SELF_INFO_2 = 54;
    private const byte TAG_OTHER_INFO_1 = 68;
    private const byte TAG_OTHER_INFO_2 = 54;
    private const byte TAG_MOB_SPAWN_1 = 64;
    private const byte TAG_MOB_SPAWN_2 = 54;
    private const byte TAG_GUARD_1 = 3;
    private const byte TAG_GUARD_2 = 54;
    private const byte TAG_ENTITY_REMOVED_1 = 33;
    private const byte TAG_ENTITY_REMOVED_2 = 141;
    private const byte TAG_ALT_MOBSPAWN_1 = 0;
    private const byte TAG_ALT_MOBSPAWN_2 = 141;
    private const byte HEAL_MARKER = 3;
    private const byte MULTI_HIT_MARKER = 1;
    private const byte NAME_BLOCK_MARKER = 7;
    private const uint SENTINEL_SKILL_CODE = 12250030;
    private const int MIN_PACKET_LENGTH = 4;
    private const int IGNORED_PACKET_LENGTH = 11;
    private const int MOB_CODE_SEARCH_RANGE = 60;
    private const int MOB_HP_SEARCH_RANGE = 67;
    private const int MAX_NAME_LENGTH = 72;
    private const int NAME_SCAN_WINDOW = 10;
    private const byte MOB_FLAG_MASK = 191;
    private const uint VARINT_ERROR = 4294967295;
    private static readonly int[] CategoryTrailingSize = new int[8]
    {
        0, 0, 0, 0, 8, 12, 10, 14
    };
    private static readonly byte[] SummonBoundaryMarker = new byte[8]
    {
        byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue,
        byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue
    };
    private static readonly byte[] SummonActorHeader = new byte[3]
    {
        (byte)7, (byte)2, (byte)6
    };
    private static readonly byte[] CP_PACKET_MARKER = new byte[2]
    {
        (byte)0, (byte)146
    };

    public PacketDispatcher(PacketProcessor owner) => _owner = owner;

    public void EnableUnparsedDump(bool enabled) => _dumpUnparsed = enabled;

    public void Dispatch(byte[] data, int offset, int length)
    {
        if (length < 4 || length == 11)
            return;
        int offset1 = offset;
        int limit = offset + length;
        int num1 = (int)ProtocolUtils.ReadVarint(data, ref offset1, limit);
        int num2 = offset1 - offset;
        if (num2 <= 0)
            num2 = 1;
        if (num2 < length - 1 && data[offset + num2] == (byte)3 && data[offset + num2 + 1] == (byte)54)
            return;
        bool flag1 = false | TryParseUserInfo(data, offset, length) | TryParseEntityRemoved(data, offset, length);
        if (num2 > 0 && num2 + 1 < length && data[offset + num2] == (byte)64 && data[offset + num2 + 1] == (byte)54)
            flag1 = flag1 | TryParseMobInfo(data, offset, length, num2 + 2) | TryParseSummon(data, offset, length, num2 + 2);
        bool flag2 = flag1 | HasEntityMarker(data, offset, length) | TryParseDamage(data, offset, length) | TryParseDot(data, offset, length) | TryParseBossHp(data, offset, length) | TryParseBattleStats(data, offset, length);
        if (!_dumpUnparsed || flag2 || length <= 4)
            return;
        _owner.Log(0, $"[UNPARSE] tag=0x{(num2 < length ? data[offset + num2] : (byte)0):X2}{(num2 + 1 < length ? data[offset + num2 + 1] : (byte)0):X2} len={length} hex={HexDump(data, offset, Math.Min(length, 80))}");
    }

    private bool TryParseDamage(byte[] data, int offset, int length)
    {
        int num1 = offset + length;
        int pos = LocateDamageHeader(data, offset, num1);
        if (pos < 0)
            return false;
        uint targetId = ProtocolUtils.ReadVarint(data, ref pos, num1);
        if (targetId == uint.MaxValue || pos >= num1)
            return false;
        uint num2 = ProtocolUtils.ReadVarint(data, ref pos, num1);
        if (num2 == uint.MaxValue)
            return false;
        uint index = num2 & 15U;
        if (index < 4U || index > 7U)
            return true;
        SkipVarint(data, ref pos, num1);
        if (pos >= num1)
            return false;
        uint actorId = ProtocolUtils.ReadVarint(data, ref pos, num1);
        if (actorId == uint.MaxValue)
            return false;
        if ((int)actorId == (int)targetId)
            return true;
        if (num1 - pos < 5)
            return false;
        SkillDatabase? skillDb = _owner.SkillDb;
        if (skillDb == null)
            return false;
        int skillCode = skillDb.ResolveFromPacketBytes(data, ref pos, num1);
        if (skillCode == 0)
            return false;
        uint damageType = ProtocolUtils.ReadVarint(data, ref pos, num1);
        if (damageType == uint.MaxValue)
            return false;
        int num3 = CategoryTrailingSize[(int)index];
        byte flagByte = 0;
        int num4 = 0;
        if (num1 - pos > 1 && (pos + 2 >= num1 || data[pos + 1] == (byte)0))
        {
            flagByte = data[pos];
            pos += 2;
            num4 = 1;
        }
        uint damageFlags = ComputeDamageFlags(flagByte, (byte)damageType);
        int num5 = num4 * 2;
        int num6 = num3 - num5;
        if (num6 > 0 && pos + num6 <= num1)
            pos += num6;
        if (num1 - pos < 1)
            return false;
        SkipVarint(data, ref pos, num1);
        uint num7 = ProtocolUtils.ReadVarint(data, ref pos, num1);
        if (num7 == uint.MaxValue)
            num7 = 0U;
        int multiHitCount = 0;
        int multiHitDamage = 0;
        if (num1 - pos >= 2)
        {
            (int count, int damage, int endPos) = TryParseMultiHit(data, pos, num1, num7);
            if (count > 0 && damage > 0)
            {
                multiHitCount = count;
                multiHitDamage = damage;
                int num8 = (int)num7 - damage;
                num7 = num8 > 0 ? (uint)num8 : 0U;
                pos = endPos;
            }
        }
        uint healAmount = 0;
        if (num1 - pos > 1 && data[pos] == (byte)3 && (pos + 1 >= num1 || data[pos + 1] == (byte)0))
        {
            int offset1 = pos + 2;
            if (offset1 < num1)
            {
                healAmount = ProtocolUtils.ReadVarint(data, ref offset1, num1);
                if (healAmount == uint.MaxValue)
                    healAmount = 0U;
            }
        }
        _owner.FireDamage((int)actorId, (int)targetId, skillCode, (byte)(damageType & (uint)byte.MaxValue), (int)num7, damageFlags, multiHitCount, multiHitDamage, (int)healAmount, 0);
        return true;
    }

    private bool TryParseDot(byte[] data, int offset, int length)
    {
        int num1 = offset + length;
        int num2 = offset;
        if (!SkipVarintAndCheckTag(data, ref num2, num1, (byte)5, (byte)56))
            return false;
        uint targetId = ProtocolUtils.ReadVarint(data, ref num2, num1);
        if (targetId == uint.MaxValue || num2 >= num1)
            return false;
        byte[] numArray = data;
        int index = num2;
        int offset1 = index + 1;
        byte num3 = numArray[index];
        uint actorId = ProtocolUtils.ReadVarint(data, ref offset1, num1);
        if (actorId == uint.MaxValue || offset1 >= num1)
            return false;
        uint healAmount = ProtocolUtils.ReadVarint(data, ref offset1, num1);
        if (healAmount == uint.MaxValue || offset1 + 4 > num1)
            return false;
        uint rawMobCode = (uint)((int)data[offset1] | (int)data[offset1 + 1] << 8 | (int)data[offset1 + 2] << 16 | (int)data[offset1 + 3] << 24);
        int offset2 = offset1 + 4;
        uint skillCode = DeriveSkillCodeFromMobCode(rawMobCode);
        uint damage = ProtocolUtils.ReadVarint(data, ref offset2, num1);
        if (damage == uint.MaxValue)
            return false;
        if (!IsRecoveryMob(skillCode))
        {
            if (num3 == (byte)3 || skillCode == 12250030U)
                damage = 0U;
            if ((int)actorId == (int)targetId)
                return true;
        }
        _owner.FireDamage((int)actorId, (int)targetId, (int)skillCode, (byte)0, (int)damage, 0U, 0, 0, (int)healAmount, num3 == (byte)2 ? 1 : 0);
        return true;
    }

    private uint DeriveSkillCodeFromMobCode(uint rawMobCode)
    {
        uint num = rawMobCode / 1000U;
        PacketProcessor.IsMobBossFunc? isMobBoss = _owner.IsMobBoss;
        return isMobBoss == null || !isMobBoss((int)num) || !SkillDatabase.IsSkillCodeInRange((int)num) ? rawMobCode / 100U : num;
    }

    private bool IsRecoveryMob(uint skillCode)
    {
        PacketProcessor.GetSkillNameFunc? getSkillName = _owner.GetSkillName;
        if (getSkillName == null)
            return false;
        string? str = getSkillName((int)skillCode);
        if (string.IsNullOrEmpty(str))
            return false;
        string lowerInvariant = str.ToLowerInvariant();
        return lowerInvariant.Contains("recuperation") || lowerInvariant.Contains("recovery") || lowerInvariant.Contains("restoration");
    }

    private bool TryParseMobInfo(byte[] data, int offset, int length, int relativeStart)
    {
        int num1 = offset + length;
        int offset1 = offset + relativeStart;
        if (offset1 >= num1)
            return false;
        uint mobId = ProtocolUtils.ReadVarint(data, ref offset1, num1);
        if (mobId == uint.MaxValue)
            return false;
        int from = offset1 - offset;
        int limit = Math.Min(from + 60, length - 2);
        int num2 = ScanMobCodeMarker(data, offset, from, limit, num1);
        if (num2 < 0)
            return false;
        int num3 = num2 - 2;
        if (from > num3 - 3)
            return false;
        int index1 = offset + num3 - 3;
        if (index1 < offset || index1 + 3 > num1)
            return false;
        uint mobCode = (uint)((int)data[index1] | (int)data[index1 + 1] << 8 | (int)data[index1 + 2] << 16);
        PacketProcessor.IsMobBossFunc? isMobBoss = _owner.IsMobBoss;
        int isBoss = isMobBoss != null && isMobBoss((int)mobCode) ? 1 : 0;
        _owner.FireMobSpawn((int)mobId, (int)mobCode, 0, isBoss);
        int num4 = num3 + 3;
        int num5 = Math.Min(num3 + 67, length - 2);
        for (int index2 = num4; index2 < num5; ++index2)
        {
            int index3 = offset + index2;
            if (index3 < num1)
            {
                if (data[index3] == (byte)1)
                {
                    int offset2 = index3 + 1;
                    if (offset2 < num1)
                    {
                        uint num6 = ProtocolUtils.ReadVarint(data, ref offset2, num1);
                        switch (num6)
                        {
                            case 0:
                            case uint.MaxValue:
                                continue;
                            default:
                                if (offset2 < num1)
                                {
                                    uint hp = ProtocolUtils.ReadVarint(data, ref offset2, num1);
                                    if (hp != uint.MaxValue)
                                    {
                                        if ((int)hp >= (int)num6)
                                            _owner.FireMobSpawn((int)mobId, (int)mobCode, (int)hp, isBoss);
                                        return true;
                                    }
                                    continue;
                                }
                                continue;
                        }
                    }
                    else
                        break;
                }
            }
            else
                break;
        }
        return true;
    }

    private static int ScanMobCodeMarker(byte[] data, int offset, int from, int limit, int end)
    {
        for (int index1 = from; index1 < limit; ++index1)
        {
            int index2 = offset + index1 + 2;
            if (index2 < end && index2 >= offset + 2 && data[index2 - 2] == (byte)0 && ((int)data[index2 - 1] & 191) == 0 && data[index2] == (byte)2)
                return index1 + 2;
        }
        return -1;
    }

    private bool TryParseSummon(byte[] data, int offset, int length, int relativeStart)
    {
        int limit = offset + length;
        int offset1 = offset + relativeStart;
        if (offset1 >= limit)
            return false;
        uint petId = ProtocolUtils.ReadVarint(data, ref offset1, limit);
        if (petId == uint.MaxValue)
            return false;
        ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(data, offset, length);
        int num1 = span.IndexOf((ReadOnlySpan<byte>)SummonBoundaryMarker);
        if (num1 == -1)
            return false;
        int num2 = num1 + 8;
        if (num2 >= length)
            return false;
        int num3 = span.Slice(num2).IndexOf((ReadOnlySpan<byte>)SummonActorHeader);
        if (num3 == -1)
            return false;
        int num4 = num2 + num3;
        if (num4 + 5 > length)
            return false;
        ushort actorId = (ushort)((uint)data[offset + num4 + 3] | (uint)data[offset + num4 + 4] << 8);
        if (actorId <= (ushort)99)
            return false;
        _owner.FireSummon((int)actorId, (int)petId);
        return true;
    }

    private bool TryParseBattleStats(byte[] data, int offset, int length)
    {
        int num1 = offset + length;
        int num2 = IndexOfTag(data, offset, num1, (byte)42, (byte)56);
        if (num2 < 0)
            num2 = IndexOfTag(data, offset, num1, (byte)43, (byte)56);
        if (num2 < 0)
            return false;
        int offset1 = num2 + 2;
        if (offset1 + 26 > num1)
            return true;
        uint entityId = ProtocolUtils.ReadVarint(data, ref offset1, num1);
        if (entityId == uint.MaxValue || offset1 >= num1 || offset1 + 2 > num1)
            return true;
        int num3 = offset1 + 1;
        byte[] numArray = data;
        int index = num3;
        int offset2 = index + 1;
        byte type = numArray[index];
        if (ProtocolUtils.ReadVarint(data, ref offset2, num1) == uint.MaxValue || offset2 >= num1 || offset2 + 4 > num1)
            return true;
        uint buffId = (uint)((int)data[offset2] | (int)data[offset2 + 1] << 8 | (int)data[offset2 + 2] << 16 | (int)data[offset2 + 3] << 24);
        offset2 += 4;
        if (offset2 + 4 > num1)
            return true;
        uint durationMs = (uint)((int)data[offset2] | (int)data[offset2 + 1] << 8 | (int)data[offset2 + 2] << 16 | (int)data[offset2 + 3] << 24);
        offset2 += 4;
        if (offset2 + 4 > num1)
            return true;
        offset2 += 4;
        if (offset2 + 8 > num1)
            return true;
        long int64 = BitConverter.ToInt64(data, offset2);
        offset2 += 8;
        int casterId = 0;
        if (offset2 < num1)
        {
            uint num4 = ProtocolUtils.ReadVarint(data, ref offset2, num1);
            if (num4 != uint.MaxValue)
                casterId = (int)num4;
        }
        _owner.FireBuff((int)entityId, (int)buffId, (int)type, durationMs, int64, casterId);
        return true;
    }

    private bool TryScanCombatPower(byte[] data, int offset, int length)
    {
        int num1 = offset + length;
        if (new ReadOnlySpan<byte>(data, offset, length).IndexOf((ReadOnlySpan<byte>)CP_PACKET_MARKER) < 0)
            return false;
        for (int index1 = num1 - 3; index1 >= offset; --index1)
        {
            if (data[index1] == (byte)6 && data[index1 + 1] == (byte)0 && data[index1 + 2] == (byte)54)
            {
                int to = index1;
                if (to - 21 >= offset)
                {
                    bool flag = true;
                    for (int index2 = to - 5; index2 < to; ++index2)
                    {
                        if (data[index2] != (byte)0)
                        {
                            flag = false;
                            break;
                        }
                    }
                    if (flag)
                    {
                        int combatPower = (int)data[to - 9] | (int)data[to - 8] << 8 | (int)data[to - 7] << 16 | (int)data[to - 6] << 24;
                        if (combatPower >= 10000 && combatPower <= 999999)
                        {
                            int num2 = (int)data[to - 13] | (int)data[to - 12] << 8 | (int)data[to - 11] << 16 | (int)data[to - 10] << 24;
                            if (num2 >= 1000 && num2 <= 5000 && ((int)data[to - 17] | (int)data[to - 16] << 8 | (int)data[to - 15] << 16 | (int)data[to - 14] << 24) == 0)
                            {
                                int num3 = (int)data[to - 21] | (int)data[to - 20] << 8 | (int)data[to - 19] << 16 | (int)data[to - 18] << 24;
                                if (num3 >= 1 && num3 <= 55)
                                {
                                    (string Nick, int ServerId)? serverInCpPacket = FindNickAndServerInCpPacket(data, offset, to);
                                    if (serverInCpPacket.HasValue)
                                    {
                                        _owner.FireCombatPowerByName(serverInCpPacket.Value.Nick, serverInCpPacket.Value.ServerId, combatPower);
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                    break;
            }
        }
        return false;
    }

    private static (string Nick, int ServerId)? FindNickAndServerInCpPacket(
        byte[] data,
        int from,
        int to)
    {
        for (int index = from; index + 3 < to; ++index)
        {
            int num1 = (int)data[index] | (int)data[index + 1] << 8;
            if (num1 >= 1001 && num1 <= 2021)
            {
                int num2 = (int)data[index + 2];
                if (num2 >= 3 && num2 <= 48)
                {
                    if (index + 3 + num2 <= to)
                    {
                        try
                        {
                            string str = Encoding.UTF8.GetString(data, index + 3, num2);
                            if (!string.IsNullOrEmpty(str))
                            {
                                bool flag = true;
                                foreach (char ch in str)
                                {
                                    if ((ch < '가' || ch > '힣') && (ch < 'a' || ch > 'z') && (ch < 'A' || ch > 'Z') && (ch < '0' || ch > '9'))
                                    {
                                        flag = false;
                                        break;
                                    }
                                }
                                if (flag && str.Length >= 2)
                                    return (str, num1);
                            }
                        }
                        catch { }
                    }
                }
            }
        }
        return null;
    }

    private bool TryParseBossHp(byte[] data, int offset, int length)
    {
        int limit = offset + length;
        for (int index = offset; index < limit - 10; ++index)
        {
            if (data[index] == (byte)141)
            {
                int offset1 = index + 1;
                uint entityId = ProtocolUtils.ReadVarint(data, ref offset1, limit);
                switch (entityId)
                {
                    case 0:
                    case uint.MaxValue:
                        continue;
                    default:
                        if (offset1 + 7 <= limit && data[offset1] == (byte)2 && data[offset1 + 1] == (byte)1 && data[offset1 + 2] == (byte)0)
                        {
                            offset1 += 3;
                            int currentHp = (int)data[offset1] | (int)data[offset1 + 1] << 8 | (int)data[offset1 + 2] << 16 | (int)data[offset1 + 3] << 24;
                            offset1 += 4;
                            if (offset1 + 4 <= limit && ((int)data[offset1] | (int)data[offset1 + 1] << 8 | (int)data[offset1 + 2] << 16 | (int)data[offset1 + 3] << 24) == 0 && currentHp > 0)
                            {
                                _owner.FireBossHp((int)entityId, currentHp);
                                return true;
                            }
                            continue;
                        }
                        continue;
                }
            }
        }
        return false;
    }

    private bool TryParseEntityRemoved(byte[] data, int offset, int length)
    {
        int limit = offset + length;
        int num1 = offset;
        int num2 = 0;
        int num3 = 0;
        while (num1 < limit)
        {
            int num4 = (int)data[num1++];
            ++num2;
            if ((num4 & 128) != 0)
            {
                num3 += 7;
                if (num3 > 31 || num1 >= limit)
                    return false;
            }
            else
                break;
        }
        int index = offset + num2;
        if (index + 1 >= limit || data[index] != (byte)33 || data[index + 1] != (byte)141)
            return false;
        int offset1 = index + 2;
        if (offset1 >= limit)
            return false;
        uint entityId = ProtocolUtils.ReadVarint(data, ref offset1, limit);
        if (entityId == uint.MaxValue)
            return false;
        _owner.FireEntityRemoved((int)entityId);
        return true;
    }

    private static bool HasEntityMarker(byte[] data, int offset, int length)
    {
        int num1 = offset + length;
        if (length <= 0)
            return false;
        int index = offset;
        int num2 = 0;
        int num3 = 0;
        while (index < num1)
        {
            int num4 = (int)data[index];
            int num5 = num2 + 1;
            if ((num4 & 128) == 0)
                return num5 > 0 && offset + num2 + 3 < num1 && offset + num5 < num1 - 1 && data[offset + num5] == (byte)0 && data[offset + num5 + 1] == (byte)141;
            num3 += 7;
            if (num3 > 31 || num5 >= length)
                return false;
            ++index;
            num2 = num5;
        }
        return false;
    }

    private bool TryParseUserInfo(byte[] data, int offset, int length)
    {
        int num1 = offset + length;
        int num2 = IndexOfTag(data, offset, num1, (byte)51, (byte)54);
        if (num2 >= 0)
        {
            int offset1 = num2 + 2;
            if (offset1 < num1)
            {
                uint entityId = ProtocolUtils.ReadVarint(data, ref offset1, num1);
                if (entityId != uint.MaxValue)
                {
                    (string Name, int AfterName)? nullable = ReadPlayerName(data, offset1, num1);
                    if (nullable.HasValue)
                    {
                        int afterName = nullable.Value.AfterName;
                        int serverId = afterName + 2 <= num1 ? (int)data[afterName] | (int)data[afterName + 1] << 8 : -1;
                        int jobCode = afterName + 3 <= num1 ? (int)data[afterName + 2] : -1;
                        string nickname = AppendServerName(nullable.Value.Name, serverId);
                        _owner.FireUserInfo((int)entityId, nickname, serverId, jobCode, 1);
                        return true;
                    }
                }
            }
        }
        int num3 = IndexOfTag(data, offset, num1, (byte)68, (byte)54);
        if (num3 < 0)
            return false;
        int offset2 = num3 + 2;
        if (offset2 >= num1)
            return false;
        uint entityId1 = ProtocolUtils.ReadVarint(data, ref offset2, num1);
        if (entityId1 == uint.MaxValue)
            return false;
        if (offset2 < num1)
        {
            int num4 = (int)ProtocolUtils.ReadVarint(data, ref offset2, num1);
        }
        if (offset2 < num1)
        {
            int num5 = (int)ProtocolUtils.ReadVarint(data, ref offset2, num1);
        }
        (string Name, int AfterName)? nullable1 = ReadPlayerName(data, offset2, num1);
        if (!nullable1.HasValue)
            return false;
        int afterName1 = nullable1.Value.AfterName;
        int jobCode1 = -1;
        uint num6 = ProtocolUtils.ReadVarint(data, ref afterName1, num1);
        if (num6 != uint.MaxValue)
            jobCode1 = (int)num6;
        int serverId1 = FindServerId(data, afterName1, num1, _owner.ValidServerIds);
        string nickname1 = AppendServerName(nullable1.Value.Name, serverId1);
        _owner.FireUserInfo((int)entityId1, nickname1, serverId1, jobCode1, 0);
        return true;
    }

    private static (string Name, int AfterName)? ReadPlayerName(byte[] data, int from, int end)
    {
        int num = Math.Min(from + 10, end);
        for (int index = from; index < num; ++index)
        {
            if (data[index] == (byte)7)
            {
                int offset = index + 1;
                uint maxLen = ProtocolUtils.ReadVarint(data, ref offset, end);
                if (maxLen == uint.MaxValue || maxLen < 1U || maxLen > 72U || offset + (int)maxLen > end)
                    return null;
                string s = ProtocolUtils.DecodeGameString(data, offset, (int)maxLen);
                return string.IsNullOrEmpty(s) || ProtocolUtils.IsAllDigits(s) ? null : ((s, offset + (int)maxLen));
            }
        }
        return null;
    }

    private static int FindServerId(byte[] data, int from, int end, HashSet<int>? validIds)
    {
        int num = end - 1;
        for (int index = from; index < num; ++index)
        {
            int serverId = (int)data[index] | (int)data[index + 1] << 8;
            if (validIds != null)
            {
                if (validIds.Contains(serverId))
                    return serverId;
            }
            else if (serverId >= 1001 && serverId <= 2999 && index + 3 <= end)
            {
                byte maxLen = data[index + 2];
                if (maxLen >= (byte)2 && maxLen <= (byte)24 && index + 3 + (int)maxLen <= end)
                {
                    string s = ProtocolUtils.DecodeGameString(data, index + 3, (int)maxLen);
                    if (!string.IsNullOrEmpty(s) && !ProtocolUtils.IsAllDigits(s))
                        return serverId;
                }
            }
        }
        return -1;
    }

    private string AppendServerName(string name, int serverId)
    {
        if (serverId <= 0)
            return name;
        Func<int, string?>? getServerName = _owner.GetServerName;
        string? str = getServerName != null ? getServerName(serverId) : null;
        return !string.IsNullOrEmpty(str) ? $"{name}[{str}]" : name;
    }

    private static bool SkipVarintAndCheckTag(
        byte[] data,
        ref int pos,
        int end,
        byte tag1,
        byte tag2)
    {
        int num = 0;
        while (pos < end)
        {
            if (((int)data[pos++] & 128) == 0)
            {
                if (pos + 2 > end || (int)data[pos] != (int)tag1 || (int)data[pos + 1] != (int)tag2)
                    return false;
                pos += 2;
                return true;
            }
            num += 7;
            if (num >= 32)
                return false;
        }
        return false;
    }

    private static (int count, int damage, int endPos) TryParseMultiHit(
        byte[] data,
        int pos,
        int end,
        uint mainDamage)
    {
        (int count, int damage, int endPos) multiHitAt1 = ParseMultiHitAt(data, pos, end, mainDamage);
        if (multiHitAt1.count > 0 && multiHitAt1.damage > 0)
            return multiHitAt1;
        if (pos + 1 < end && data[pos] == (byte)1)
        {
            (int count, int damage, int endPos) multiHitAt2 = ParseMultiHitAt(data, pos + 1, end, mainDamage);
            if (multiHitAt2.count > 0 && multiHitAt2.damage > 0)
                return multiHitAt2;
        }
        return (0, 0, pos);
    }

    private static (int count, int damage, int endPos) ParseMultiHitAt(
        byte[] data,
        int pos,
        int end,
        uint mainDamage)
    {
        int num1 = pos;
        uint num2 = ProtocolUtils.ReadVarint(data, ref pos, end);
        if (num2 == 0U || num2 == uint.MaxValue || num2 >= 100U)
            return (0, 0, num1);
        int num3 = 0;
        uint num4 = 0;
        for (uint index = 0; index < num2 && pos < end; ++index)
        {
            uint num5 = ProtocolUtils.ReadVarint(data, ref pos, end);
            if (num5 != uint.MaxValue)
            {
                num3 += (int)num5;
                ++num4;
            }
            else
                break;
        }
        if ((int)num4 != (int)num2 || num3 <= 0)
            return (0, 0, num1);
        return mainDamage > 0U && (double)num3 < (double)(int)mainDamage * 0.005 ? (0, 0, num1) : ((int)num4, num3, pos);
    }

    private static int LocateDamageHeader(byte[] data, int start, int end)
    {
        int index1 = start;
        int num1 = 0;
        int num2 = 0;
        while (index1 < end)
        {
            int index2 = index1++;
            ++num2;
            if (((int)data[index2] & 128) == 0)
            {
                if (num2 > 0 && end - index1 > 1 && data[index1] == (byte)4 && data[index1 + 1] == (byte)56)
                    return index1 + 2;
                break;
            }
            num1 += 7;
            if (num1 > 31)
                break;
        }
        return -1;
    }

    private static uint ComputeDamageFlags(byte flagByte, byte damageType)
    {
        uint damageFlags = (uint)flagByte & (uint)sbyte.MaxValue;
        if (((int)flagByte & 128) != 0)
            damageFlags |= 128U;
        if (damageType == (byte)3)
            damageFlags |= 256U;
        return damageFlags;
    }

    private static void SkipVarint(byte[] data, ref int pos, int end)
    {
        while (pos < end && ((int)data[pos++] & 128) != 0) ;
    }

    private static int IndexOfTag(byte[] data, int start, int end, byte b1, byte b2)
    {
        for (int index = start; index + 1 < end; ++index)
        {
            if ((int)data[index] == (int)b1 && (int)data[index + 1] == (int)b2)
                return index;
        }
        return -1;
    }

    private static string HexDump(byte[] data, int offset, int length)
    {
        StringBuilder stringBuilder = new StringBuilder(length * 3);
        for (int index = 0; index < length && offset + index < data.Length; ++index)
        {
            if (index > 0)
                stringBuilder.Append(' ');
            stringBuilder.Append(data[offset + index].ToString("X2"));
        }
        return stringBuilder.ToString();
    }
}
