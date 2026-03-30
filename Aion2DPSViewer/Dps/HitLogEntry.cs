using System.Text.Json.Serialization;

namespace Aion2DPSViewer.Dps;

public class HitLogEntry
{
    public double T { get; set; }
    public int EntityId { get; set; }
    public string SkillName { get; set; } = "";
    [JsonIgnore]
    public string? SkillIcon { get; set; }
    public string? SkillType { get; set; }
    public int Damage { get; set; }
    public uint Flags { get; set; }
}
