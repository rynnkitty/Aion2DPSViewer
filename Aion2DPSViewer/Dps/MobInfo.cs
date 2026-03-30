namespace Aion2DPSViewer.Dps;

public class MobInfo
{
    public int MobId { get; set; }
    public int MobCode { get; set; }
    public string Name { get; set; } = "";
    public long MaxHp { get; set; }
    public long CurrentHp { get; set; }
    public bool IsBoss { get; set; }
}
