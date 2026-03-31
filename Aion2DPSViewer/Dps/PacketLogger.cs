using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Aion2DPSViewer.Dps;

internal class PacketLogger : IDisposable
{
    private static readonly string LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aion2Info", "packet_logs");
    private StreamWriter? _writer;
    private long _bytesWritten;
    private long _feedSeq;
    private long _lastCallbackFeedSeq;
    private readonly (long seq, int srcPort, int dstPort, byte[] data)[] _recentFeeds = new (long, int, int, byte[])[8];
    private int _recentFeedIdx;

    public static string Directory => LogDir;

    public void Init()
    {
        try
        {
            Cleanup();
            System.IO.Directory.CreateDirectory(LogDir);
            string str = Path.Combine(LogDir, $"packets_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            _writer = new StreamWriter(str, false, Encoding.UTF8, 65536);
            _writer.WriteLine($"# PacketProcessor 패킷 분석 로그 v2 — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _writer.WriteLine("# FEED: 입력 패킷 (시퀀스, 포트, hex)");
            _writer.WriteLine("# CB: 콜백 출력 (타입, 데이터, 참조 FEED 시퀀스 범위)");
            _writer.WriteLine();
            Console.Error.WriteLine("[dps-debug] 패킷 로그: " + str);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[dps-debug] 로그 초기화 실패: " + ex.Message);
        }
    }

    public void Log(string msg)
    {
        try
        {
            StreamWriter? writer = _writer;
            if (writer == null || _bytesWritten > 31457280L)
                return;
            writer.WriteLine(msg);
            _bytesWritten += (long)(msg.Length + 2);
        }
        catch { }
    }

    public void IncrementFeedSeq() => Interlocked.Increment(ref _feedSeq);

    public long ReadFeedSeq() => Interlocked.Read(ref _feedSeq);

    public string FeedRef()
    {
        long num = Interlocked.Read(ref _feedSeq);
        long lastCallbackFeedSeq = _lastCallbackFeedSeq;
        _lastCallbackFeedSeq = num;
        if (lastCallbackFeedSeq != num)
            return $"[feed={lastCallbackFeedSeq + 1L}~{num}]";
        return $"[feed={num}]";
    }

    public void RecordFeed(long seq, int srcPort, int dstPort, byte[] data)
    {
        _recentFeeds[_recentFeedIdx % _recentFeeds.Length] = (seq, srcPort, dstPort, data);
        ++_recentFeedIdx;
    }

    public void Flush()
    {
        try { _writer?.Flush(); } catch { }
    }

    public void Dispose()
    {
        try { _writer?.Flush(); } catch { }
        try { _writer?.Dispose(); } catch { }
        _writer = null;
    }

    private static void Cleanup()
    {
        try
        {
            if (!System.IO.Directory.Exists(LogDir))
                return;
            foreach (string file in System.IO.Directory.GetFiles(LogDir, "packets_*.log"))
            {
                try { File.Delete(file); } catch { }
            }
        }
        catch { }
    }
}
