using System.Diagnostics;

namespace pdutil;

internal class PowerShell
{
    public static int RunScript(string ps)
    {
        Process process = Process.Start(new ProcessStartInfo("powershell.exe", "-Command " + ps)
        {
            CreateNoWindow = true,
            UseShellExecute = false
        });
        process.WaitForExit();
        int exitCode = process.ExitCode;
        process.Close();
        return exitCode;
    }
}
