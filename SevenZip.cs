/*
 * SevenZip.cs - Functions for downloading and serving command-line
 *               7-Zip (7za) binaries.
 * Created on: 22:00 23-12-2021
 * Author    : itsmevjnk
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace HRngBackend
{
    public static class SevenZip
    {
        /*
         * public string BinaryPath
         *   Path to the 7za binary.
         */
        public static string BinaryPath = Path.Combine(BaseDir.PlatformBase, "7za" + ((RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) ? ".exe" : ""));

        /*
         * public static bool Exists()
         *   Checks if 7za exists.
         *   Input : none.
         *   Output: true if 7za exists, or false otherwise.
         */
        public static bool Exists()
        {
            return File.Exists(BinaryPath);
        }

        /*
         * public static async Task<bool> Initialize([Func<bool> consent])
         *   Checks if 7za exists, and downloads it if it doesn't
         *   exist. This function should only be called once by the
         *   frontend during its initialization sequence.
         *   Input : consent: The function to ask for user's consent
         *                    to download 7za. Returns true if the
         *                    user allows, or false otherwise.
         *   Output: true if the initialization is successful, or
         *           false otherwise.
         */
#nullable enable
        public static async Task<bool> Initialize(Func<bool>? consent = null)
        {
            if (!File.Exists(BinaryPath))
            {
                if (consent != null && !consent()) return false;

                string url = "https://github.com/develar/7zip-bin/raw/master/";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) url += "win/";
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) url += "mac/";
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) url += "linux/";
                else throw new InvalidOperationException("7za is not available for the running OS");

                switch (RuntimeInformation.OSArchitecture)
                {
                    case Architecture.X86: url += "ia32/"; break;
                    case Architecture.X64: url += "x64/"; break;
                    case Architecture.Arm: url += "arm/"; break;
                    case Architecture.Arm64: url += "arm64/"; break;
                    default: throw new InvalidOperationException($"7za is not available for the running architecture ({Convert.ToString(RuntimeInformation.OSArchitecture)})");
                }

                url += "7za";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) url += ".exe";

                var resp = await CommonHTTP.Client.GetAsync(url);
                resp.EnsureSuccessStatusCode();
                using (var fs = new FileStream(BinaryPath, FileMode.CreateNew)) await resp.Content.CopyToAsync(fs);
            }

            /* Make sure that 7za is executable (Linux/Unix only) */
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process chmod = new Process();
                chmod.StartInfo.FileName = "chmod";
                chmod.StartInfo.Arguments = $"+x {BinaryPath}";
                chmod.StartInfo.UseShellExecute = false;
                chmod.StartInfo.RedirectStandardOutput = true;
                chmod.StartInfo.CreateNoWindow = true;
                chmod.Start();
                chmod.StandardOutput.ReadToEnd();
                chmod.WaitForExit();
            }

            return true;
        }

        /*
         * public static async Task<int> Extract(string archive, [string? output_dir],
         *                                [string[] files], [bool overwrite])
         *   Extracts an archive using 7za.
         *   Input : archive   : The archive to be extracted.
         *           output_dir: The directory to extract to (optional).
         *           files     : The list of files to extract (optional).
         *                       If this argument is not specified or is
         *                       empty, all files will be extracted.
         *           overwrite : Whether 7za can overwrite existing files
         *                       (optional). If this argument is not specified,
         *                       7za will overwrite existing files.
         *   Output: -999 if the 7za binary does not exist, or the return value
         *           from 7za.
         */
        public static async Task<int> Extract(string archive, string? output_dir = null, string[]? files = null, bool overwrite = true)
        {
            files = files ?? new string[0];
            if (BinaryPath == "" || !File.Exists(BinaryPath)) return -999; // 7za does not exist
            Process proc7z = new Process();
            proc7z.StartInfo.FileName = BinaryPath;
            proc7z.StartInfo.WorkingDirectory = BaseDir.PlatformBase;
            proc7z.StartInfo.UseShellExecute = false;
            proc7z.StartInfo.RedirectStandardOutput = true;
            proc7z.StartInfo.CreateNoWindow = true;

            /* Construct args */
            proc7z.StartInfo.Arguments = "e";
            if (overwrite) proc7z.StartInfo.Arguments += " -y";
            if (output_dir != null && output_dir != "") proc7z.StartInfo.Arguments += $" -o\"{output_dir}\"";
            proc7z.StartInfo.Arguments += $" \"{archive}\"";
            foreach (string file in files) proc7z.StartInfo.Arguments += $" {file}";

            proc7z.Start();
            await proc7z.StandardOutput.ReadToEndAsync();
            proc7z.WaitForExit();

            return proc7z.ExitCode;
        }
#nullable disable
    }
}