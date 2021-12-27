/*
 * SevenZip.cs - Functions for downloading and serving command-line
 *               7-Zip (7za) binaries.
 * Created on: 22:00 23-12-2021
 * Author    : itsmevjnk
 */

using System;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Diagnostics;

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

            return true;
        }

        /*
         * public static async Task<int> Extract(string archive, [string? output_dir],
         *                                [string[] files], [bool overwrite], [bool atomic])
         *   Extracts an archive using 7za.
         *   Input : archive   : The archive to be extracted.
         *           output_dir: The directory to extract to (optional).
         *           files     : The list of files to extract (optional).
         *                       If this argument is not specified or is
         *                       empty, all files will be extracted.
         *           overwrite : Whether 7za can overwrite existing files
         *                       (optional). If this argument is not specified,
         *                       7za will overwrite existing files.
         *           atomic    : This option pipes 7za's extract output to another
         *                       7za instance for **actually** extracting the archive.
         *                       Used for tar.gz/xz/lz formats.
         *   Output: -999 if the 7za binary does not exist, or the return value
         *           from 7za.
         */
        public static async Task<int> Extract(string archive, string? output_dir = null, string[]? files = null, bool overwrite = true, bool atomic = false)
        {
            files = files ?? new string[0];
            if (BinaryPath == "" || !File.Exists(BinaryPath)) return -999; // 7za does not exist
            Process proc7z = new Process();
            proc7z.StartInfo.FileName = BinaryPath;
            proc7z.StartInfo.UseShellExecute = false;
            proc7z.StartInfo.RedirectStandardOutput = true;
            proc7z.StartInfo.CreateNoWindow = true;

            /* Construct args */
            proc7z.StartInfo.Arguments = "e";
            if (overwrite) proc7z.StartInfo.Arguments += " -y";
            if (output_dir != null && output_dir != "") proc7z.StartInfo.Arguments += $" -o\"{output_dir}\"";
            if (!atomic) proc7z.StartInfo.Arguments += $" \"{archive}\"";
            else proc7z.StartInfo.Arguments += " -si -ttar"; // Take input from stdin, and treat it as TAR (which it is)
            foreach (string file in files) proc7z.StartInfo.Arguments += $" {file}";

            if (!atomic) proc7z.Start();
            else
            {
                /* Set up preprocessing 7za process */
                Process proc7z_pre = new Process();
                proc7z_pre.StartInfo.FileName = BinaryPath;
                proc7z_pre.StartInfo.UseShellExecute = false;
                proc7z_pre.StartInfo.RedirectStandardOutput = true;
                proc7z_pre.StartInfo.CreateNoWindow = true;
                proc7z_pre.StartInfo.Arguments = $"x \"{archive}\" -so"; // Extract and pipe output to stdout
                proc7z.StartInfo.RedirectStandardInput = true; // Crucial for our piping operation

                proc7z_pre.Start(); proc7z.Start(); // Start both processes
                using(StreamReader instream = proc7z_pre.StandardOutput)
                {
                    using(StreamWriter outstream = proc7z.StandardInput)
                    {
                        while(true)
                        {
                            int b = instream.Read();
                            if (b == -1) break; // End of input stream
                            outstream.Write(new byte[] { (byte)b }); // Write byte to output stream
                        }
                        instream.Close(); outstream.Close(); // Finish piping
                    }
                }
            }
            await proc7z.StandardOutput.ReadToEndAsync();
            proc7z.WaitForExit();

            return proc7z.ExitCode;
        }
#nullable disable
    }
}