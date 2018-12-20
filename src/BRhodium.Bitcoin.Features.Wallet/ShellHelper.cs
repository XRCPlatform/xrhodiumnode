using System;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace BRhodium.Bitcoin.Features.Wallet
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



        public static Response Run(string cmd,  string dir = "")
        {
            var result = new Response();
            var stderr = new StringBuilder();
            var stdout = new StringBuilder();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = GetFileName();
            startInfo.Arguments = cmd;
            startInfo.RedirectStandardOutput = false;
            startInfo.RedirectStandardError = false;
            startInfo.UseShellExecute = true;
            startInfo.CreateNoWindow = false;
            if (!String.IsNullOrEmpty(dir))
            {
                startInfo.WorkingDirectory = dir;
            }

            Process process = Process.Start(startInfo);
            //result.stdout = process.StandardOutput.ReadToEnd();
            //result.stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return result;
        }
    }
}