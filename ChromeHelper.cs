/*
 * ChromeHelper.cs - Functions for updating and initializing Chromium for Selenium.
 * Created on: 10:42 02-12-2021
 * Author    : itsmevjnk
 */

using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using HRngBackend;

namespace HRngSelenium
{
    public class ChromeHelper : IBrowserHelper
    {
        /* Properties specified in the IBrowserHelper interface */
        public string BrowserPath { get; }
        public bool BrowserInst { get; } = true;
        public string DriverPath { get; }
        public string TempFile { get; } = Path.GetTempFileName();

        /// <summary>
        ///  Class constructor. This locates existing Chrome/Chromium installations.
        /// </summary>
        /// <param name="detect">Whether to detect existing Chrome/Chromium installations (optional). Enabled by default.</param>
        public ChromeHelper(bool detect = true)
        {
            DriverPath = Path.Combine(BaseDir.PlatformBase, "chromedriver");

            /* Attempt to find existing Google Chrome installation */
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                /* Windows */
                DriverPath += ".exe";

                if (detect)
                {
                    /* Google Chrome */
                    BrowserPath = Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Google\Chrome\Application\chrome.exe");
                    if (File.Exists(BrowserPath)) return;
                    BrowserPath = Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe");
                    if (File.Exists(BrowserPath)) return;
                    BrowserPath = Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Google\Chrome\Application\chrome.exe");
                    if (File.Exists(BrowserPath)) return;

                    /* CocCoc */
                    BrowserPath = Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\CocCoc\Browser\Application\browser.exe");
                    if (File.Exists(BrowserPath)) return;
                    BrowserPath = Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\CocCoc\Browser\Application\browser.exe");
                    if (File.Exists(BrowserPath)) return;
                    BrowserPath = Environment.ExpandEnvironmentVariables(@"%LocalAppData%\CocCoc\Browser\Application\browser.exe");
                    if (File.Exists(BrowserPath)) return;

                    /* Chromium (Hibbiki/Marmaduke) */
                    BrowserPath = Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Chromium\Application\chrome.exe");
                    if (File.Exists(BrowserPath)) return;
                    BrowserPath = Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Chromium\Application\chrome.exe");
                    if (File.Exists(BrowserPath)) return;
                    BrowserPath = Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Chromium\Application\chrome.exe");
                    if (File.Exists(BrowserPath)) return;
                }

                /* Cannot find local installation */
                BrowserInst = false; BrowserPath = Path.Combine(BaseDir.PlatformBase, "chrome", "chrome.exe");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                /* Mac OS X/macOS */
                if (detect)
                {
                    /* Google Chrome */
                    BrowserPath = @"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
                    if (File.Exists(BrowserPath)) return;

                    /* CocCoc */
                    BrowserPath = @"/Applications/CocCoc.app/Contents/MacOS/CocCoc";
                    if (File.Exists(BrowserPath)) return;

                    /* Chromium */
                    BrowserPath = @"/Applications/Chromium.app/Contents/MacOS/Chromium";
                    if (File.Exists(BrowserPath)) return;
                }

                /* Cannot find local installation */
                string[] path_array = { BaseDir.PlatformBase, "chrome", "Google Chrome for Testing.app", "Contents", "MacOS", "Google Chrome for Testing" };
                BrowserInst = false; BrowserPath = Path.Combine(path_array);
            }
            else
            {
                /* Linux/FreeBSD (not sure if FreeBSD gets Chrome) */
                if (detect)
                {
                    System.Diagnostics.Process proc = new System.Diagnostics.Process();
                    proc.StartInfo.FileName = "whereis";
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.RedirectStandardOutput = true;
                    proc.StartInfo.CreateNoWindow = true;

                    /* Google Chrome */
                    proc.StartInfo.Arguments = "google-chrome";
                    proc.Start();
                    string[] p_out = proc.StandardOutput.ReadToEnd().Split(' ');
                    proc.WaitForExit();
                    if (p_out.Length > 1)
                    {
                        BrowserPath = p_out[1];
                        return;
                    }

                    /* Chromium */
                    proc.StartInfo.Arguments = "chromium";
                    proc.Start();
                    p_out = proc.StandardOutput.ReadToEnd().Split(' ');
                    proc.WaitForExit();
                    if (p_out.Length > 1)
                    {
                        BrowserPath = p_out[1];
                        return;
                    }
                }

                /* Cannot find local installation */
                BrowserInst = false; BrowserPath = Path.Combine(BaseDir.PlatformBase, "chrome", "chrome");
            }
        }

        /// <summary>
        ///  Class destructor. Used to delete the temporary file (as of now).
        /// </summary>
        ~ChromeHelper()
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

        /// <summary>
        ///  OSCombo to platform mapping for Chrome/ChromeDriver versions starting from 115.
        /// </summary>
        private Dictionary<string, string> Chrome115ComboMap = new Dictionary<string, string>
        {
            { "Windows.X86", "win32" },
            { "Windows.X64", "win64" },
            { "Linux.X64", "linux64" }, // There are probably no Chrome 115+ builds for x86 Linux
            { "OSX.X64", "mac-x64" }, // There are DEFINITELY no Chrome 115+ builds for x86 Mac OS X
            { "OSX.Arm64", "mac-arm64" }
        };

