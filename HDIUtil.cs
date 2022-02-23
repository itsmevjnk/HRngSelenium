/*
 * HDIUtil.cs - Functions for working with the hdiutil disk/image mounter
 *              and manipulation utility available on macOS/Mac OS X.
 * Created on: 10:30 23-02-2022
 * Author    : itsmevjnk
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HRngSelenium
{
    [System.Runtime.Versioning.SupportedOSPlatform("osx")]
    internal static class HDIUtil
    {
        /// <summary>
        ///  Mount an image file or block device.<br/>
        ///  This function is asynchronous.
        /// </summary>
        /// <param name="path">The path to the image file or block device.</param>
        /// <param name="noverify">Whether the mounting file/device will not be verified for integrity before mounting. Disabled by default.</param>
        /// <returns>A list of tuples containing the block device, partition type and mountpoint path.</returns>
        public static async Task<List<(string blkdev, string ptype, string mntpoint)>> Attach(string path, bool noverify = false)
        {
            using (Process hdiutil = new Process())
            {
                hdiutil.StartInfo.FileName = "hdiutil";
                hdiutil.StartInfo.Arguments = $"attach {((noverify) ? "-noverify" : "")} \"{path}\"";
                hdiutil.StartInfo.UseShellExecute = false;
                hdiutil.StartInfo.RedirectStandardOutput = true;
                hdiutil.StartInfo.CreateNoWindow = true;

                hdiutil.Start();
                string[] output = (await hdiutil.StandardOutput.ReadToEndAsync()).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries); // Lines returned by hdiutil
                hdiutil.WaitForExit();
                
                List<(string blkdev, string ptype, string mntpoint)> ret = new List<(string blkdev, string ptype, string mntpoint)>(); // Returning values
                foreach (string line in output)
                {
                    if (line.StartsWith("/dev/disk"))
                    {
                        /* Valid line containing block device */
                        string[] components = line.Split('\t');
                        ret.Add((components[0].TrimEnd(), components[1].TrimEnd(), components[2]));
                    }
                }
                return ret;
            }
        }

        /// <summary>
        ///  Unmount a mountpoint or block device.<br/>
        ///  This function is asynchronous.
        /// </summary>
        /// <param name="path">The path to the block device or mountpoint to be unmounted.</param>
        public static async Task Detach(string path)
        {
            using (Process hdiutil = new Process())
            {
                hdiutil.StartInfo.FileName = "hdiutil";
                hdiutil.StartInfo.Arguments = $"detach \"{path}\"";
                hdiutil.StartInfo.UseShellExecute = false;
                hdiutil.StartInfo.CreateNoWindow = true;
                hdiutil.StartInfo.RedirectStandardOutput = true;

                hdiutil.Start();
                await hdiutil.StandardOutput.ReadToEndAsync();
                await hdiutil.WaitForExitAsync();
            }
        }
    }
}