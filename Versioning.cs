/*
 * Versioning.cs - Functions to handle version numbers.
 * Created on: 13:56 02-12-2021
 * Author    : itsmevjnk
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace HRngSelenium
{
    internal static class Versioning
    {
        /// <summary>
        ///  Retrieves the specified version index from a x.y.z.* version string that can be retrieved from Chrome/ChromeDriver and Firefox.
        /// </summary>
        /// <param name="version">The version string.</param>
        /// <param name="idx">The version index.</param>
        /// <returns>The version number, or -1 if the string is invalid.</returns>
        public static int GetVersion(string version, int idx)
        {
            if (idx < 0) return -1;
            string[] components = version.Split('.');
            if (components.Length < (idx + 1) || !components[idx].All(char.IsDigit)) return -1;
            return Convert.ToInt32(components[idx]);
        }

        /// <summary>
        ///  Retrieves the major version from a version string.
        /// </summary>
        /// <param name="version">The version string.</param>
        /// <returns>The version number.</returns>
        public static int GetMajVersion(string version)
        {
            return GetVersion(version, 0);
        }

        /// <summary>
        ///  Compares the two version strings a and b.
        /// </summary>
        /// <param name="a">Version string in x.y.z.* format.</param>
        /// <param name="b">Version string in x.y.z.* format.</param>
        /// <param name="max_idx">The maximum index to compare (optional).</param>
        /// <returns>0 if the two versions are the same, or the difference between strings a and b's version where there's mismatch (i.e. <c>a[i] - b[i]</c> if <c>a[b] != b[i]</c>)</returns>
        public static int CompareVersion(string a, string b, int max_idx = -1)
        {
            for (int i = 0; (max_idx < 0 || i <= max_idx); i++)
            {
                int ai = GetVersion(a, i), bi = GetVersion(b, i); // Version at index i
                if (ai == -1 || bi == -1) return 0; // We've hit the end of one of the version strings, and so far we're still going, which means the two versions are identical
                if (ai != bi) return (ai - bi); // Mismatch
            }
            return 0;
        }

        /// <summary>
        ///  Retrieves the version of the specified executable.<br/>
        ///  On Windows, this function gets the executable's product version. On other platforms, this functions returns the last part (containing the version) of the output from <c>[executable path] --version</c>.
        /// </summary>
        /// <param name="path">The path to the executable.</param>
        /// <returns>The executable's version, or an empty string if the file does not exist.</returns>
        public static string ExecVersion(string path, int? idx = null)
        {
            if (!File.Exists(path)) return "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                FileVersionInfo version = FileVersionInfo.GetVersionInfo(path);
                if (version.ProductVersion != null) return version.ProductVersion;
            }
            Process process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.FileName = path;
            process.StartInfo.Arguments = "--version";
            process.Start();
            string[] output = process.StandardOutput.ReadToEnd().Trim().Split(' ');
            process.WaitForExit();
            return Regex.Replace((idx == null) ? output.Last() : output[(int)idx], "[a-zA-Z]", "");
        }
    }
}
