using System;
using System.Buffers;
using System.Text;

namespace Aion2DPSViewer.Packet;

internal sealed class StreamProcessor
{
    private readonly PacketProcessor _owner;
    private readonly PacketDispatcher _dispatcher;
    private byte[] _buffer;
    private int _bufferLen;
    private const int InitialCapacity = 65536;
    private const int MaxMessageLen = 2000000;
    private int _processDataCalls;
    private int _dispatchCalls;
    private int _syncRecoverCount;
    private int _lz4DecompCount;
    private int _lz4FailCount;
    private int _invalidLenCount;
    private int _incompleteCount;
    private DateTime _lastDiagLog = DateTime.MinValue;

    public StreamProcessor(PacketProcessor owner)
    {
        _owner = owner;
        _dispatcher = new PacketDispatcher(owner);
        _buffer = new byte[65536];
        _bufferLen = 0;
    }

    private void SyncDumpSetting()
    {
        _dispatcher.EnableUnparsedDump(_owner.DumpUnparsedMessages);
    }

    private void LogDiag(string msg) => _owner.Log(0, "[StreamProc] " + msg);

    private void EmitDiagPeriodic()
    {
        DateTime utcNow = DateTime.UtcNow;
        if ((utcNow - _lastDiagLog).TotalSeconds < 30.0)
            return;
        _lastDiagLog = utcNow;
        LogDiag($"stats: calls={_processDataCalls} dispatch={_dispatchCalls} lz4={_lz4DecompCount} lz4fail={_lz4FailCount} sync={_syncRecoverCount} invalid={_invalidLenCount} incomplete={_incompleteCount} bufLen={_bufferLen}");
    }

    public void ProcessData(byte[] data)
    {
        SyncDumpSetting();
        ++_processDataCalls;
        EmitDiagPeriodic();
        EnsureCapacity(_bufferLen + data.Length);
        Buffer.BlockCopy(data, 0, _buffer, _bufferLen, data.Length);
        _bufferLen += data.Length;
        int pos = 0;
        while (pos < _bufferLen)
        {
            while (pos < _bufferLen && _buffer[pos] == (byte)0)
                ++pos;
            if (pos < _bufferLen)
            {
                int num1 = pos;
                int bytesConsumed = 0;
                uint num2 = ReadVarintCounting(_buffer, ref pos, _bufferLen, out bytesConsumed);
                if (num2 == uint.MaxValue || bytesConsumed == 0)
                {
                    pos = num1;
                    ++_incompleteCount;
                    break;
                }
                int len = (int)num2 + bytesConsumed - 4;
                if (len <= 0 || len > 2000000)
                {
                    ++_invalidLenCount;
                    if (_invalidLenCount <= 5)
                        LogDiag($"invalid msgLen={len} varint=0x{num2:X} varintBytes={bytesConsumed} at bufOffset={num1} bufLen={_bufferLen} first4={HexSnippet(_buffer, num1, 8)}");
                    int syncPattern = FindSyncPattern(num1 + 1);
                    if (syncPattern >= 0)
                    {
                        ++_syncRecoverCount;
                        pos = syncPattern;
                    }
                    else
                    {
                        pos = _bufferLen;
                        break;
                    }
                }
                else
                {
                    if (num1 + len > _bufferLen)
                    {
                        pos = num1;
                        break;
                    }
                    pos = num1 + len;
                    if (len > bytesConsumed)
                        ProcessMessage(_buffer, num1, len, bytesConsumed);
                }
            }
            else
                break;
        }
        if (pos <= 0)
            return;
        _bufferLen -= pos;
        if (_bufferLen <= 0)
            return;
        Buffer.BlockCopy(_buffer, pos, _buffer, 0, _bufferLen);
    }

