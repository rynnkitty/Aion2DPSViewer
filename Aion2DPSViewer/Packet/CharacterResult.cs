namespace Aion2DPSViewer.Packet;

public class CharacterResult
{
    public string Nickname { get; set; } = "";
    public int? ServerId { get; set; }
    public string ServerName { get; set; } = "";
    public string Type { get; set; } = "char_view";
    public bool NickTrimmed { get; set; }
}
