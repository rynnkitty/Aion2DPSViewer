using System;
using System.Collections.Generic;
using System.Text;

namespace Aion2DPSViewer.Packet;

public static class ProtocolUtils
{
    public static string HexDump(ReadOnlySpan<byte> data, int offset = 0, int maxBytes = 200)
    {
        int num1 = Math.Max(0, offset);
        int num2 = Math.Min(data.Length - num1, maxBytes);
        if (num2 <= 0)
            return "";
        StringBuilder stringBuilder = new StringBuilder(num2 * 3);
        for (int index = 0; index < num2; ++index)
        {
            if (index > 0)
                stringBuilder.Append(' ');
            stringBuilder.Append(data[num1 + index].ToString("X2"));
        }
        if (num1 + num2 < data.Length)
            stringBuilder.Append("...");
        return stringBuilder.ToString();
    }

    public static string HexDump(byte[] data, int offset = 0, int maxBytes = 200)
    {
        return HexDump((ReadOnlySpan<byte>)data.AsSpan(), offset, maxBytes);
    }

    public static int IndexOf(byte[] buffer, byte[] pattern)
    {
        for (int index1 = 0; index1 <= buffer.Length - pattern.Length; ++index1)
        {
            bool flag = true;
            for (int index2 = 0; index2 < pattern.Length; ++index2)
            {
                if ((int)buffer[index1 + index2] != (int)pattern[index2])
                {
                    flag = false;
                    break;
                }
            }
            if (flag)
                return index1;
        }
        return -1;
    }

    public static int IndexOf(List<byte> buffer, byte[] pattern)
    {
        for (int index1 = 0; index1 <= buffer.Count - pattern.Length; ++index1)
        {
            bool flag = true;
            for (int index2 = 0; index2 < pattern.Length; ++index2)
            {
                if ((int)buffer[index1 + index2] != (int)pattern[index2])
                {
                    flag = false;
                    break;
                }
            }
            if (flag)
                return index1;
        }
        return -1;
    }

    public static bool IsGamePacket(byte[] data)
    {
        return data.Length >= 3 && (int)data[0] - 20 <= 3 && data[1] == (byte)3 && data[2] <= (byte)4;
    }

    public static uint ReadVarint(byte[] data, ref int offset, int limit)
    {
        uint num1 = 0;
        int num2 = 0;
        while (offset < limit)
        {
            byte num3 = data[offset++];
            num1 |= (uint)(((int)num3 & (int)sbyte.MaxValue) << num2);
            if (((int)num3 & 128) == 0)
                return num1;
            num2 += 7;
            if (num2 > 31)
                break;
        }
        return uint.MaxValue;
    }

    public static string DecodeGameString(byte[] data, int offset, int maxLen)
    {
        byte[] numArray = new byte[maxLen * 4];
        int num1 = 0;
        int num2 = offset + maxLen;
        for (int index1 = offset; index1 < num2; ++index1)
        {
            byte num3 = data[index1];
            if (num3 != (byte)0)
            {
                if (num3 < (byte)32)
                {
                    int num4 = Math.Min((int)num3, num1);
                    for (int index2 = 0; index2 < num4 && num1 < numArray.Length; ++index2)
                        numArray[num1++] = numArray[index2];
                }
                else if (num1 < numArray.Length)
                    numArray[num1++] = num3;
            }
            else
                break;
        }
        string str = Encoding.UTF8.GetString(numArray, 0, num1);
        StringBuilder stringBuilder = new StringBuilder(str.Length);
        foreach (char ch in str)
        {
            if (char.IsLetterOrDigit(ch) || ch >= '가' && ch <= '힣')
                stringBuilder.Append(ch);
        }
        return stringBuilder.ToString();
    }

    public static ulong Fnv1aHash(ReadOnlySpan<byte> data)
    {
        ulong num1 = 14695981039346656037UL;
        ReadOnlySpan<byte> readOnlySpan = data;
        for (int index = 0; index < readOnlySpan.Length; ++index)
        {
            byte num2 = readOnlySpan[index];
            num1 = (num1 ^ (ulong)num2) * 1099511628211UL;
        }
        return num1;
    }

    public static bool IsAllDigits(string s)
    {
        foreach (char ch in s)
        {
            if (!char.IsDigit(ch))
                return false;
        }
        return true;
    }

    public static int FindPattern(byte[] data, int offset, int length, byte[] pattern)
    {
        int num = offset + length - pattern.Length + 1;
        for (int pattern1 = offset; pattern1 < num; ++pattern1)
        {
            bool flag = true;
            for (int index = 0; index < pattern.Length; ++index)
            {
                if ((int)data[pattern1 + index] != (int)pattern[index])
                {
                    flag = false;
                    break;
                }
            }
            if (flag)
                return pattern1;
        }
        return -1;
    }
}