    private void ProcessMessage(byte[] buf, int start, int len, int varintLen)
    {
        if (len <= varintLen)
            return;
        int num1 = varintLen;
        if (num1 < len && ((int)buf[start + num1] & 240) == 240 && buf[start + num1] != byte.MaxValue)
            ++num1;
        if (len >= num1 + 2 && buf[start + num1] == byte.MaxValue && buf[start + num1 + 1] == byte.MaxValue)
        {
            int num2 = start + num1 + 2;
            int num3 = start + len;
            if (num2 + 4 > num3)
                return;
            int int32 = BitConverter.ToInt32(buf, num2);
            int srcOffset = num2 + 4;
            if (int32 <= 0 || int32 > 2000000)
                return;
            byte[] numArray = ArrayPool<byte>.Shared.Rent(int32);
            try
            {
                int srcLen = num3 - srcOffset;
                int length = Lz4Decoder.Decompress(buf, srcOffset, srcLen, numArray, 0, int32);
                if (length > 0)
                {
                    ++_lz4DecompCount;
                    ProcessDecompressedBlock(numArray, 0, length);
                }
                else
                {
                    ++_lz4FailCount;
                    if (_lz4FailCount > 5)
                        return;
                    LogDiag($"lz4 fail: compLen={srcLen} decompSize={int32} result={length}");
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(numArray, false);
            }
        }
        else
        {
            ++_dispatchCalls;
            _dispatcher.Dispatch(buf, start, len);
        }
    }

    private void ProcessDecompressedBlock(byte[] data, int offset, int length)
    {
        int pos = offset;
        int num1;
        int len;
        for (int end = offset + length; pos < end; pos = num1 + len)
        {
            while (pos < end && data[pos] == (byte)0)
                ++pos;
            if (pos >= end)
                break;
            num1 = pos;
            int bytesConsumed = 0;
            uint num2 = ReadVarintCounting(data, ref pos, end, out bytesConsumed);
            if (num2 == uint.MaxValue || bytesConsumed == 0)
            {
                if (end - num1 <= 0)
                    break;
                _dispatcher.Dispatch(data, num1, end - num1);
                break;
            }
            len = (int)num2 + bytesConsumed - 4;
            if (len <= 0 || num1 + len > end)
            {
                if (end - num1 <= 0)
                    break;
                _dispatcher.Dispatch(data, num1, end - num1);
                break;
            }
            ++_dispatchCalls;
            ProcessSubMessage(data, num1, len, bytesConsumed);
        }
    }

    private void ProcessSubMessage(byte[] data, int start, int len, int varintLen)
    {
        int index = start + varintLen;
        if (index < start + len && ((int)data[index] & 240) == 240 && data[index] != byte.MaxValue)
            ++index;
        if (index + 2 <= start + len && data[index] == byte.MaxValue && data[index + 1] == byte.MaxValue)
        {
            int num = index + 2;
            if (num + 4 > start + len)
                return;
            int int32 = BitConverter.ToInt32(data, num);
            int srcOffset = num + 4;
            if (int32 <= 0 || int32 > 2000000)
                return;
            byte[] numArray = ArrayPool<byte>.Shared.Rent(int32);
            try
            {
                int srcLen = start + len - srcOffset;
                int length = Lz4Decoder.Decompress(data, srcOffset, srcLen, numArray, 0, int32);
                if (length <= 0)
                    return;
                if (length < numArray.Length)
                    numArray[length] = (byte)0;
                ProcessDecompressedBlock(numArray, 0, length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(numArray, false);
            }
        }
        else
            _dispatcher.Dispatch(data, start, len);
    }

    private static uint ReadVarintCounting(byte[] data, ref int pos, int end, out int bytesConsumed)
    {
        uint num1 = 0;
        int num2 = 0;
        int num3 = pos;
        while (pos < end)
        {
            byte num4 = data[pos++];
            num1 |= (uint)(((int)num4 & (int)sbyte.MaxValue) << num2);
            if (((int)num4 & 128) == 0)
            {
                bytesConsumed = pos - num3;
                return num1;
            }
            num2 += 7;
            if (num2 > 28)
            {
                bytesConsumed = 0;
                return uint.MaxValue;
            }
        }
        bytesConsumed = 0;
        return uint.MaxValue;
    }

    private int FindSyncPattern(int startOffset)
    {
        for (int syncPattern = startOffset; syncPattern + 2 < _bufferLen; ++syncPattern)
        {
            if (_buffer[syncPattern] == (byte)6 && _buffer[syncPattern + 1] == (byte)0 && _buffer[syncPattern + 2] == (byte)54)
                return syncPattern;
        }
        return -1;
    }

    private static string HexSnippet(byte[] data, int offset, int maxBytes)
    {
        int num = Math.Min(maxBytes, data.Length - offset);
        if (num <= 0)
            return "";
        StringBuilder stringBuilder = new StringBuilder(num * 3);
        for (int index = 0; index < num; ++index)
        {
            if (index > 0)
                stringBuilder.Append(' ');
            stringBuilder.Append(data[offset + index].ToString("X2"));
        }
        return stringBuilder.ToString();
    }

    private void EnsureCapacity(int needed)
    {
        if (_buffer.Length >= needed)
            return;
        byte[] numArray = new byte[Math.Max(_buffer.Length * 2, needed)];
        Buffer.BlockCopy(_buffer, 0, numArray, 0, _bufferLen);
        _buffer = numArray;
    }
}
