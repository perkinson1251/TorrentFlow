using System.ComponentModel;

namespace TorrentFlow.Enums;

public enum ThemeType
{
    [Description("System Default")] Default = 0,
    [Description("Light")] Light = 1,
    [Description("Dark")] Dark = 2
}