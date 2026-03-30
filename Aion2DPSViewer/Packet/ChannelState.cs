using System;

namespace Aion2DPSViewer.Packet;

internal sealed class ChannelState
{
    public readonly TcpReassembler ServerToClient;
    public readonly TcpReassembler ClientToServer;
    public readonly StreamProcessor StreamProcessor;

    public ChannelState(PacketProcessor owner)
    {
        StreamProcessor = new StreamProcessor(owner);
        ServerToClient = new TcpReassembler(data => StreamProcessor.ProcessData(data));
        ClientToServer = new TcpReassembler(_ => { });
    }
}
