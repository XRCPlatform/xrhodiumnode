using System;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace BRhodium.Node.Utilities
{
    public static class OS
    {
        public static bool IsWin() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static bool IsMac() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static bool IsGnu() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        public static string GetCurrent()
        {
            return
            (IsWin() ? "win" : null) ??
            (IsMac() ? "mac" : null) ??
            (IsGnu() ? "gnu" : null);
        }
    }

    public class Response
    {
        public int code { get; set; }
        public string stdout { get; set; }
        public string stderr { get; set; }
    }

    public enum Output
    {
        Hidden,
        Internal,
        External
    }

    public static class ShellHelper
    {
        private static string GetFileName()
        {
            string fileName = "";
            try
            {
                switch (OS.GetCurrent())
                {
                    case "win":
                        fileName = "cmd.exe";
                        break;
                    case "mac":
                    case "gnu":
                        fileName = "/bin/bash";
                        break;
                }
            }
            catch (Exception Ex)
            {
                Console.WriteLine(Ex.Message);
            }
            return fileName;
        }

        private static string GetArguments(string cmd)
        {
            string arguments = "";
            try
            {
                switch (OS.GetCurrent())
                {
                    case "win":
                        arguments = " /K " + cmd;
                        break;
                    case "mac":
                    case "gnu":
                        arguments = " -c " + cmd;
                        break;
                }
            }
            catch (Exception Ex)
            {
                Console.WriteLine(Ex.Message);
            }
            return arguments;
        }

        /// <summary>
        /// Runs shell command
        /// </summary>
        /// <param name="cmd">Command</param>
        /// <returns>Result of outputs</returns>
        public static Response Run(string cmd)
        {
            var result = new Response();

            var stderr = new StringBuilder();
            var stdout = new StringBuilder();

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = GetFileName();
            startInfo.Arguments = GetArguments(cmd);
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.UseShellExecute = false;

            Process p = new Process();

            p.OutputDataReceived += (sender, args) => stdout.AppendLine(args.Data);
            p.ErrorDataReceived += (sender, args) => stderr.AppendLine(args.Data);

            p.StartInfo = startInfo;

            p.Start();

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            p.WaitForExit(3000);
            result.stdout = stdout.ToString();
            result.stderr = stderr.ToString();
            p.Close();

            return result;
        }
    }
}