using System.ComponentModel;

namespace TorrentFlow.Enums
{
    public enum SpeedUnitType
    {
        [Description("Kb")]
        Kb = 0,
        [Description("Mb")]
        Mb = 1,
        [Description("Gb")]
        Gb = 2
    }
}