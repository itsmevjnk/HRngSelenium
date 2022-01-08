/*
 * Release.cs - Class for containing information on a browser/driver
 *              release.
 * Created on: 11:53 24/12/2021
 * Author    : itsmevjnk
 */

using System;
using System.Threading.Tasks;


namespace HRngBackend
{
    public class Release
    {
        /*
         * public string Version
         *   The release's version number.
         */
        public string Version = "";

        /*
         * public string DownloadURL
         *   The release's download URL.
         */
        public string DownloadURL = "";

        /*
         * public string ChangelogURL
         *   The release's changelog URL (optional).
         */
        public string ChangelogURL = "";

        /*
         * public uint Update
         *   Indicates whether the local browser/driver can be updated
         *   to the release. Possible values are:
         *    0 - not updatable (local version is the same or newer)
         *    1 - can be updated
         *    2 - must be updated (local version does not exist)
         */
        public uint Update = 0;

        /*
         * public async Task<bool> Download(string destination, [Func<float, bool> cb])
         *   Downloads the release.
         *   Input : destination: Path to the file to which the release
         *                        will be saved.
         *           cb         : The download callback function (optional).
         *                        For more details, refer to
         *                        CommonHTTP.Download() function description.
         *   Output: Output from CommonHTTP.Download().
         */
        public async Task<bool> Download(string destination, Func<float, bool>?cb = null)
        {
            return await CommonHTTP.DownloadFile(DownloadURL, destination, cb);
        }
    }
}
