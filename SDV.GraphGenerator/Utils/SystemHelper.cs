using System.Runtime.InteropServices;

namespace SDV.GraphGenerator.Utils;

public static class SystemHelper
{
    [DllImport("UXTheme.dll", SetLastError = true, EntryPoint = "#138")]
    public static extern bool IsDarkModeEnabled();

}