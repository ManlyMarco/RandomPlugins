using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace HoneyComeSteamPassthrough
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main()
        {
            try
            {
                var gameRootDir = Path.GetDirectoryName(Application.ExecutablePath) ?? throw new InvalidOperationException("Failed to extract directory name from: " + Application.ExecutablePath);
                var gameExePath = Path.Combine(gameRootDir, "InitSetting.exe");
                if (!File.Exists(gameExePath))
                {
                    gameExePath = Path.Combine(gameRootDir, "HoneyCome.exe");
                
                    if (!File.Exists(gameExePath))
                    {
                        MessageBox.Show("The game executable is missing, make sure to put this exe inside of HoneyCome root folder, where HoneyCome.exe is.", "Failed to start", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return 1;
                    }
                }
                
                // TryGetArgsString crashes when started from steam for some reason with index out of range
                //Process.Start(new ProcessStartInfo(gameExePath, TryGetArgsString())
                Process.Start(new ProcessStartInfo(gameExePath)
                {
                    UseShellExecute = false,
                });
                return 0;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString(), "Failed to start", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 2;
            }
        }

        private static string TryGetArgsString()
        {
            try
            {
                var exe = Environment.GetCommandLineArgs()[0]; // Command invocation part
                var rawCmd = Environment.CommandLine; // Complete command
                var argsOnly = rawCmd.Remove(rawCmd.IndexOf(exe), exe.Length).TrimStart('"').Substring(1);
                return argsOnly;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString(), "Failed to start", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return string.Empty;
            }
        }
    }
}
