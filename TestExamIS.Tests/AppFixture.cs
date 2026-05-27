using System.Diagnostics;

namespace TestExamIS.Tests;

public class AppFixture : IAsyncLifetime
{
    private Process? _appProcess;
    private const string AppUrl = "http://localhost:5210";

    public async Task InitializeAsync()
    {
        if (!await IsAppRunning())
        {
            var a = Directory.GetCurrentDirectory();
            var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = projectPath,
                Arguments = $"run --project BookBorrowingSystem",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            
            startInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Test";
            startInfo.EnvironmentVariables["DOTNET_ENVIRONMENT"] = "Test";            

            _appProcess = Process.Start(startInfo);

            // Wait until the app is reachable
            for (int i = 0; i < 30; i++)
            {
                if (await IsAppRunning()) break;
                await Task.Delay(1000);
            }
            
            if (!await IsAppRunning())
                throw new Exception("App did not start in time.");
        }
    }

    public Task DisposeAsync()
    {
        if (_appProcess is { HasExited: false })
        {
            try
            {
                // Kill the entire process tree, not just the parent
                KillProcessAndChildren(_appProcess.Id);
                _appProcess.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error killing process: {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }

    private static void KillProcessAndChildren(int pid)
    {
        if (OperatingSystem.IsWindows())
        {
            // Windows: Use taskkill with /T flag to kill tree
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/PID {pid} /T /F",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit();
        }
        else
        {
            // Linux/Mac: Kill process group
            try
            {
                var process = Process.GetProcessById(pid);
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Fallback: use pkill
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "pkill",
                        Arguments = $"-P {pid}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit();
            }
        }
    }
    private static async Task<bool> IsAppRunning()
    {
        try
        {
            using var client = new HttpClient();
            var response = await client.GetAsync(AppUrl);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}