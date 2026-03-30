using System.Collections.Generic;

namespace Aion2DPSViewer.Dps;

public class TimelineEntry
{
    public int T { get; set; }
    public List<TimelinePlayer> Players { get; set; } = new List<TimelinePlayer>();
}
