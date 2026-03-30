using System;
using System.Collections.Generic;

namespace Aion2DPSViewer.Packet;

internal sealed class TcpReassembler
{
    private readonly Action<byte[]> _callback;
    private uint _expectedSeq;
    private bool _initialized;
    private readonly SortedDictionary<uint, byte[]> _outOfOrder = new SortedDictionary<uint, byte[]>();
    private long _totalBuffered;
    private long _lastActivityTicks;
    private int _deliveredCount;
    private int _oooCount;
    private int _forceFlushCount;
    private int _gapStuckCount;
    private const long MaxBufferBytes = 4194304;
    private const long TimeoutMs = 2000;
    private const int MaxGapStuck = 50;

    public string DiagInfo
    {
        get
        {
            return $"delivered={_deliveredCount} ooo={_oooCount} flush={_forceFlushCount} buffered={_totalBuffered} pending={_outOfOrder.Count} gapStuck={_gapStuckCount}";
        }
    }

    public TcpReassembler(Action<byte[]> callback) => _callback = callback;

    public void Feed(uint seqNum, byte[] data)
    {
        if (data.Length == 0)
            return;
        if (!_initialized)
        {
            _expectedSeq = seqNum;
            _initialized = true;
        }
        long tickCount64 = Environment.TickCount64;
        if (_outOfOrder.Count > 0 && _lastActivityTicks > 0L && tickCount64 - _lastActivityTicks > 2000L)
            ForceFlush();
        int num1 = (int)seqNum - (int)_expectedSeq;
        if (num1 == 0)
        {
            _callback(data);
            _expectedSeq = seqNum + (uint)data.Length;
            _lastActivityTicks = tickCount64;
            ++_deliveredCount;
            _gapStuckCount = 0;
            DrainOrdered();
        }
        else if (num1 < 0)
        {
            int num2 = -num1;
            if (num2 >= data.Length)
                return;
            byte[] numArray = new byte[data.Length - num2];
            Buffer.BlockCopy(data, num2, numArray, 0, numArray.Length);
            _callback(numArray);
            _expectedSeq += (uint)numArray.Length;
            _lastActivityTicks = tickCount64;
            ++_deliveredCount;
            _gapStuckCount = 0;
            DrainOrdered();
        }
        else
        {
            ++_oooCount;
            ++_gapStuckCount;
            if (!_outOfOrder.ContainsKey(seqNum))
            {
                _outOfOrder[seqNum] = data;
                _totalBuffered += (long)data.Length;
            }
            if (_totalBuffered <= 4194304L && _gapStuckCount <= 50)
                return;
            ForceFlush();
        }
    }

    private void DrainOrdered()
    {
        while (_outOfOrder.Count > 0)
        {
            uint num1 = 0;
            using (SortedDictionary<uint, byte[]>.Enumerator enumerator = _outOfOrder.GetEnumerator())
            {
                if (enumerator.MoveNext())
                    num1 = enumerator.Current.Key;
            }
            int num2 = (int)num1 - (int)_expectedSeq;
            if (num2 > 0)
                break;
            byte[] numArray1;
            if (_outOfOrder.TryGetValue(num1, out numArray1))
                _outOfOrder.Remove(num1);
            if (numArray1 != null)
            {
                _totalBuffered -= (long)numArray1.Length;
                if (num2 == 0)
                {
                    _callback(numArray1);
                    _expectedSeq += (uint)numArray1.Length;
                }
                else
                {
                    int num3 = -num2;
                    if (num3 < numArray1.Length)
                    {
                        byte[] numArray2 = new byte[numArray1.Length - num3];
                        Buffer.BlockCopy(numArray1, num3, numArray2, 0, numArray2.Length);
                        _callback(numArray2);
                        _expectedSeq += (uint)numArray2.Length;
                    }
                }
            }
        }
    }

    private void ForceFlush()
    {
        ++_forceFlushCount;
        _gapStuckCount = 0;
        foreach (KeyValuePair<uint, byte[]> keyValuePair in _outOfOrder)
        {
            _callback(keyValuePair.Value);
            _expectedSeq = keyValuePair.Key + (uint)keyValuePair.Value.Length;
        }
        _outOfOrder.Clear();
        _totalBuffered = 0L;
    }

    public void Reset()
    {
        _initialized = false;
        _expectedSeq = 0U;
        _outOfOrder.Clear();
        _totalBuffered = 0L;
    }
}
