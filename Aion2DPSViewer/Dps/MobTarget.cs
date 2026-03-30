using System.Collections.Generic;

namespace Aion2DPSViewer.Dps;

public class MobTarget
{
    public string Name { get; set; } = "";
    public long MaxHp { get; set; }
    public long CurrentHp { get; set; }
    public long TotalDamageReceived { get; set; }
    public bool IsBoss { get; set; }
    public List<BuffUptimeEntry>? Debuffs { get; set; }
}
