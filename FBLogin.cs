/*
 * FBLogin.cs - Functions for logging into Facebook using credentials.
 * Created on: 19:00 19-01-2022
 * Author    : itsmevjnk
 */

using System;
using System.Collections.Generic;
using System.Threading;
using OpenQA.Selenium;

using HRngBackend;

namespace HRngSelenium
{
    public static class FBLogin
    {
        /// <summary>
        ///  Verify that a Facebook account has been successfully logged in in the Selenium session.
        /// </summary>
        /// <param name="driver">The Selenium session to be checked.</param>
        /// <returns><c>true</c> if there's a logged in account, or <c>false</c> otherwise.</returns>
        public static bool VerifyLogin(IWebDriver driver)
        {
            driver.Navigate().GoToUrl("https://m.facebook.com/home.php");

            try
            {
                driver.FindElement(By.XPath("//div[@id='mJewelNav']"));
                return true; // If the navigation bar exists, chances are we are currently logged in
            }
            catch (NoSuchElementException) { } // We only catch NoSuchElementExceptions, if we end up with any other exception something has gone very, very wrong

            return false;
        }

        /// <summary>
        ///  Log into a Facebook account for a Selenium session and optionally retrieve the resulting cookies.
        /// </summary>
        /// <param name="driver">The driver instance for the Selenium browser session.</param>
        /// <param name="email">The email address or phone number associated with the account.</param>
        /// <param name="password">The password associated with the account.</param>
        /// <param name="cookies">The string =&gt; string dictionary that will be used to store the resulting cookies (optional).</param>
        /// <returns>
        ///  0 on success, or any of these failure results:
        ///  <list type="bullet">
        ///   <item><description>-1: Deformed login form</description></item>
        ///   <item><description>-2: Wrong credentials</description></item>
        ///   <item><description>
        ///    -3: Two-factor authentication required.<br/>
        ///    LoginOTP is supposed to be called to enter the OTP or wait for login approval from another device.
        ///   </description></item>
        ///   <item><description>-4: Deformed checkpoint page (account is locked?)</description></item>
        ///   <item><description>-5: Facebook was already logged in</description></item>
        ///  </list>
        /// </returns>
        public static int Login(IWebDriver driver, string email, string password, IDictionary<string, string>? cookies = null)
        {
            if (VerifyLogin(driver))
            {
                if (cookies != null)
                {
                    /* Get cookies */
                    foreach (var cookie in driver.Manage().Cookies.AllCookies)
                    {
                        if (!cookies.ContainsKey(cookie.Name)) cookies.Add(cookie.Name, cookie.Value);
                    }
                }
                return -5;
            }

            string url = driver.Url; // For waiting for URL change
            try
            {
                var elem = driver.FindElement(By.XPath("//input[@name='email']"));
                elem.Clear(); elem.SendKeys(email);
                elem = driver.FindElement(By.XPath("//input[@name='pass']"));
                elem.Clear(); elem.SendKeys(password);
            }
            catch (NoSuchElementException) { return -1; }
            driver.FindElement(By.XPath("//button[@name='login']")).Click();
            while (driver.Url == url)
            {
                /* Wait until either the URL changes or Facebook tells us we entered the wrong credentials */
                try
                {
                    if (driver.FindElement(By.XPath("//div[@id='login_error']")).GetAttribute("display") == "none") return -2;
                }
                catch (NoSuchElementException) { break; }
                catch (StaleElementReferenceException) { Thread.Sleep(10); } // Try again, this time with a small delay so we don't overload the driver and browser
            }
            if (driver.Url.StartsWith("https://m.facebook.com/login/checkpoint/") || driver.Url.StartsWith("https://m.facebook.com/checkpoint/"))
            {
                /* Getting checkpointed, can either mean 2FA or locked account */
                try
                {
                    driver.FindElement(By.XPath("//input[@name='approvals_code']"));
                    return -3; // OTP field exists so it's 2FA
                }
                catch (NoSuchElementException) { return -4; }
            }
            if (driver.Url.StartsWith("https://m.facebook.com/login/save-device/"))
            {
                /* Save device notification, we'll just click OK (TODO: is this even needed?) */
                url = driver.Url;
                driver.FindElement(By.XPath("//form[@action='/login/device-based/update-nonce/']//button")).Click();
                while (driver.Url == url) Thread.Sleep(10); // More waiting
            }

            /* Get cookies */
            CommonHTTP.ClearCookies("facebook.com");
            foreach (var cookie in driver.Manage().Cookies.AllCookies)
            {
                if (cookie.Domain.Contains("facebook.com"))
                {
                    CommonHTTP.AddCookie("facebook.com", cookie.Name, cookie.Value); // Load cookie to CommonHTTP
                    if (cookies != null && !cookies.ContainsKey(cookie.Name)) cookies.Add(cookie.Name, cookie.Value);
                }
            }

            return 0;
        }

