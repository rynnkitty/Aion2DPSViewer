namespace Aion2DPSViewer.Packet;

internal readonly record struct PacketEntry(
    int SrcPort,
    int DstPort,
    byte[] Data,
    string DeviceName,
    uint SeqNum)
;
