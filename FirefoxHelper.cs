/*
 * FirefoxHelper.cs - Functions for updating and initializing Mozilla Firefox for Selenium.
 * Created on: 18:42 17-01-2022
 * Author    : itsmevjnk
 */

using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace HRngBackend
{
    public class FirefoxHelper : IBrowserHelper
    {
        /* Properties specified in the IBrowserHelper interface */
        public string BrowserPath { get; }
        public bool BrowserInst { get; } = true;
        public string DriverPath { get; }
        public string TempFile { get; } = Path.GetTempFileName();

        /*
         * public FirefoxHelper([bool detect])
         *   Class constructor. This locates existing Firefox
         *   installations.
         *   It is highly recommended that this class is constructed in a
         *   try-catch structure to catch any exceptions from attempting to
         *   create the base directory (e.g. IOException).
         *   Input : detect: Whether to detect existing Firefox installations
         *                   (optional).
         *                   Enabled by default.
         *   Output: none.
         */
        public FirefoxHelper(bool detect = true)
        {
            DriverPath = Path.Combine(BaseDir.PlatformBase, "geckodriver");

            CommonHTTP.Client.DefaultRequestHeaders.Add("User-Agent", UserAgent.Next()); // We have to do this so that GitHub is happy

            /* Attempt to find existing Google Chrome installation */
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                /* Windows */
                DriverPath += ".exe";

                if (detect)
                {
                    BrowserPath = Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Mozilla Firefox\firefox.exe");
                    if (File.Exists(BrowserPath)) return;
                    BrowserPath = Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Mozilla Firefox\firefox.exe");
                    if (File.Exists(BrowserPath)) return;
                    BrowserPath = Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Mozilla Firefox\firefox.exe");
                    if (File.Exists(BrowserPath)) return;
                }

                /* Cannot find local installation */
                BrowserInst = false; BrowserPath = Path.Combine(BaseDir.PlatformBase, "firefox", "firefox.exe");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                /* Mac OS X/macOS */
                if (detect)
                {
                    BrowserPath = @"/Applications/Firefox.app/Contents/MacOS/firefox";
                    if (File.Exists(BrowserPath)) return;
                }

                /* Cannot find local installation */
                string[] path_array = { BaseDir.PlatformBase, "firefox", "Firefox.app", "Contents", "MacOS", "firefox" };
                BrowserInst = false; BrowserPath = Path.Combine(path_array);
            }
            else
            {
                /* Linux/FreeBSD (not sure if FreeBSD gets Firefox) */
                if (detect)
                {
                    System.Diagnostics.Process proc = new System.Diagnostics.Process();
                    proc.StartInfo.FileName = "whereis";
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.RedirectStandardOutput = true;
                    proc.StartInfo.CreateNoWindow = true;

                    proc.StartInfo.Arguments = "firefox";
                    proc.Start();
                    string[] p_out = proc.StandardOutput.ReadToEnd().Split(' ');
                    proc.WaitForExit();
                    if (p_out.Length > 1)
                    {
                        BrowserPath = p_out[1];
                        return;
                    }
                }

                /* Cannot find local installation */
                BrowserInst = false; BrowserPath = Path.Combine(BaseDir.PlatformBase, "firefox", "firefox");
            }
        }

        /*
         * ~FirefoxHelper()
         *   Class destructor. Used to delete the temporary file (as of now).
         *   Input : none.
         *   Output: none.
         */
        ~FirefoxHelper()
        {
            File.Delete(TempFile);
        }

        /* Functions specified in the IBrowserHelper interface */

        public string LocalVersion(string? path = null, int? idx = null)
        {
            return Versioning.ExecVersion(path ?? BrowserPath, idx); // Use the common implementation
        }

        public string LocalDriverVersion(string? path = null)
        {
            return Versioning.ExecVersion(path ?? DriverPath, 1);
        }

        public async Task<Release> LatestRelease()
        {
            try
            {
                Release release = new Release();
                if (!File.Exists(BrowserPath)) release.Update = 2; // Force update
                /* Construct latest ESR download link */
                string url = "https://download.mozilla.org/?product=firefox-esr-latest&lang=en-US&os=";
                switch (RuntimeInformation.OSArchitecture)
                {
                    case Architecture.X86:
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) url += "win";
                        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) url += "linux";
                        break;
                    case Architecture.X64:
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) url += "win64";
                        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) url += "linux64";
                        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) url += "osx";
                        break;
                    case Architecture.Arm64:
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) url += "win64-aarch64"; // Undocumented
                        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) url += "osx";
                        break;
                }
                if (url.EndsWith("&os=")) throw new Exception("Mozilla Firefox is not available for this OS/architecture");
                var resp = await CommonHTTP.Client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                resp.EnsureSuccessStatusCode();
                var resp_uri = resp.RequestMessage.RequestUri; // Get the redirected URI
                release.Version = resp_uri.Segments[4].Replace("esr/", "");
                release.DownloadURL = resp_uri.ToString();
                release.ChangelogURL = $"https://www.mozilla.org/en-US/firefox/{release.Version}/releasenotes/";
                if (release.Update != 2 && !BrowserInst && Versioning.CompareVersion(release.Version, LocalVersion()) > 0) release.Update = 1;
                return release;
            }
            catch
            {
                return null;
            }
        }

        public async Task<Release> LatestDriverRelease(string version)
        {
            try
            {
                int major = Versioning.GetVersion(version, 0);

                /* Get list of GeckoDriver versions and each version's compatibility */
                var resp = await CommonHTTP.Client.GetAsync("https://firefox-source-docs.mozilla.org/_sources/testing/geckodriver/Support.md.txt");
                resp.EnsureSuccessStatusCode();
                string[] support_md = (await resp.Content.ReadAsStringAsync()).Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                List<(string version, int minver, int maxver)> gd_versions = new List<(string version, int minver, int maxver)>();
                for (int line = 0; line < support_md.Length; line++) // TODO: Optimize
                {
                    if (support_md[line] == " <tr>")
                    {
                        /* Version list item */
                        gd_versions.Add((support_md[line + 1].Replace("  <td>", ""),
                                         Convert.ToInt32(support_md[line + 3].Replace("  <td>", "").Split(' ')[0]),
                                         ((support_md[line + 4] == "  <td>n/a") ? -1 : Convert.ToInt32(support_md[line + 4].Replace("  <td>", "")))));
                    }
                }

                /* Get GeckoDriver version */
                string gdver = "";
                foreach (var ver in gd_versions)
                {
                    if (!(ver.minver > major || (ver.maxver != -1 && ver.maxver < major)))
                    {
                        gdver = ver.version;
                        break;
                    }
                }
                if (gdver == "") throw new Exception($"No suitable GeckoDriver version found for Firefox {major}");

                Dictionary<string, string> combo_map = new Dictionary<string, string> {
                    { "Windows.X86", "win32.zip" },
                    { "Windows.X64", "win64.zip" },
                    { "Linux.X86", "linux32.tar.gz" },
                    { "Linux.X64", "linux64.tar.gz" },
                    { "OSX.X64", "macos.tar.gz" },
                    { "OSX.Arm64", "macos-aarch64.tar.gz" }
                };

                /* Check if the request is successful, i.e. the file exists */
                resp = await CommonHTTP.Client.GetAsync($"https://github.com/mozilla/geckodriver/releases/download/v{gdver}/geckodriver-v{gdver}-{combo_map[OSCombo.Combo]}");
                resp.EnsureSuccessStatusCode();

                Release release = new Release();
                release.Version = gdver;
                release.DownloadURL = $"https://github.com/mozilla/geckodriver/releases/download/v{gdver}/geckodriver-v{gdver}-{combo_map[OSCombo.Combo]}";
                release.ChangelogURL = $"https://github.com/mozilla/geckodriver/releases/tag/v{gdver}";
                if (!File.Exists(DriverPath)) release.Update = 2; // Force update if GeckoDriver does not exist
                else
                {
                    string localver = LocalDriverVersion();
                    /* Local version possibly does not support current Firefox version */
                    foreach (var ver in gd_versions)
                    {
                        if (ver.version == localver)
                        {
                            if (ver.minver > major || (ver.maxver != -1 && ver.maxver < major)) release.Update = 2;
                            return release;
                        }
                    }
                    /* Check if remote version is newer */
                    if (Versioning.CompareVersion(release.Version, localver) > 0) release.Update = 1;
                }
                return release;
            }
            catch
            {
                return null;
            }
        }

        public async Task<int> Update(Func<Release, bool>? consent = null, Release? release = null, Func<float, bool>? cb = null)
        {
            if (!BrowserInst)
            {
                /* We can update Firefox */
                Release remote; // The Firefox version we aim to update to
                if (release != null)
                {
                    /* Forced release specified */
                    remote = release;
                    remote.Update = 2; // Force this version
                }
                else remote = await LatestRelease(); // Get latest Firefox version
                if (remote.Update != 0)
                {
                    if (consent != null)
                    {
                        bool consent_ret = consent(remote); // Prompt for consent
                        if (!consent_ret)
                        {
                            if (remote.Update == 2) return -1; // Forced update refused
                            else goto UpdateDriver;
                        }
                    }
                    /* Download Firefox to replace old release */
                    if (await remote.Download(TempFile, cb))
                    {
                        if (Directory.Exists(Path.Combine(BaseDir.PlatformBase, "firefox"))) Directory.Delete(Path.Combine(BaseDir.PlatformBase, "firefox"), true); // Delete old release
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            /* For Windows, Firefox is distributed as a 7-Zip extractable EXE file with binaries in core/ */
                            await SevenZip.Extract(TempFile, BaseDir.PlatformBase, new string[] { "core" });
                            Directory.Move(Path.Combine(BaseDir.PlatformBase, "core"), Path.Combine(BaseDir.PlatformBase, "firefox"));
                        }
                        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        {
                            /* For macOS, Firefox is distributed as a DMG image, but fortunately that can also be extracted with 7-Zip */
                            await SevenZip.Extract(TempFile, BaseDir.PlatformBase, new string[] { "Firefox/Firefox.app" });
                            Directory.Move(Path.Combine(BaseDir.PlatformBase, "Firefox"), Path.Combine(BaseDir.PlatformBase, "firefox")); // Safety measure, in case the FS is case sensitive
                        }
                        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        {
                            /* For Linux, Firefox is distributed as a tar.bz2 archive, which can be extracted using 7-Zip (although very inefficiently) */
                            await SevenZip.Extract(TempFile, BaseDir.PlatformBase); // This will create a file whose name is the temporary file name without the .tmp extension
                            string tar_path = Path.Combine(BaseDir.PlatformBase, Path.GetFileNameWithoutExtension(TempFile)); // Path of the aforementioned file
                            await SevenZip.Extract(tar_path, BaseDir.PlatformBase, new string[] { "firefox" });
                            File.Delete(tar_path);
                        }
                    }
                    else if (remote.Update == 2) return -1;
                }
            }

        UpdateDriver:
            Release driver = await LatestDriverRelease(LocalVersion());
            if (driver.Update != 0)
            {
                if (await driver.Download(TempFile, cb))
                {
                    if (File.Exists(DriverPath)) File.Delete(DriverPath); // Delete old GeckoDriver
                    await SevenZip.Extract(TempFile, BaseDir.PlatformBase); // Extract temporary file
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        /* On all OSes except Windows, GeckoDriver is stored in a tar.gz file, and therefore needs another round of extracting */
                        string tar_path = Path.Combine(BaseDir.PlatformBase, Path.GetFileNameWithoutExtension(TempFile));
                        await SevenZip.Extract(tar_path, BaseDir.PlatformBase);
                        File.Delete(tar_path);
                    }
                }
                else if (driver.Update == 2) return -1;
            }
            return 0;
        }

        public IWebDriver InitializeSelenium(bool no_console = true, bool verbose = false, bool no_log = false, bool headless = true, bool no_img = true)
        {
            FirefoxDriverService driver = FirefoxDriverService.CreateDefaultService(Path.GetDirectoryName(DriverPath), Path.GetFileName(DriverPath));
            driver.HideCommandPromptWindow = no_console;
            driver.FirefoxBinaryPath = BrowserPath;
            FirefoxOptions browser = new FirefoxOptions();
            browser.BrowserExecutableLocation = BrowserPath;
            if (verbose) browser.LogLevel = 0; // Set log level to TRACE
            if (headless) browser.AddArgument("-headless");
            var ret = new FirefoxDriver(driver, browser, Timeout.InfiniteTimeSpan);
            if (no_img) ret.InstallAddOn(Convert.ToBase64String(Properties.Resources.FFImageBlocker)); // Undocumented function (why?), but this is the only way that this will work. Plus, this will not require the add on to be copied to a file, which is a win ;)
            return ret;
        }
    }
}
