using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using TruePath;
using TruePath.SystemIo;

namespace DiscordScreenshareLoopbackShutup.Services;

public class InstallerService
{
    public static void DoInstall()
    {
#if DEBUG
        // Check if we in debug mode
        return;
#endif

        var currentExePath = new AbsolutePath(Environment.ProcessPath!);
        if ((currentExePath.Parent!.Value / ("DiscordScreenshareLoopbackShutup" + ".dll")).Exists())
            throw new Exception("Did you compiled the Release binary by yourself? " +
                                "Please, do not. Use 'dotnet publish' to get a single file " +
                                "or build Debug build to debug");


        var targetFolder = Program.GetAppropriateProgramFolderPath();
        var targetExePath = targetFolder / ("DiscordScreenshareLoopbackShutup" + ".exe");

        // Check if we're already in the appropriate folder
        if (currentExePath.Parent == targetFolder) return;

        // Check if a copy is already running from the target folder
        var currentProcess = Process.GetCurrentProcess();
        var existingProcesses = Process.GetProcessesByName(currentProcess.ProcessName)
            .Where(p => p.Id != currentProcess.Id && targetExePath == new AbsolutePath(p.MainModule?.FileName!));

        // Kill existing processes from target folder
        foreach (var process in existingProcesses)
            try
            {
                process.Kill();
                process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to kill process {process.Id}: {ex.Message}");
            }

        // Wait a bit to ensure file is unlocked
        Thread.Sleep(100);

        targetFolder.CreateDirectory();

        // Copy current exe to target location
        try
        {
            currentExePath.Copy(targetExePath, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to copy executable: {ex.Message}");
            throw;
        }

        // Create Start Menu shortcut if it doesn't exist
        CreateStartMenuShortcut(targetExePath);

        // Create scheduled task for autostart if it doesn't exist
        CreateScheduledTask(targetExePath);

        Process.Start(targetExePath.ToString());
        Thread.Sleep(500);
    }

    private static void CreateStartMenuShortcut(AbsolutePath targetExePath)
    {
        try
        {
            var shortcutPath = new AbsolutePath(Environment.GetFolderPath(Environment.SpecialFolder.Programs))
                               / "Discord Screenshare Loopback Shutup.lnk";

            if (shortcutPath.Exists()) return;

            // Use PowerShell to create shortcut
            var psScript = $"""
                            $WshShell = New-Object -ComObject WScript.Shell
                            $Shortcut = $WshShell.CreateShortcut('{shortcutPath}')
                            $Shortcut.TargetPath = '{targetExePath}'
                            $Shortcut.WorkingDirectory = '{targetExePath.Parent}'
                            $Shortcut.Description = 'Discord Screenshare Loopback Shutup'
                            $Shortcut.IconLocation = '{targetExePath},0'
                            $Shortcut.Save()
                            """;
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace("\"", "`\"")}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit();

            if (process?.ExitCode == 0)
                Console.WriteLine($"Created Start menu shortcut: {shortcutPath}");
            else
                Console.WriteLine($"Failed to create Start menu shortcut. Exit code: {process?.ExitCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create Start menu shortcut: {ex.Message}");
        }
    }

    private static void CreateScheduledTask(AbsolutePath targetExePath)
    {
        try
        {
            const string taskName = "Discord Screenshare Loopback Shutup";

            // Check if task already exists
            var queryPsi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Query /TN \"{taskName}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var queryProcess = Process.Start(queryPsi))
            {
                queryProcess?.WaitForExit();
                if (queryProcess?.ExitCode == 0)
                {
                    Console.WriteLine("Scheduled task already exists.");
                    return;
                }
            }

            // Create XML for task definition
            var userName = $"{Environment.UserDomainName}\\{Environment.UserName}";
            var xmlContent = $"""
                              <?xml version="1.0" encoding="UTF-16"?>
                              <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
                                <RegistrationInfo>
                                  <Description>Starts DiscordScreenshareLoopbackShutup at user logon</Description>
                                </RegistrationInfo>
                                <Triggers>
                                  <LogonTrigger>
                                    <Enabled>true</Enabled>
                                    <UserId>{userName}</UserId>
                                  </LogonTrigger>
                                </Triggers>
                                <Principals>
                                  <Principal id="Author">
                                    <UserId>{userName}</UserId>
                                    <LogonType>InteractiveToken</LogonType>
                                    <RunLevel>LeastPrivilege</RunLevel>
                                  </Principal>
                                </Principals>
                                <Settings>
                                  <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                                  <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                                  <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                                  <AllowHardTerminate>true</AllowHardTerminate>
                                  <StartWhenAvailable>true</StartWhenAvailable>
                                  <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                                  <IdleSettings>
                                    <StopOnIdleEnd>false</StopOnIdleEnd>
                                    <RestartOnIdle>false</RestartOnIdle>
                                  </IdleSettings>
                                  <AllowStartOnDemand>true</AllowStartOnDemand>
                                  <Enabled>true</Enabled>
                                  <Hidden>false</Hidden>
                                  <RunOnlyIfIdle>false</RunOnlyIfIdle>
                                  <WakeToRun>false</WakeToRun>
                                  <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                                  <Priority>7</Priority>
                                </Settings>
                                <Actions Context="Author">
                                  <Exec>
                                    <Command>{targetExePath}</Command>
                                    <WorkingDirectory>{targetExePath.Parent!.Value}</WorkingDirectory>
                                  </Exec>
                                </Actions>
                              </Task>
                              """;

            // Save XML to temp file
            var tempXmlPath = Path.GetTempFileName();
            File.WriteAllText(tempXmlPath, xmlContent);

            try
            {
                // Create task using schtasks
                var createPsi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Create /TN \"{taskName}\" /XML \"{tempXmlPath}\" /F",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var createProcess = Process.Start(createPsi);
                createProcess?.WaitForExit();

                if (createProcess?.ExitCode == 0)
                {
                    Console.WriteLine($"Created scheduled task: {taskName}");
                }
                else
                {
                    var error = createProcess?.StandardError.ReadToEnd();
                    Console.WriteLine(
                        $"Failed to create scheduled task. Exit code: {createProcess?.ExitCode}, Error: {error}");
                }
            }
            finally
            {
                // Clean up temp file
                File.Delete(tempXmlPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create scheduled task: {ex.Message}");
        }
    }
}