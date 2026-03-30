namespace Aion2DPSViewer.Dps;

public class BuffUptimeEntry
{
    public int BuffId { get; set; }
    public string Name { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public double UptimeSeconds { get; set; }
    public double UptimePercent { get; set; }
    public int CasterId { get; set; }
}
