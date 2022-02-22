/*
 * Release.cs - Class for containing information on a browser/driver
 *              release.
 * Created on: 11:53 24/12/2021
 * Author    : itsmevjnk
 */

using System;
using System.Threading.Tasks;

using HRngBackend;

namespace HRngBackend
{
    public class Release
    {
        /// <summary>
        ///  The release's version number.
        /// </summary>
        public string Version = "";

        /// <summary>
        ///  The release's download URL.
        /// </summary>
        public string DownloadURL = "";

        /// <summary>
        ///  The release's changelog URL (optional).
        /// </summary>
        public string ChangelogURL = "";

        /// <summary>
        ///  Indicates whether the local browser/driver can be updated to the release. Possible values are:
        /// <list type="bullet">
        ///  <item><description>0: not updatable (local version is the same or newer)</description></item>
        ///  <item><description>1: can be updated</description></item>
        ///  <item><description>2: must be updated (local version does not exist)</description></item>
        /// </list>
        /// </summary>
        public uint Update = 0;

        /// <summary>
        ///  Downloads the release.
        /// </summary>
        /// <param name="destination">Path to the file to which the release will be saved.</param>
        /// <param name="cb">
        ///  The download callback function (optional).<br/>
        ///  For more details, refer to <c>CommonHTTP.Download()</c> function description.
        /// </param>
        /// <returns>Output from <c>CommonHTTP.Download()</c>.</returns>
        public async Task<bool> Download(string destination, Func<float, bool>?cb = null)
        {
            return await CommonHTTP.DownloadFile(DownloadURL, destination, cb);
        }
    }
}
