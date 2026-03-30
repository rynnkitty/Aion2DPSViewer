using System.Collections.Generic;

namespace Aion2DPSViewer.Dps;

public class DpsSnapshot
{
    public double ElapsedSeconds { get; set; }
    public double WallElapsedSeconds { get; set; }
    public long TotalPartyDamage { get; set; }
    public MobTarget? Target { get; set; }
    public List<ActorDps> Players { get; set; } = new List<ActorDps>();
}
