/*
 * IBrowserHelper.cs - Browser Selenium initialization helper interface
 *                     for abstraction.
 * Created on: 17:34 18-01-2022
 * Author    : itsmevjnk
 */

using System;
using System.Threading.Tasks;
using OpenQA.Selenium;

namespace HRngBackend
{
    public interface IBrowserHelper
    {
        /*
         * public string BrowserPath
         *   Path to the browser executable to be used by HRng.
         *   The helper class is supposed to detect local installations
         *   of the browser unless specified in the constructor (e.g.
         *   via a detect=false argument as seen in ChromeHelper),
         *   and if none can be found, it should resort to using the
         *   browser binaries stored in <PlatformBase>.
         */
        public string BrowserPath { get; }

        /*
         * public bool BrowserInst
         *   Set if the browser executable used by HRng is installed on
         *   the machine and not downloaded by HRng.
         */
        public bool BrowserInst { get; }

        /*
         * public string DriverPath
         *   Path to the browser driver executable to be used by HRng.
         *   The driver executable for the locally installed browser is
         *   supposed to be downloaded and extracted by the helper to
         *   <PlatformBase>.
         */
        public string DriverPath { get; }

        /*
         * public string TempFile
         *   Path to the temporary file used by the helper.
         *   This temporary file is supposed to be deleted upon instance
         *   destruction.
         */
        public string TempFile { get; }

        /*
         * public string LocalVersion([string path], [int idx])
         *   Get the version of the browser that is locally installed.
         *   With the optional [path] argument, this function can also be used
         *   for any browser executable.
         *   Input : path: Path to the browser executable (optional).
         *           idx : The space-split substring index containing the
         *                 version number (optional). For example, for
         *                   <BrowserDriver> 10.0.xxxx.yy (...)
         *                 the index would be 1.
         *   Output: String containing the browser version, or an empty string
         *           if the function fails.
         */
        public string LocalVersion(string? path = null, int? idx = null);

        /*
         * public string LocalDriverVersion([string path])
         *   Get the version of the local browser driver.
         *   With the optional [path] argument, this function can also be used
         *   for any driver executable.
         *   Input : path: Path to the driver executable (optional).
         *   Output: String containing the driver version, or an empty string
         *           if the function fails.
         */
        public string LocalDriverVersion(string? path = null);

        /*
         * public Task<Release> LatestRelease()
         *   Retrieves the latest (stable/LTS) browser release available for
         *   the running platform. This function is asynchronous.
         *   Input : none.
         *   Output: The latest stable/LTS browser release available.
         */
        public Task<Release> LatestRelease();

        /*
         * public Task<Release> LatestDriverRelease(string version)
         *   Retrieves the latest driver release available for the specified
         *   browser version. This function is asynchronous.
         *   Input : version: The browser version string.
         *   Output: A Release class containing the driver release.
         */
        public Task<Release> LatestDriverRelease(string version);

        /*
         * public Task<int> Update([Func<Release, bool> consent], [Release release],
         *                         [Func<float, bool> cb])
         *   Checks for new release and updates the browser and its driver.
         *   The browser update portion will not be run if the library is
         *   using a local browser installation (in which case updating is the
         *   responsibility of the user). This function is asynchronous.
         *   Input : consent: Function for asking the user for consent to update
         *                    the browser or driver (optional).
         *                    This function takes a Release instance containing
         *                    information on the release that will be updated to,
         *                    and returns true if the user allows the browser to
         *                    be updated, or false otherwise.
         *           release: Browser release to force up/downgrade to (optional).
         *                    If this is not specified, this function will update
         *                    to the latest version.
         *                    Please note that in the case of using local browser
         *                    installations, this argument will be
         *                    ignored.
         *           cb     : The callback function used during downloads (optional).
         *                    For details, refer to the CommonHTTP.Download()
         *                    function description.
         *   Output: -1 if the user refuses to perform a forced update (i.e.
         *           there's no browser found), or 0 on success.
         */
        public Task<int> Update(Func<Release, bool>? consent = null, Release? release = null, Func<float, bool>? cb = null);

        /*
         * public IWebDriver InitializeSelenium([bool no_console], [bool verbose], [bool no_log], [bool headless], [bool no_img])
         *   Initializes Selenium using the browser and driver in BrowserPath
         *   and DriverPath.
         *   Input : no_console: Whether to disable showing the console window
         *                       for the driver (optional). Set to true by
         *                       default.
         *           verbose   : Whether to enable verbose logging (optional).
         *                       Set to false by default.
         *           no_log    : Whether to disable saving the driver's logs
         *                       to files (optional). Set to false by default.
         *           headless  : Whether to start the browser in headless
         *                       mode (i.e. no GUI) (optional). Set to true by
         *                       default.
         *           no_img    : Whether to disable images loading. Set to true
         *                       by default. Please note that enabling images
         *                       loading will result in higher unnecessary data
         *                       usage and longer loading time.
         *   Output: An IWebDriver instance from Selenium.
         */
        public IWebDriver InitializeSelenium(bool no_console = true, bool verbose = false, bool no_log = false, bool headless = true, bool no_img = true);
    }
}

