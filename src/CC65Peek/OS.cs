using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CC65Peek;

public static class OS
{
    public static async Task<bool> IsCallable(string filename, string arguments)
    {
        try {
            if (OperatingSystem.IsWindows())
                filename += ".exe";
            
            var p = Process.Start(filename, arguments);
            await p.WaitForExitAsync();
            return p.ExitCode == 0;
        }
        catch (Exception) {
            return false;
        }
    }
}