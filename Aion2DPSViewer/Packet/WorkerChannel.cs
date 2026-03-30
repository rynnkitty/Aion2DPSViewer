using System.Collections.Concurrent;
using System.Threading;

namespace Aion2DPSViewer.Packet;

internal sealed class WorkerChannel
{
    public readonly ConcurrentQueue<PacketEntry> Queue = new ConcurrentQueue<PacketEntry>();
    public readonly ManualResetEventSlim Signal = new ManualResetEventSlim(false);
}
