using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Serilog;
using TruePath;
using TruePath.SystemIo;

namespace DiscordScreenshareLoopbackShutup.Services;

public class InstallerService
{
    private static readonly ILogger Logger = Log.ForContext<InstallerService>();

    public static void DoInstall()
    {
#if DEBUG
        // Check if we in debug mode
        Logger.Information("Skipping installation in DEBUG mode");
        return;
#endif

        var currentExePath = new AbsolutePath(Environment.ProcessPath!);
        if ((currentExePath.Parent!.Value / (Program.Name + ".dll")).Exists())
            throw new Exception("Did you compiled the Release binary by yourself? " +
                                "Please, do not. Use 'dotnet publish' to get a single file " +
                                "or build Debug build to debug");

        var targetFolder = Program.GetAppropriateProgramFolderPath();
        var targetExePath = targetFolder / (Program.Name + ".exe");

        // Check if we're already in the appropriate folder
        if (currentExePath.Parent == targetFolder)
        {
            Logger.Information("Running from target folder, skipping installation");
            return;
        }

        Logger.Information("Starting installation from {CurrentPath} to {TargetFolder}", currentExePath, targetFolder);

        StopInstalledProcesses(targetExePath);

        targetFolder.CreateDirectory();

        // Copy current exe to target location
        try
        {
            Logger.Information("Copying executable to {TargetExePath}", targetExePath);
            currentExePath.Copy(targetExePath, true);
        }
        catch (Exception ex)
        {
            Logger.Fatal(ex, "Failed to copy executable");
            throw;
        }

        // Create Start Menu shortcut if it doesn't exist
        CreateStartMenuShortcut(targetExePath);

        // Create scheduled task for autostart if it doesn't exist
        CreateScheduledTask(targetExePath);

        Logger.Information("Installation completed, starting new process");
        Process.Start(targetExePath.ToString());
        Thread.Sleep(500);
    }

    private static void StopInstalledProcesses(AbsolutePath targetExePath)
    {
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(targetExePath.ToString()));
        foreach (var process in processes)
        {
            if (process.Id == Environment.ProcessId)
                continue;

            try
            {
                if (process.HasExited)
                    continue;

                var executablePath = process.MainModule?.FileName;
                if (executablePath == null)
                    throw new InvalidOperationException($"Cannot determine executable path for process {process.Id}");

                if (targetExePath != new AbsolutePath(executablePath))
                    continue;

                Logger.Information("Killing existing process {ProcessId}", process.Id);
                process.Kill();
                if (!process.WaitForExit(5000))
                    throw new TimeoutException($"Process {process.Id} did not exit within 5 seconds");
            }
            catch (Exception ex)
            {
                if (process.HasExited)
                    continue;

                Logger.Error(ex, "Failed to stop installed process {ProcessId}", process.Id);
                throw;
            }
        }
    }

    private static void CreateStartMenuShortcut(AbsolutePath targetExePath)
    {
        try
        {
            var shortcutPath = new AbsolutePath(Environment.GetFolderPath(Environment.SpecialFolder.Programs))
                               / "Discord Screenshare Loopback Shutup.lnk";

            if (shortcutPath.Exists())
            {
                Logger.Debug("Start menu shortcut already exists at {ShortcutPath}", shortcutPath);
                return;
            }

            Logger.Information("Creating Start menu shortcut at {ShortcutPath}", shortcutPath);

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
                Logger.Information("Created Start menu shortcut");
            else
                Logger.Error("Failed to create Start menu shortcut. Exit code: {ExitCode}", process?.ExitCode);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to create Start menu shortcut");
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
                    Logger.Debug("Scheduled task {TaskName} already exists", taskName);
                    return;
                }
            }

            Logger.Information("Creating scheduled task {TaskName}", taskName);

            // Create XML for task definition
            var userName = $"{Environment.UserDomainName}\\{Environment.UserName}";
            var xmlContent = $"""
                              <?xml version="1.0" encoding="UTF-16"?>
                              <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
                                <RegistrationInfo>
                                  <Description>Starts {Program.Name} at user logon</Description>
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
                                    <Command>{targetExePath} --scheduled</Command>
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
                    Logger.Information("Created scheduled task: {TaskName}", taskName);
                }
                else
                {
                    var error = createProcess?.StandardError.ReadToEnd();
                    Logger.Error("Failed to create scheduled task. Exit code: {ExitCode}, Error: {Error}",
                        createProcess?.ExitCode, error);
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
            Logger.Error(ex, "Failed to create scheduled task");
        }
    }
}