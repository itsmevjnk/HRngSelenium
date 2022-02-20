/*
 * FirefoxHelper.cs - Functions for updating and initializing Safari for Selenium.
 *                    Please note that this is only supported on Mac OS X/macOS,
 *                    and that remote automation is enabled by the user (see
 *                    https://developer.apple.com/documentation/webkit/testing_with_webdriver_in_safari).
 * Created on: 11:58 20-02-2022
 * Author    : itsmevjnk
 */

using OpenQA.Selenium;
using OpenQA.Selenium.Safari;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

namespace HRngBackend
{
    public class SafariHelper : IBrowserHelper
    {
        /* Properties specified in the IBrowserHelper interface */
        public string BrowserPath { get; } = "/Applications/Safari.app/Contents/MacOS/Safari";
        public bool BrowserInst { get; } = true;
        public string DriverPath { get; } = "/usr/bin/safaridriver";
        public string TempFile { get; } = ""; // We don't need any temporary files

        /* Functions specified in the IBrowserHelper interface */

        [System.Runtime.Versioning.SupportedOSPlatform("osx")] // TODO: Is this the correct platform name for macOS?
        public string LocalVersion(string? path = null, int? idx = null)
        {
            /* Safari binaries require a bit more handling */
            if (!File.Exists(path)) return "";
            Process process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.FileName = "/usr/bin/mdls";
            process.StartInfo.Arguments = "-name kMDItemVersion -raw /Applications/Safari.app";
            process.Start();
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return output;
        }

        [System.Runtime.Versioning.SupportedOSPlatform("osx")]
        public string LocalDriverVersion(string? path = null)
        {
            /* SafariDriver binaries require a bit more handling */
            if (!File.Exists(path)) return "";
            Process process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.FileName = path ?? DriverPath;
            process.StartInfo.Arguments = "--version";
            process.Start();
            string[] output = process.StandardOutput.ReadToEnd().Trim().Split(' ');
            process.WaitForExit();
            return output.Last().Replace("(", "").Replace(")", "");
        }

        /// <summary>
        ///  Unused method for SafariHelper, do not use.
        /// </summary>
        /// <returns><c>null</c>.</returns>
        [System.Runtime.Versioning.SupportedOSPlatform("osx")]
        public async Task<Release> LatestRelease()
        {
            return null;
        }

        /// <summary>
        ///  Unused method for SafariHelper, do not use.
        /// </summary>
        /// <param name="version">Ignored.</param>
        /// <returns><c>null</c>.</returns>
        [System.Runtime.Versioning.SupportedOSPlatform("osx")]
        public async Task<Release> LatestDriverRelease(string version)
        {
            return null;
        }

        /// <summary>
        ///  Unused method for SafariHelper, do not use.
        /// </summary>
        /// <param name="consent">Ignored.</param>
        /// <param name="release">Ignored.</param>
        /// <param name="cb">Ignored.</param>
        /// <returns>0</returns>
        [System.Runtime.Versioning.SupportedOSPlatform("osx")]
        public async Task<int> Update(Func<Release, bool>? consent = null, Release? release = null, Func<float, bool>? cb = null)
        {
            return 0;
        }
        
        [System.Runtime.Versioning.SupportedOSPlatform("osx")]
        public IWebDriver InitializeSelenium(bool no_console = true, bool verbose = false, bool no_log = false, bool headless = true, bool no_img = true)
        {
            SafariDriverService driver = SafariDriverService.CreateDefaultService(Path.GetDirectoryName(DriverPath), Path.GetFileName(DriverPath));
            driver.HideCommandPromptWindow = no_console;
            // no_log, verbose and headless are currently not available with Safari
            var ret = new SafariDriver(driver);
            // There is a way to disable images loading, but since it also affects other Safari sessions, it will not be used here.
            return ret;
        }
    }
}
