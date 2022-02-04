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
        /// <summary>
        ///  Path to the browser executable to be used by HRng.<br/>
        ///  The helper class is supposed to detect local installations of the browser unless specified in the constructor (e.g. via a detect=false argument as seen in ChromeHelper), and if none can be found, it should resort to using the browser binaries stored in PlatformBase.
        /// </summary>
        public string BrowserPath { get; }

        /// <summary>
        ///  Set if the browser executable used by HRng is installed on the machine and not downloaded by HRng.
        /// </summary>
        public bool BrowserInst { get; }

        /// <summary>
        ///  Path to the browser driver executable to be used by HRng.<br/>
        ///  The driver executable for the locally installed browser is supposed to be downloaded and extracted by the helper to PlatformBase.
        /// </summary>
        public string DriverPath { get; }

        /// <summary>
        ///  Path to the temporary file used by the helper for downloading operations.<br/>
        ///  This temporary file is supposed to be deleted upon instance destruction.
        /// </summary>
        public string TempFile { get; }

        /// <summary>
        ///  Get the version of the browser that is locally installed.<br/>
        ///  With the optional <c>path</c> argument, this function can also be used for any browser executable.
        /// </summary>
        /// <param name="path">Path to the browser executable (optional).</param>
        /// <param name="idx">
        ///  The space-split substring index containing the version number (optional).<br/>
        ///  For example, for &lt;BrowserDriver&gt; 10.0.xxxx.yy (...) the index would be 1.
        /// </param>
        /// <returns>String containing the browser version, or an empty string if the function fails.</returns>
        public string LocalVersion(string? path = null, int? idx = null);

        /// <summary>
        ///  Get the version of the local browser driver.<br/>
        ///  With the optional <c>path</c> argument, this function can also be used for any driver executable.
        /// </summary>
        /// <param name="path">Path to the driver executable (optional).</param>
        /// <returns>String containing the driver version, or an empty string if the function fails.</returns>
        public string LocalDriverVersion(string? path = null);

        /// <summary>
        ///  Retrieves the latest (stable/LTS) browser release available for the running platform.<br/>
        ///  This function is asynchronous.
        /// </summary>
        /// <returns>The latest stable/LTS browser release available.</returns>
        public Task<Release> LatestRelease();

        /// <summary>
        ///  Retrieves the latest driver release available for the specified browser version.<br/>
        ///  This function is asynchronous.
        /// </summary>
        /// <param name="version">The browser version string.</param>
        /// <returns>A Release class containing the driver release.</returns>
        public Task<Release> LatestDriverRelease(string version);

        /// <summary>
        ///  Checks for new release and updates the browser and its driver. The browser update portion will not be run if the library is using a local browser installation (in which case updating is the responsibility of the user).<br/>
        ///  This function is asynchronous.
        /// </summary>
        /// <param name="consent">
        ///  Function for asking the user for consent to update the browser or driver (optional).<br/>
        ///  This function takes a <c>Release</c> instance containing information on the release that will be updated to, and returns <c>true</c> if the user allows the browser to be updated, or <c>false</c> otherwise.
        /// </param>
        /// <param name="release">
        ///  Browser release to force up/downgrade to (optional). If this is not specified, this function will update to the latest version.<br/>
        ///  Please note that in the case of using local browser installations, this argument will be ignored.
        /// </param>
        /// <param name="cb">
        ///  The callback function used during downloads (optional).<br/>
        ///  For details, refer to the CommonHTTP.Download() function description.
        /// </param>
        /// <returns>-1 if the user refuses to perform a forced update (i.e. there's no browser found), or 0 on success.</returns>
        public Task<int> Update(Func<Release, bool>? consent = null, Release? release = null, Func<float, bool>? cb = null);

        /// <summary>
        ///  Initializes Selenium using the browser and driver in BrowserPath and DriverPath.
        /// </summary>
        /// <param name="no_console">Whether to disable showing the console window for the driver (optional). Set to <c>true</c> by default.</param>
        /// <param name="verbose">Whether to enable verbose logging (optional). Set to <c>false</c> by default.</param>
        /// <param name="no_log">Whether to disable saving the driver's logs to files (optional). Set to <c>false</c> by default.</param>
        /// <param name="headless">Whether to start the browser in headless mode (i.e. no GUI) (optional). Set to <c>true</c> by default.</param>
        /// <param name="no_img">
        ///  Whether to disable images loading. Set to <c>true</c> by default.<br/>
        ///  Please note that enabling images loading will result in higher unnecessary data usage and longer loading time.
        /// </param>
        /// <returns>An <c>IWebDriver</c> instance from Selenium.</returns>
        public IWebDriver InitializeSelenium(bool no_console = true, bool verbose = false, bool no_log = false, bool headless = true, bool no_img = true);
    }
}

