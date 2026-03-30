namespace Aion2DPSViewer.Packet;

public class PartyMember
{
    public uint CharacterId { get; set; }
    public int ServerId { get; set; }
    public string ServerName { get; set; } = "";
    public string Nickname { get; set; } = "";
    public int JobCode { get; set; }
    public string JobName { get; set; } = "";
    public int Level { get; set; }
    public int CombatPower { get; set; }
}
