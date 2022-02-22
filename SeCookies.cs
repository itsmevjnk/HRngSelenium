/*
 * Cookies.cs - Functions for handling web cookies.
 * Created on: 11:39 22-02-2022
 * Author    : itsmevjnk
 */

using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.IO;

#nullable enable
namespace HRngSelenium
{
    public static class SeCookies
    {
        /// <summary>
        ///  Load a single cookie to a Selenium browser session.
        /// </summary>
        /// <param name="driver">The driver instance for the Selenium browser session.</param>
        /// <param name="key">The cookie's key.</param>
        /// <param name="value">The cookie's value.</param>
        /// <param name="url">URL to the domain for which the cookie will be stored for (optional). If specified, this function will load the web page before setting the cookie.</param>
        public static void LoadCookie(IWebDriver driver, string key, string value, string url = "")
        {
            if (url != "" && driver.Url != url) driver.Navigate().GoToUrl(url);
            driver.Manage().Cookies.AddCookie(new Cookie(key, value));
        }

        /// <summary>
        ///  Load a string =&gt; string dictionary containing cookies to a Selenium browser session.
        /// </summary>
        /// <param name="driver">The driver instance for the Selenium browser session.</param>
        /// <param name="cookies">The string =&gt; string dictionary containing the cookies.</param>
        /// <param name="url">
        ///  URL to the domain for which the cookies will be stored for (optional).<br/>
        ///  If specified, this function will load the web page before setting the cookies.
        /// </param>
        public static void LoadCookies(IWebDriver driver, IDictionary<string, string> cookies, string url = "")
        {
            if (url != "" && driver.Url != url) driver.Navigate().GoToUrl(url);
            foreach (var item in cookies) driver.Manage().Cookies.AddCookie(new Cookie(item.Key, item.Value));
        }

        /// <summary>
        ///  Clear all cookies for a domain in a Selenium browser session.
        /// </summary>
        /// <param name="driver">The driver instance for the Selenium browser session.</param>
        /// <param name="url">
        ///  URL to the domain of which the cookies will be deleted (optional).<br/>
        ///  If specified, this function will load the web page before clearing the cookies.
        /// </param>
        public static void ClearCookies(IWebDriver driver, string url = "")
        {
            if (url != "" && driver.Url != url) driver.Navigate().GoToUrl(url);
            driver.Manage().Cookies.DeleteAllCookies();
        }

        /// <summary>
        ///  Get all cookies associated with a domain in a Selenium browser session.
        /// </summary>
        /// <param name="driver">The driver instance for the Selenium browser session.</param>
        /// <param name="url">
        ///  The URL to retrieve cookies for (optional).<br/>
        ///  If specified, this function will load the page before getting its cookies.
        /// </param>
        /// <returns>A string =&gt; string dictionary containing the cookies.</returns>
        public static Dictionary<string, string> SaveCookies(IWebDriver driver, string url = "")
        {
            if (url != "" && driver.Url != url) driver.Navigate().GoToUrl(url);
            Dictionary<string, string> cookies = new Dictionary<string, string>();
            foreach (var cookie in driver.Manage().Cookies.AllCookies)
            {
                if (!cookies.ContainsKey(cookie.Name)) cookies.Add(cookie.Name, cookie.Value);
            }
            return cookies;
        }
    }
}
#nullable disable