        public async Task<Release> LatestRelease()
        {
            try
            {
                Release release = new Release();
                if (!File.Exists(BrowserPath)) release.Update = 2; // Force update

                /* Retrieve latest stable Chrome version available for our OS */
                Dictionary<string, string> os_map = new Dictionary<string, string>
                {
                    { "Windows.X86", "Win32" },
                    { "Windows.X64", "Windows" },
                    { "Linux.X64", "Linux" }, // There are probably no Chrome 115+ builds for x86 Linux
                    { "OSX.X64", "Mac" }, // There are DEFINITELY no Chrome 115+ builds for x86 Mac OS X
                    { "OSX.Arm64", "Mac" }
                };
                var resp_latest_ver = await CommonHTTP.Client.GetAsync($"https://chromiumdash.appspot.com/fetch_releases?channel=Stable&num=1&platform={os_map[OSCombo.Combo]}");
                resp_latest_ver.EnsureSuccessStatusCode();
                dynamic latest_vers = JsonConvert.DeserializeObject(await resp_latest_ver.Content.ReadAsStringAsync());
                if (latest_vers.Count == 0) throw new NotSupportedException($"There are no Chrome versions available for the OS combo {OSCombo.Combo} ({os_map[OSCombo.Combo]})");
                release.Version = ((string)latest_vers[0].version).Trim();

                /* Retrieve download link. In practice, we can shortcut this by generating our own download URLs, but doing this is safer in case the host changes. */
                var resp_dash = await CommonHTTP.Client.GetAsync("https://googlechromelabs.github.io/chrome-for-testing/known-good-versions-with-downloads.json");
                resp_dash.EnsureSuccessStatusCode();
                dynamic versions = ((dynamic)JsonConvert.DeserializeObject(await resp_dash.Content.ReadAsStringAsync())).versions;
                for (int i = versions.Count - 1; i >= 0; i--) // Iterate backwards as the versions list are arranged in ascending order
                {
                    if (versions[i].version == release.Version)
                    {
                        /* We've found the version that we need */
                        foreach (dynamic download in versions[i].downloads.chrome)
                        {
                            if (download.platform == Chrome115ComboMap[OSCombo.Combo]) release.DownloadURL = download.url;
                        }
                    }
                }


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
                int minor = Versioning.GetVersion(version, 1);
                int build = Versioning.GetVersion(version, 2);

                /* Get ChromeDriver version */
                string cdver = "";
                string download_url = "";
                string changelog_url = "";
                if (major < 115)
                {
                    /* Traditional checking method(s) */

                    if (major < 42) throw new NotSupportedException($"No information on ChromeDriver version for Chrome {version}");
                    else if (major < 70)
                    {
                        /* TODO: Tidy up this mess */
                        if (major >= 68) cdver = "2.42";
                        else if (major >= 66) cdver = "2.40";
                        else if (major >= 64) cdver = "2.37";
                        else if (major >= 62) cdver = "2.35";
                        else if (major >= 60) cdver = "2.33";
                        else if (major >= 57) cdver = "2.28";
                        else if (major >= 54) cdver = "2.25";
                        else if (major >= 51) cdver = "2.22";
                        else if (major >= 44) cdver = "2.19";
                        else if (major >= 42) cdver = "2.15";
                    }
                    else
                    {
                        /* Traditional method - check via chromedriver.storage.googleapis.com */
                        var resp_ver = await CommonHTTP.Client.GetAsync($"https://chromedriver.storage.googleapis.com/LATEST_RELEASE_{major}.{minor}.{build}");
                        resp_ver.EnsureSuccessStatusCode();
                        cdver = (await resp_ver.Content.ReadAsStringAsync()).Trim();
                    }

                    Dictionary<string, string> combo_map = new Dictionary<string, string>
                    {
                        { "Windows.X86", "win32" },
                        { "Windows.X64", "win32" }, // Strangely, Windows x64 binary does not exist :/
                        { "Linux.X86", "linux32" },
                        { "Linux.X64", "linux64" },
                        { "OSX.X86", "mac32" },
                        { "OSX.X64", "mac64" },
                        { "OSX.Arm64", "mac64_m1" }
                    };

                    /* Set download and changelog URLs */
                    download_url = $"https://chromedriver.storage.googleapis.com/{cdver}/chromedriver_{combo_map[OSCombo.Combo]}.zip";
                    changelog_url = $"https://chromedriver.storage.googleapis.com/{cdver}/notes.txt";
                }
                else
                {
                    /* Check via Chrome for Testing dashboard */

                    /* Get the dashboard's JSON API endpoint */
                    var resp_dash = await CommonHTTP.Client.GetAsync("https://googlechromelabs.github.io/chrome-for-testing/known-good-versions-with-downloads.json"); // Starting from Chrome 115, ChromeDriver is only available for known-good builds
                    resp_dash.EnsureSuccessStatusCode();
                    dynamic versions = ((dynamic)JsonConvert.DeserializeObject(await resp_dash.Content.ReadAsStringAsync())).versions;

                    /* Get the latest (known good) Chrome/ChromeDriver release version */
                    var resp_ver = await CommonHTTP.Client.GetAsync($"https://googlechromelabs.github.io/chrome-for-testing/LATEST_RELEASE_{major}.{minor}.{build}");
                    resp_ver.EnsureSuccessStatusCode();
                    cdver = (await resp_ver.Content.ReadAsStringAsync()).Trim();

                    /* Get the build's entry in the dashboard if it exists and extract information from that */
                    foreach (dynamic entry in versions)
                    {
                        if (entry.version == cdver)
                        {
                            /* We've found the entry */
                            foreach (dynamic download in entry.downloads.chromedriver)
                            {
                                if (download.platform == Chrome115ComboMap[OSCombo.Combo]) download_url = download.url;
                            }
                            // Starting from Chrome 115, ChromeDriver no longer ships changelogs

                            break; // Nothing else to do
                        }
                    }

                    if (download_url == "") throw new KeyNotFoundException($"Cannot find information on Chrome/Chromium {version}"); // Cannot get download URL, so we'll throw an exception and exit here
                }

                /* Check if the request is successful, i.e. the file exists */
                var resp = await CommonHTTP.Client.GetAsync(download_url);
                resp.EnsureSuccessStatusCode();

                Release release = new Release();
                release.Version = cdver;
                release.DownloadURL = download_url;
                release.ChangelogURL = changelog_url;
                if (!File.Exists(DriverPath) || Versioning.CompareVersion(release.Version, LocalDriverVersion(), 2) != 0) release.Update = 2; // Force update if ChromeDriver does not exist or there's a version mismatch

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
                /* We can update Chromium */
                Release remote; // The Chromium version we aim to update to
                if (release != null)
                {
                    /* Forced release specified */
                    remote = release;
                    remote.Update = 2; // Force this version
                }
                else remote = await LatestRelease(); // Get latest Chromium version
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
                    /* Download Chromium to replace old release */
                    if (await remote.Download(TempFile, cb))
                    {
                        if (Directory.Exists(Path.Combine(BaseDir.PlatformBase, "chrome"))) Directory.Delete(Path.Combine(BaseDir.PlatformBase, "chrome"), true); // Delete old release
                        await SevenZip.Extract(TempFile, BaseDir.PlatformBase);
                        Directory.Move(Path.Combine(BaseDir.PlatformBase, $"chrome-{Chrome115ComboMap[OSCombo.Combo]}"), Path.Combine(BaseDir.PlatformBase, "chrome")); // Rename extracted folder
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
                    if (File.Exists(DriverPath)) File.Delete(DriverPath); // Delete old ChromeDriver
                    await SevenZip.Extract(TempFile, BaseDir.PlatformBase); // Extract temporary file

                    /* Starting from ChromeDriver 115, the binary is inside a folder, so we'll need to move the folder's contents to its parent too */
                    if (Versioning.GetMajVersion(driver.Version) >= 115)
                    {
                        string dir = Path.Combine(BaseDir.PlatformBase, $"chromedriver-{Chrome115ComboMap[OSCombo.Combo]}");
                        Directory.Move(Path.Combine(dir, Path.GetFileName(DriverPath)), DriverPath);
                        Directory.Move(Path.Combine(dir, "LICENSE.chromedriver"), Path.Combine(BaseDir.PlatformBase, "LICENSE.chromedriver")); // Probably needed too
                        Directory.Delete(dir, true);
                    }
                }
                else if (driver.Update == 2) return -1;
            }
            return 0;
        }

