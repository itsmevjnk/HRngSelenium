/*
 * Versioning.cs - Functions to handle version numbers.
 * Created on: 13:56 02-12-2021
 * Author    : itsmevjnk
 */

using System;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.IO;

namespace HRngBackend
{
    internal static class Versioning
    {
        /*
         * public int GetVersion(string version, int idx)
         *   Retrieves the specified version index from a x.y.z.* version
         *   string that can be retrieved from Chrome/ChromeDriver and
         *   Firefox.
         *   Input : version: The version string.
         *           idx    : The version index.
         *   Output: The version number, or -1 if the string is invalid.
         */
        public static int GetVersion(string version, int idx)
        {
            if (idx < 0) return -1;
            string[] components = version.Split('.');
            if (components.Length < (idx + 1) || !components[idx].All(char.IsDigit)) return -1;
            return Convert.ToInt32(components[idx]);
        }

        /*
         * public int GetMajorVersion(string version)
         *   Retrieves the major version from a version string.
         *   Input : version: The version string.
         *   Output: The version number.
         */
        public static int GetMajVersion(string version)
        {
            return GetVersion(version, 0);
        }

        /*
         * public int CompareVersion(string a, string b, [int max_idx])
         *   Compares the two version strings a and b.
         *   Input : a, b   : Version strings in x.y.z.* format.
         *           max_idx: The maximum index to compare (optional).
         *   Output: 0 if the two versions are the same, or the difference
         *           between strings a and b's version where there's
         *           mismatch (i.e. a[i] - b[i] if a[b] != b[i])
         */
        public static int CompareVersion(string a, string b, int max_idx = -1)
        {
            for(int i = 0; (max_idx < 0 || i <= max_idx); i++)
            {
                int ai = GetVersion(a, i), bi = GetVersion(b, i); // Version at index i
                if (ai == -1 || bi == -1) return 0; // We've hit the end of one of the version strings, and so far we're still going, which means the two versions are identical
                if (ai != bi) return (ai - bi); // Mismatch
            }
            return 0;
        }

        /*
         * public string ExecVersion(string path)
         *   Retrieves the version of the specified executable.
         *   On Windows, this function gets the executable's product
         *   version. On other platforms, this functions returns the
         *   last part (containing the version) of the output from
         *   [executable path] --version.
         *   Input : path: The path to the executable.
         *   Output: The executable's version, or an empty string if
         *           the file does not exist.
         */
        public static string ExecVersion(string path, int? idx = null)
        {
            if (!File.Exists(path)) return "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                FileVersionInfo version = FileVersionInfo.GetVersionInfo(path);
                if(version.ProductVersion != null) return version.ProductVersion;
            }
            Process process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.FileName = path;
            process.StartInfo.Arguments = "--version";
            process.Start();
            string[] output = process.StandardOutput.ReadToEnd().Trim().Split(' ');
            process.WaitForExit();
            return Regex.Replace((idx == null) ? output.Last() : output[(int) idx], "[a-zA-Z]", "");
        }
    }
}
