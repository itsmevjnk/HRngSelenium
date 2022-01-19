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
    public class ChromeHelper : IBrowserHelper
    {
        /* Properties specified in the IBrowserHelper interface */
        public string BrowserPath { get; }
        public bool BrowserInst { get; } = true;
        public string DriverPath { get; }
        public string TempFile { get; } = Path.GetTempFileName();

        /*
         * public ChromeHelper([bool detect])
         *   Class constructor. This locates existing Chrome/Chromium
         *   installations.
         *   It is highly recommended that this class is constructed in a
         *   try-catch structure to catch any exceptions from attempting to
         *   create the base directory (e.g. IOException).
         *   Input : detect: Whether to detect existing Chrome/Chromium
         *                   installations (optional).
         *                   Enabled by default.
         *   Output: none.
         */
        public ChromeHelper(bool detect = true)
        {
            DriverPath = Path.Combine(BaseDir.PlatformBase, "chromedriver");

            CommonHTTP.Client.DefaultRequestHeaders.Add("User-Agent", UserAgent.Next()); // We have to do this so that GitHub is happy

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
                string[] path_array = { BaseDir.PlatformBase, "chrome", "Chromium.app", "Contents", "MacOS", "Chromium" };
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
                        if (((string)asset.name).EndsWith(".AppImage"))
                        {
                            release.DownloadURL = asset.browser_download_url;
                            break;
                        }
                    }
                }
                else throw new InvalidOperationException("Chromium is not available for this operating system");
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

                Dictionary<string, string> combo_map = new Dictionary<string, string>{
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
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        /* With Linux we'll be using AppImage which can actually extract itself */
                        string appimg_file = Path.Combine(BaseDir.PlatformBase, "chromium.AppImage");
                        if (await remote.Download(appimg_file, cb))
                        {
                            if (Directory.Exists(Path.Combine(BaseDir.PlatformBase, "chrome"))) Directory.Delete(Path.Combine(BaseDir.PlatformBase, "chrome"), true); // Delete old release
                            System.Diagnostics.Process process = new System.Diagnostics.Process();
                            process.StartInfo.FileName = "chmod";
                            process.StartInfo.Arguments = $"+x {appimg_file}";
                            process.StartInfo.UseShellExecute = false;
                            process.StartInfo.RedirectStandardOutput = true;
                            process.StartInfo.CreateNoWindow = true;
                            process.Start(); await process.StandardOutput.ReadToEndAsync(); process.WaitForExit();
                            process = new System.Diagnostics.Process();
                            process.StartInfo.FileName = appimg_file;
                            process.StartInfo.WorkingDirectory = BaseDir.PlatformBase;
                            process.StartInfo.Arguments = "--appimage-extract opt/google/chrome/*";
                            process.StartInfo.UseShellExecute = false;
                            process.StartInfo.RedirectStandardOutput = true;
                            process.StartInfo.CreateNoWindow = true;
                            process.Start(); await process.StandardOutput.ReadToEndAsync(); process.WaitForExit();
                            Directory.Move(Path.Combine(new string[] { BaseDir.PlatformBase, "squashfs-root", "opt", "google", "chrome" }), Path.Combine(BaseDir.PlatformBase, "chrome"));
                            Directory.Delete(Path.Combine(BaseDir.PlatformBase, "squashfs-root"), true);
                            File.Delete(appimg_file);
                        }
                        else
                        {
                            if (File.Exists(appimg_file)) File.Delete(appimg_file);
                            if (remote.Update == 2) return -1;
                        }
                    }
                    else
                    {
                        if (await remote.Download(TempFile, cb))
                        {
                            if (Directory.Exists(Path.Combine(BaseDir.PlatformBase, "chrome"))) Directory.Delete(Path.Combine(BaseDir.PlatformBase, "chrome"), true); // Delete old release
                            string[] files = new string[1]; // List of files to provide to 7za for extracting
                            await SevenZip.Extract(TempFile, Path.Combine(BaseDir.PlatformBase, "chrome"), files);
                            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            {
                                /* For Windows, the Chromium executable will be stored in Chrome-bin, and we need to pull that out */
                                Directory.Move(Path.Combine(BaseDir.PlatformBase, "chrome", "Chrome-bin"), Path.Combine(BaseDir.PlatformBase, "Chrome-bin")); // Move Chrome-bin directory out
                                Directory.Delete(Path.Combine(BaseDir.PlatformBase, "chrome"), true); // Remove old chrome directory
                                Directory.Move(Path.Combine(BaseDir.PlatformBase, "Chrome-bin"), Path.Combine(BaseDir.PlatformBase, "chrome")); // Rename Chrome-bin to chrome
                            }
                        }
                        else if (remote.Update == 2) return -1;
                    }
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