        public IWebDriver InitializeSelenium(bool no_console = true, bool verbose = false, bool no_log = false, bool headless = true, bool no_img = true)
        {
            ChromeDriverService driver = ChromeDriverService.CreateDefaultService(Path.GetDirectoryName(DriverPath), Path.GetFileName(DriverPath));
            driver.EnableVerboseLogging = verbose;
            driver.HideCommandPromptWindow = no_console;
            if (!no_log) driver.LogPath = Path.Combine(Path.GetDirectoryName(DriverPath), $"crdrv_{DateTime.Now.ToString("ddMMyyyyHHmmss")}.log");
            ChromeOptions browser = new ChromeOptions();
            browser.BinaryLocation = BrowserPath;
            if (headless) browser.AddArgument("--headless");
            browser.AddArguments("--disable-extensions --disable-dev-shm-usage --no-sandbox --window-size=800,600".Split(' ')); // Disable extensions, overcome limited resource problems, disable sandboxing, and set window size to 800x600 for screenshotting
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) browser.AddArgument("--disable-gpu"); // According to Google this is "temporary" for Windows back in 2017, but looks like we still need it in 2021 :/
            if (no_img)
            {
                browser.AddUserProfilePreference("profile.managed_default_content_settings", new Dictionary<string, object> { { "images", 2 } });
            }
            return new ChromeDriver(driver, browser);
        }
    }
}
