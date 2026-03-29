using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Paper.Rendering.Silk.NET
{
    /// <summary>
    /// Opens the OS-native file open/save dialogs on macOS (via osascript),
    /// Windows (via PowerShell), and Linux (via zenity).
    /// All methods are non-blocking: they launch the dialog in a background Task
    /// and invoke <paramref name="onResult"/> with the selected path (or null if cancelled).
    /// </summary>
    public static class NativeFileDialog
    {
        /// <summary>
        /// Opens a native "Open File" dialog and invokes <paramref name="onResult"/>
        /// with the selected path, or null if the user cancelled.
        /// </summary>
        public static void OpenFile(Action<string?> onResult, string? title = null, string? defaultPath = null)
        {
            Task.Run(() =>
            {
                try { onResult(RunOpenDialog(title, defaultPath)); }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[NativeFileDialog] OpenFile error: " + ex.Message);
                    onResult(null);
                }
            });
        }

        /// <summary>
        /// Opens a native "Save File" dialog and invokes <paramref name="onResult"/>
        /// with the chosen path, or null if the user cancelled.
        /// </summary>
        public static void SaveFile(Action<string?> onResult, string? title = null, string? defaultName = null)
        {
            Task.Run(() =>
            {
                try { onResult(RunSaveDialog(title, defaultName)); }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[NativeFileDialog] SaveFile error: " + ex.Message);
                    onResult(null);
                }
            });
        }

        // ── Platform implementations ──────────────────────────────────────────

        private static string? RunOpenDialog(string? title, string? defaultPath)
        {
            if (OperatingSystem.IsMacOS())
            {
                string prompt = (title ?? "Open File").Replace("\"", "\\\"");
                string script = $"POSIX path of (choose file with prompt \"{prompt}\")";
                return RunOsascript(script)?.Trim();
            }
            if (OperatingSystem.IsWindows())
            {
                string initDir = defaultPath != null ? $"$d.InitialDirectory='{EscapePs(defaultPath)}';" : "";
                string ps = $"Add-Type -AssemblyName System.Windows.Forms; $d=New-Object System.Windows.Forms.OpenFileDialog; {initDir} if($d.ShowDialog() -eq 'OK'){{$d.FileName}}";
                return RunProcessArgs("powershell", ["-NoProfile", "-Command", ps])?.Trim();
            }
            if (OperatingSystem.IsLinux())
            {
                var args = new System.Collections.Generic.List<string> { "--file-selection", $"--title={title ?? "Open File"}" };
                if (defaultPath != null) args.Add($"--filename={defaultPath}");
                return RunProcessArgs("zenity", args.ToArray())?.Trim();
            }
            return null;
        }

        private static string? RunSaveDialog(string? title, string? defaultName)
        {
            if (OperatingSystem.IsMacOS())
            {
                string prompt = (title ?? "Save File").Replace("\"", "\\\"");
                string nameClause = defaultName != null ? $" default name \"{defaultName.Replace("\"", "\\\"")}\"" : "";
                string script = $"POSIX path of (choose file name with prompt \"{prompt}\"{nameClause})";
                return RunOsascript(script)?.Trim();
            }
            if (OperatingSystem.IsWindows())
            {
                string fileName = defaultName != null ? $"$d.FileName='{EscapePs(defaultName)}';" : "";
                string ps = $"Add-Type -AssemblyName System.Windows.Forms; $d=New-Object System.Windows.Forms.SaveFileDialog; {fileName} if($d.ShowDialog() -eq 'OK'){{$d.FileName}}";
                return RunProcessArgs("powershell", ["-NoProfile", "-Command", ps])?.Trim();
            }
            if (OperatingSystem.IsLinux())
            {
                var args = new System.Collections.Generic.List<string> { "--file-selection", "--save", "--confirm-overwrite", $"--title={title ?? "Save File"}" };
                if (defaultName != null) args.Add($"--filename={defaultName}");
                return RunProcessArgs("zenity", args.ToArray())?.Trim();
            }
            return null;
        }

        // ── Process helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Run osascript with the given AppleScript. Each argument is passed separately
        /// via ArgumentList so no shell quoting is needed.
        /// </summary>
        private static string? RunOsascript(string script)
            => RunProcessArgs("osascript", ["-e", script]);

        /// <summary>
        /// Starts a process with each argument passed as a discrete entry in ArgumentList,
        /// bypassing any shell interpretation.
        /// </summary>
        private static string? RunProcessArgs(string exe, string[] args)
        {
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var proc = new Process { StartInfo = psi };
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            string result = output.Trim();
            return result.Length == 0 ? null : result;
        }

        private static string EscapePs(string s) => s.Replace("'", "''");
    }
}