        /// <summary>
        ///  Perform two-factor authentication on the account being logged in in a Selenium session.
        /// </summary>
        /// <param name="driver">The driver instance for the Selenium browser session.</param>
        /// <param name="otp">The time-based one time password provided by a TOTP generator such as Google Authenticator or Twilio Authy for the account (optional). If this is not specified, the function will wait until the user has given login approval from another device.</param>
        /// <param name="cookies">The string =&gt; string dictionary that will be used to store the resulting cookies (optional).</param>
        /// <param name="timeout">The timeout duration of waiting for login approval from another device (optional). If this is not specified, or a negative value or 0 was given, the function will wait indefinitely.</param>
        /// <returns>
        ///  0 on success, or any of these failure results:
        ///  <list type="bullet">
        ///   <item><description>-1: Already logged in successfully (no 2FA required)</description></item>
        ///   <item><description>-2: Deformed checkpoint page</description></item>
        ///   <item><description>-3: Waiting timed out</description></item>
        ///   <item><description>-4: Wrong OTP</description></item>
        ///  </list>
        /// </returns>
        public static int LoginOTP(IWebDriver driver, string? otp = null, IDictionary<string, string>? cookies = null, int timeout = -1)
        {
            if (!driver.Url.StartsWith("https://m.facebook.com/login/checkpoint/") && !driver.Url.StartsWith("https://m.facebook.com/checkpoint/")) driver.Navigate().GoToUrl("https://m.facebook.com/login/checkpoint/");
            if (driver.Url.StartsWith("https://m.facebook.com/home.php"))
            {
                if (cookies != null)
                {
                    /* Get cookies */
                    foreach (var cookie in driver.Manage().Cookies.AllCookies)
                    {
                        if (!cookies.ContainsKey(cookie.Name)) cookies.Add(cookie.Name, cookie.Value);
                    }
                }
                return -1;
            }

            if (otp == null)
            {
                /* Wait for login approval */
                DateTime dt_timeout = DateTime.Now.AddSeconds(timeout);
                bool approved = false;
                while (!approved && ((timeout <= 0) || (DateTime.Now <= dt_timeout)))
                {
                    try
                    {
                        driver.FindElement(By.XPath("//input[@name='approvals_code']"));
                    }
                    catch (NoSuchElementException) { approved = true; }
                }
                if (!approved) return -3;
            }
            else
            {
                /* Enter and submit OTP */
                while (driver.Url.StartsWith("https://m.facebook.com/login/checkpoint/") || driver.Url.StartsWith("https://m.facebook.com/checkpoint/"))
                {
                    try
                    {
                        var elem = driver.FindElement(By.XPath("//input[@name='approvals_code']"));
                        elem.Clear();
                        elem.SendKeys(otp);
                        driver.FindElement(By.XPath("//button[@id='checkpointSubmitButton-actual-button']")).Click();
                        break;
                    }
                    catch (StaleElementReferenceException) { continue; }
                    catch (NoSuchElementException) { break; }
                }

                /* Possible case where Facebook prompts whether to remember browser */
                if (driver.Url.StartsWith("https://m.facebook.com/login/checkpoint/") || driver.Url.StartsWith("https://m.facebook.com/checkpoint/"))
                {
                    try
                    {
                        driver.FindElement(By.XPath("//input[@name='approvals_code']")); // Check if we're still stuck at OTP prompt
                        return -4; // Wrong OTP if we are
                    }
                    catch (NoSuchElementException) { }
                    try
                    {
                        driver.FindElement(By.XPath("//button[@id='checkpointSubmitButton-actual-button']")).Click(); // If there's no submit button, chances are the page is deformed
                    }
                    catch (NoSuchElementException) { return -2; }
                }

                /* Never seen this before, but might as well add it here */
                if (driver.Url.StartsWith("https://m.facebook.com/login/save-device/"))
                {
                    /* Save device notification, we'll just click OK (TODO: is this even needed?) */
                    string url = driver.Url;
                    driver.FindElement(By.XPath("//form[@action='/login/device-based/update-nonce/']//button")).Click();
                    while (driver.Url == url) Thread.Sleep(10); // More waiting
                }
            }

            /* Get cookies */
            CommonHTTP.ClearCookies("facebook.com");
            foreach (var cookie in driver.Manage().Cookies.AllCookies)
            {
                if (cookie.Domain.Contains("facebook.com"))
                {
                    CommonHTTP.AddCookie("facebook.com", cookie.Name, cookie.Value); // Load cookie to CommonHTTP
                    if (cookies != null && !cookies.ContainsKey(cookie.Name)) cookies.Add(cookie.Name, cookie.Value);
                }
            }

            return 0;
        }

        /// <summary>
        ///  Get the user ID of the currently logged in user in a Selenium session.
        /// </summary>
        /// <param name="driver">The driver instance for the Selenium session. </param>
        /// <returns>The logged in user's ID, or -1 if the operation fails.</returns>
        public static long GetUID(IWebDriver driver)
        {
            foreach (var cookie in driver.Manage().Cookies.AllCookies)
            {
                if (cookie.Domain.Contains("facebook.com") && cookie.Name == "c_user") return Convert.ToInt64(cookie.Value);
            }
            return -1;
        }

        /// <summary>
        ///  Get the user ID of the currently logged in user in a Selenium session.
        /// </summary>
        /// <param name="cookies">A string =&gt; string dictionary of cookies for Facebook.</param>
        /// <returns>The logged in user's ID, or -1 if the operation fails.</returns>
        public static long GetUID(IDictionary<string, string> cookies)
        {
            if (cookies.ContainsKey("c_user")) return Convert.ToInt64(cookies["c_user"]);
            else return -1;
        }
    }
}
