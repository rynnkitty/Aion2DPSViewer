using System.Collections.Generic;

namespace Aion2DPSViewer.Forms;

public class SettingsData
{
    public string Refresh { get; set; } = "";

    public string Toggle { get; set; } = "";

    public string Compact { get; set; } = "";

    public string SwitchTab { get; set; } = "";

    public bool OverlayOnly { get; set; }

    public int TextScale { get; set; } = 100;

    public int FontScale { get; set; } = 100;

    public Dictionary<string, List<string>>? TrackedSkills { get; set; }

    public bool ShowParty { get; set; } = true;

    public bool ShowSelf { get; set; } = true;

    public bool AutoTabSwitch { get; set; } = true;

    public string DpsPercentMode { get; set; } = "party";

    public string ScoreFormat { get; set; } = "full";

    public string DpsTimeMode { get; set; } = "wallclock";

    public string GpuMode { get; set; } = "off";
}
