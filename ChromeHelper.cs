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

namespace HRngBackend
{
    public class ChromeHelper
    {
        /*
         * public string ChromePath
         *   Path to the Google Chrome/Chromium executable to be used
         *   by HRng.
         *   This library tries to detect local installations of Google
         *   Chrome and Chromium, and if none can be found, it will resort
         *   to using Chromium stored in <PlatformBase>.
         */
        public string ChromePath;

        /*
         * public bool ChromeInst
         *   Set if the Chrome executable used by HRng is installed on the
         *   machine and not downloaded by HRng.
         */
        public bool ChromeInst = true;

        /*
         * public string ChromeDriverPath
         *   Path to the ChromeDriver executable to be used by HRng.
         *   The ChromeDriver executable for the locally installed Chrome version
         *   will be downloaded and extracted by this library to
         *   <PlatformBase>.
         */
        public string ChromeDriverPath;

        /*
         * public string TempFile
         *   Path to the temporary file used by this class.
         *   This temporary file will be deleted upon instance destruction.
         */
        public string TempFile = Path.GetTempFileName();

        /*
         * public ChromeHelper()
         *   Class constructor. This locates existing Chrome/Chromium
         *   installations.
         *   It is highly recommended that this class is constructed in a
         *   try-catch structure to catch any exceptions from attempting to
         *   create the base directory (e.g. IOException).
         *   Input : none.
         *   Output: none.
         */
        public ChromeHelper()
        {
            ChromeDriverPath = Path.Combine(BaseDir.PlatformBase, "chromedriver");

            CommonHTTP.Client.DefaultRequestHeaders.Add("User-Agent", UserAgent.Next()); // We have to do this so that GitHub is happy

            /* Attempt to find existing Google Chrome installation */
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                /* Windows */
                ChromeDriverPath += ".exe";

                /* Google Chrome */
                ChromePath = Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Google\Chrome\Application\chrome.exe");
                if (File.Exists(ChromePath)) return;
                ChromePath = Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe");
                if (File.Exists(ChromePath)) return;
                ChromePath = Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Google\Chrome\Application\chrome.exe");
                if (File.Exists(ChromePath)) return;

                /* CocCoc */
                ChromePath = Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\CocCoc\Browser\Application\browser.exe");
                if (File.Exists(ChromePath)) return;
                ChromePath = Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\CocCoc\Browser\Application\browser.exe");
                if (File.Exists(ChromePath)) return;
                ChromePath = Environment.ExpandEnvironmentVariables(@"%LocalAppData%\CocCoc\Browser\Application\browser.exe");
                if (File.Exists(ChromePath)) return;

                /* Chromium (Hibbiki/Marmaduke) */
                ChromePath = Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Chromium\Application\chrome.exe");
                if (File.Exists(ChromePath)) return;
                ChromePath = Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Chromium\Application\chrome.exe");
                if (File.Exists(ChromePath)) return;
                ChromePath = Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Chromium\Application\chrome.exe");
                if (File.Exists(ChromePath)) return;

                /* Cannot find local installation */
                ChromeInst = false; ChromePath = Path.Combine(BaseDir.PlatformBase, "chrome", "chrome.exe");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                /* Mac OS X/macOS */
                /* Google Chrome */
                ChromePath = @"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
                if (File.Exists(ChromePath)) return;

                /* CocCoc */
                ChromePath = @"/Applications/CocCoc.app/Contents/MacOS/CocCoc";
                if (File.Exists(ChromePath)) return;

                /* Chromium */
                ChromePath = @"/Applications/Chromium.app/Contents/MacOS/Chromium";
                if (File.Exists(ChromePath)) return;

                /* Cannot find local installation */
                string[] path_array = { BaseDir.PlatformBase, "chrome", "Chromium.app", "Contents", "MacOS", "Chromium" };
                ChromeInst = false; ChromePath = Path.Combine(path_array);
            }
            else
            {
                /* Linux/FreeBSD (not sure if FreeBSD gets Chrome) */
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
                    ChromePath = p_out[1];
                    return;
                }

                /* Chromium */
                proc.StartInfo.Arguments = "chromium";
                proc.Start();
                p_out = proc.StandardOutput.ReadToEnd().Split(' ');
                proc.WaitForExit();
                if (p_out.Length > 1)
                {
                    ChromePath = p_out[1];
                    return;
                }

                /* Cannot find local installation */
                ChromeInst = false; ChromePath = Path.Combine(BaseDir.PlatformBase, "chrome", "chrome");
            }
        }

        /*
         * ~ChromeHelper()
         *   Class destructor. Used to delete the temporary file (as of now).
         *   Input : none.
         *   Output: none.
         */
        ~ChromeHelper()
        {
            File.Delete(TempFile);
        }

        /*
         * public string LocalVersion([string path], [int idx])
         *   Get the version of Chrome that is locally installed.
         *   With the optional [path] argument, this function can also be used
         *   for any Chrome executable.
         *   Input : path: Path to the Chrome executable (optional).
         *           idx : The space-split substring index containing the
         *                 version number (optional). For example, for
         *                   ChromeDriver 94.0.xxxx.yy (...)
         *                 the index would be 1.
         *   Output: String containing the Chrome version, or an empty string
         *           if the function fails.
         */
        public string LocalVersion(string? path = null, int? idx = null)
        {
            return Versioning.ExecVersion(path ?? ChromePath, idx); // Use the common implementation
        }

        /*
         * public string LocalDriverVersion([string path])
         *   Get the version of ChromeDriver.
         *   With the optional [path] argument, this function can also be used
         *   for any ChromeDriver executable.
         *   Input : path: Path to the ChromeDriver executable (optional).
         *   Output: String containing the ChromeDriver version, or an empty
         *           string if the function fails.
         */
        public string LocalDriverVersion(string? path = null)
        {
            return Versioning.ExecVersion(path ?? ChromeDriverPath, 1);
        }

        /*
         * public async Task<Release> LatestRelease()
         *   Retrieves the latest (stable) release of Chromium available for
         *   the running platform.
         *   Input : none.
         *   Output: The latest Chromium release available.
         */
        public async Task<Release> LatestRelease()
        {
            try
            {
                Release release = new Release();
                if (!File.Exists(ChromePath)) release.Update = 2; // Force update
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string repo = ""; // GitHub repo for Chromium releases for this platform
                    switch (RuntimeInformation.OSArchitecture)
                    {
                        case Architecture.X64: repo = "Hibbiki/chromium-win64"; break;
                        case Architecture.X86: repo = "Hibbiki/chromium-win32"; break;
                        /* TODO: Add ARM64 */
                        default: throw new InvalidOperationException($"Chromium for Windows is not available for this platform ({Convert.ToString(RuntimeInformation.OSArchitecture)})");
                    }
                    var resp = await CommonHTTP.Client.GetAsync($"https://api.github.com/repos/{repo}/releases/latest");
                    resp.EnsureSuccessStatusCode();
                    dynamic release_json = JsonConvert.DeserializeObject(await resp.Content.ReadAsStringAsync());
                    string tag = ((string)release_json.html_url).Split('/').Last();
                    release.Version = Regex.Replace(tag.Replace("v", ""), "-r.*", "");
                    release.DownloadURL = $"https://github.com/{repo}/releases/download/{tag}/chrome.nosync.7z";
                    release.ChangelogURL = release_json.html_url;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    if (RuntimeInformation.OSArchitecture != Architecture.X64) throw new InvalidOperationException($"Chromium for macOS is not available for this platform ({Convert.ToString(RuntimeInformation.OSArchitecture)})"); // Only x86_64 builds exist as of now
                    var resp = await CommonHTTP.Client.GetAsync($"https://api.github.com/repos/macchrome/macstable/releases/latest");
                    resp.EnsureSuccessStatusCode();
                    dynamic release_json = JsonConvert.DeserializeObject(await resp.Content.ReadAsStringAsync());
                    string tag = ((string)release_json.html_url).Split('/').Last();
                    release.Version = Regex.Replace(tag.Replace("v", ""), "-r.*", "");
                    release.DownloadURL = $"https://github.com/macchrome/macstable/releases/download/{tag}/Chromium.app.ungoogled-{release.Version}.zip";
                    release.ChangelogURL = release_json.html_url;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    if (RuntimeInformation.OSArchitecture != Architecture.X64) throw new InvalidOperationException($"Chromium for Linux is not available for this platform ({Convert.ToString(RuntimeInformation.OSArchitecture)})"); // Only x86_64 builds exist as of now
                    var resp = await CommonHTTP.Client.GetAsync($"https://api.github.com/repos/macchrome/linchrome/releases/latest");
                    resp.EnsureSuccessStatusCode();
                    dynamic release_json = JsonConvert.DeserializeObject(await resp.Content.ReadAsStringAsync());
                    string tag = ((string)release_json.html_url).Split('/').Last();
                    release.Version = Regex.Replace(tag.Replace("v", ""), "-r.*", "");
                    release.ChangelogURL = release_json.html_url;
                    foreach (var asset in release_json.assets)
                    {
                        if (((string)asset.name).EndsWith(".tar.xz"))
                        {
                            release.DownloadURL = asset.browser_download_url;
                            break;
                        }
                    }
                }
                else throw new InvalidOperationException("Chromium is not available for this operating system");
                if (release.Update != 2 && !ChromeInst && Versioning.CompareVersion(release.Version, LocalVersion()) > 0) release.Update = 1;
                return release;
            }
            catch
            {
                return null;
            }
        }

        /*
         * public async Task<Release> LatestDriverRelease(string version)
         *   Retrieves the latest release of ChromeDriver available for the
         *   specified Chrome/Chromium version.
         *   Input : version: The Chrome/Chromium version string. This string
         *                    must contain at least <major>.<minor>.<build>.
         *   Output: A Release class containing the ChromeDriver release.
         */
        public async Task<Release> LatestDriverRelease(string version)
        {
            try
            {
                int major = Versioning.GetVersion(version, 0);
                int minor = Versioning.GetVersion(version, 1);
                int build = Versioning.GetVersion(version, 2);

                /* Get ChromeDriver version */
                string cdver = "";
                if (major < 42) throw new Exception($"No information on ChromeDriver version for Chrome {version}");
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
                    var resp_ver = await CommonHTTP.Client.GetAsync($"https://chromedriver.storage.googleapis.com/LATEST_RELEASE_{major}.{minor}.{build}");
                    resp_ver.EnsureSuccessStatusCode();
                    cdver = (await resp_ver.Content.ReadAsStringAsync()).Trim();
                }

                IDictionary<string, string> combo_map = new Dictionary<string, string>{
                { "Windows.X86", "win32" },
                { "Windows.X64", "win32" }, // Strangely, Windows x64 binary does not exist :/
                { "Linux.X86", "linux32" },
                { "Linux.X64", "linux64" },
                { "OSX.X86", "mac32" },
                { "OSX.X64", "mac64" },
                { "OSX.Arm64", "mac64_m1" }
            };

                /* Check if the request is successful, i.e. the file exists */
                var resp = await CommonHTTP.Client.GetAsync($"https://chromedriver.storage.googleapis.com/{cdver}/chromedriver_{combo_map[OSCombo.Combo]}.zip");
                resp.EnsureSuccessStatusCode();

                Release release = new Release();
                release.Version = cdver;
                release.DownloadURL = $"https://chromedriver.storage.googleapis.com/{cdver}/chromedriver_{combo_map[OSCombo.Combo]}.zip";
                release.ChangelogURL = $"https://chromedriver.storage.googleapis.com/{cdver}/notes.txt";
                if (!File.Exists(ChromeDriverPath) || Versioning.CompareVersion(release.Version, LocalDriverVersion(), 2) != 0) release.Update = 2; // Force update if ChromeDriver does not exist or there's a version mismatch

                return release;
            }
            catch
            {
                return null;
            }
        }

        /*
         * public async Task DownloadRelease(Release release, [string dir])
         *   Downloads and extracts a release to the specified (optional)
         *   directory. If the directory is not specified, the release will
         *   be extracted to PlatformBase instead.
         *   Input : release: The release to be downloaded.
         *           dir    : The directory to which the release is extracted
         *                    (optional)
         *   Output: none.
         */
        public async Task DownloadRelease(Release release, string dir)
        {
            await release.Download(TempFile);
            await SevenZip.Extract(TempFile, dir);
        }
        public async Task DownloadRelease(Release release)
        {
            await DownloadRelease(release, BaseDir.PlatformBase);
        }

        /*
         * public async Task<int> Update([Func<Release, bool> consent], [Release release])
         *   Checks for new release and updates Chromium and ChromeDriver.
         *   The Chromium update portion will not be run if the library is
         *   using a local Chrome/Chromium installation (in which case
         *   updating is the responsibility of the user).
         *   The consent function will not be called for ChromeDriver updating,
         *   due to the nature of Chromium requiring a matching ChromeDriver
         *   binary to work.
         *   Input : consent: Function for asking the user for consent to update
         *                    Chrome/Chromium (optional).
         *                    This function takes a Release instance containing
         *                    information on the release that will be updated to,
         *                    and returns true if the user allows the browser to
         *                    be updated, or false otherwise.
         *           release: Chromium release to force up/downgrade to (optional).
         *                    If this is not specified, this function will update
         *                    to the latest version.
         *                    Please note that in the case of using local 
         *                    Chrome/Chromium installations, this argument will be
         *                    ignored.
         *   Output: -1 if the user refuses to perform a forced update (i.e.
         *           there's no browser found), or 0 on success.
         */
        public async Task<int> Update(Func<Release, bool>? consent = null, Release? release = null)
        {
            if (!ChromeInst)
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
                if (remote.Update != 0 && consent != null)
                {
                    bool consent_ret = consent(remote); // Prompt for consent
                    if (!consent_ret)
                    {
                        if (remote.Update == 2) return -1; // Forced update refused
                        else goto UpdateDriver;
                    }
                    /* Download Chromium to replace old release */
                    if (Directory.Exists(Path.Combine(BaseDir.PlatformBase, "chrome"))) Directory.Delete(Path.Combine(BaseDir.PlatformBase, "chrome"), true); // Delete old release
                    await remote.Download(TempFile); // We'll implement our own download-and-extract code since there are many ways the contents inside the archive is contained
                    string[] files = new string[1]; // List of files to provide to 7za for extracting
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) files[0] = "Chrome-bin/*"; // Hibbiki puts their Chromium binaries in Chrome-bin/
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) files[0] = remote.DownloadURL.Split("/").Last().Replace(".tar.xz", "") + "/*"; // Marmaduke puts their Linux Chromium binaries in a directory with the same name as the file
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) files[0] = ""; // Surprisingly, there's nothing much to Marmaduke's macOS Chromium builds
                    await SevenZip.Extract(TempFile, Path.Combine(BaseDir.PlatformBase, "chrome"), files, atomic: RuntimeInformation.IsOSPlatform(OSPlatform.Linux));
                }
            }

        UpdateDriver:
            Release driver = await LatestDriverRelease(LocalVersion());
            if (driver.Update != 0)
            {
                if (File.Exists(ChromeDriverPath)) File.Delete(ChromeDriverPath); // Delete old ChromeDriver
                await DownloadRelease(driver); // Download and extract new one
            }
            return 0;
        }

        /*
         * public IWebDriver InitializeSelenium([bool no_console], [bool verbose], [bool no_log], [bool headless], [bool no_img])
         *   Initializes Selenium using the Chrome/Chromium and ChromeDriver
         *   binaries in ChromePath and ChromeDriverPath.
         *   Input : no_console: Whether to disable showing the console window
         *                       for ChromeDriver (optional). Set to true by
         *                       default.
         *           verbose   : Whether to enable verbose logging (optional).
         *                       Set to false by default.
         *           no_log    : Whether to disable saving ChromeDriver's logs
         *                       to files (optional). Set to false by default.
         *                       ChromeDriver logs can be found in PlatformBase
         *                       as crdrv_(timestamp).log.
         *           headless  : Whether to start Chrome/Chromium in headless
         *                       mode (i.e. no GUI). Set to true by default.
         *           no_img    : Whether to disable images loading. Set to true
         *                       by default. Please note that enabling images
         *                       loading will result in higher unnecessary data
         *                       usage and longer loading time.
         *   Output: An IWebDriver instance from Selenium.
         */
        public IWebDriver InitializeSelenium(bool no_console = true, bool verbose = false, bool no_log = false, bool headless = true, bool no_img = true)
        {
            ChromeDriverService driver = ChromeDriverService.CreateDefaultService(Path.GetDirectoryName(ChromeDriverPath), Path.GetFileName(ChromeDriverPath));
            driver.EnableVerboseLogging = verbose;
            driver.HideCommandPromptWindow = no_console;
            if (!no_log) driver.LogPath = Path.Combine(Path.GetDirectoryName(ChromeDriverPath), $"crdrv_{DateTime.Now.ToString("ddMMyyyyHHmmss")}.log");
            ChromeOptions browser = new ChromeOptions();
            browser.BinaryLocation = ChromePath;
            if (headless)
            {
                browser.AddArgument("--headless");
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) browser.AddArgument("--disable-gpu"); // According to Google this is "temporary" for Windows back in 2017, but looks like we still need it in 2021 :/
            }
            if (no_img)
            {
                browser.AddUserProfilePreference("profile.managed_default_content_settings", new Dictionary<string, object> { { "images", 2 } });
            }
            return new ChromeDriver(driver, browser);
        }
    }
}
