﻿/*
 * FBPost.cs - Functions for gathering information on Facebook posts.
 *             If any of HRng's main function fails, this file is
 *             the first place to check, as Facebook seems to change
 *             how their mobile (m.facebook.com) website work randomly,
 *             and it is the maintainer(s)' job to adapt this code
 *             to such changes.
 * Created on: 23:04 28-12-2021
 * Author    : itsmevjnk
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web;
using System.Linq;

using OpenQA.Selenium;
using HtmlAgilityPack;
using Newtonsoft.Json;

using HRngBackend;

namespace HRngSelenium
{
    public class FBPost : IFBPost
    {
        /* Properties specified by the IFBPost interface */
        public long PostID { get; internal set; } = -1;
        public long AuthorID { get; internal set; } = -1;
        public bool IsGroupPost { get; internal set; } = false;

        /// <summary>
        ///  The Selenium WebDriver instance associated with this post.<br/>
        ///  Do NOT share drivers across multiple posts concurrently; Selenium WebDriver instances aren't by any means thread-safe (even a single state change requires all nodes to be re-fetched).
        /// </summary>
        private IWebDriver Driver = null;

        /// <summary>
        ///  Class constructor. Sets the WebDriver instance.
        /// </summary>
        public FBPost(IWebDriver driver)
        {
            Driver = driver;
        }

        /// <summary>
        ///  Drop-in replacement for UID.Get() which adds handling code for cases where the account's URL is profile.php (which points to the account being checked).
        /// </summary>
        /// <returns>Same as <c>UID.Get()</c>.</returns>
        private async Task<long> GetUID(string url)
        {
            if (url.Contains("profile.php") && !url.Contains("id=")) return FBLogin.GetUID(Driver); // The case we're looking for
            else return await UID.Get(url); // Attempt to get UID using UID.Get() as normal
        }

        /// <summary>
        ///  Helper function to click an element and wait for the the AJAX request to finish. Useful for loading more items.
        /// </summary>
        /// <param name="click_xpath">XPath pointing to the element to be clicked.</param>
        /// <param name="check_xpath">XPath pointing to the element that is present during the loading process (optional). By default, it's set to watch for any elements with the <c>async_elem_saving</c> class, which is good enough in most cases.</param>
        /// <param name="delay">The interval in milliseconds to wait for after the checking is complete to let the new elements load. Defaults to 250ms.</param>
        /// <param name="elem">The root element to perform on (optional). If not specified, the operation will be done on the root element.</param>
        /// <returns>-1 if there's no element to click, or 0 if the function succeeds.</returns>
        private int ClickAndWait(string click_xpath, string check_xpath = "//*[contains(@class, 'async_elem_saving')]", int delay = 250, IWebElement elem = null)
        {
            while (true)
            {
                try
                {
                    if (elem == null) Driver.FindElement(By.XPath(click_xpath)).Click();
                    else elem.FindElement(By.XPath(click_xpath)).Click();
                    break;
                }
                catch (NoSuchElementException) { return -1; }
                catch (StaleElementReferenceException) { }
                catch (ElementClickInterceptedException) { } // Just try again basically
            }
            while (true)
            {
                try
                {
                    if (elem == null) Driver.FindElement(By.XPath(check_xpath));
                    else elem.FindElement(By.XPath(check_xpath));
                }
                catch (NoSuchElementException) { break; }
                catch (StaleElementReferenceException) { }
            }
            Thread.Sleep(delay); // Wait for new content to be loaded
            return 0;
        }

        /* Functions specified by the IFBPost interface */

        public async Task<int> Initialize(long id)
        {
            return await Initialize($"https://m.facebook.com/{id}");
        }

        public async Task<int> Initialize(string url)
        {
            if (url.Length == 0) return -1; // Return right away

            /* Change the domain to m.facebook.com */
            if(!url.StartsWith("https://m.facebook.com"))
            {
                url = Regex.Replace(url, "^.*://", ""); // Remove the schema (aka http(s)://) from the URL
                url = Regex.Replace(url, "^[^/]*", "https://m.facebook.com"); // Perform the replacement
            }

            /* Request webpage to attempt to get post ID */
            Driver.Navigate().GoToUrl(url);

            /* Detect group post */
            Uri driver_uri = new Uri(Driver.Url);
            List<string> uri_segments = new List<string>();
            foreach (string seg in driver_uri.Segments)
            {
                if (!seg.StartsWith('?') && seg != "/") uri_segments.Add(seg.Replace("/", ""));
            }
            if (uri_segments.Contains("groups")) IsGroupPost = true; // Group post detected

            /* Get author ID */
            AuthorID = -1; // Just in case this object was re-initialized with another post
            /* Attempt to get directly from URL */
            if (IsGroupPost && uri_segments[uri_segments.IndexOf("permalink") - 1].All(char.IsDigit)) AuthorID = Convert.ToInt64(uri_segments[uri_segments.IndexOf("permalink") - 1]);
            else
            {
                var url_params = HttpUtility.ParseQueryString(driver_uri.Query);
                if (url_params.Get("id") != null) AuthorID = Convert.ToInt64(url_params.Get("id"));
            }
            /* Attempt to get from post container (not working with images) */
            if (AuthorID < 0)
            {
                try
                {
                    dynamic data_ft = JsonConvert.DeserializeObject(Driver.FindElement(By.XPath("//div[contains(@data-ft, 'content_owner_id_new')]")).GetAttribute("data-ft"));
                    if (data_ft != null) AuthorID = Convert.ToInt64(data_ft.content_owner_id_new);
                }
                catch (NoSuchElementException) { }
            }
            /* Attempt to get from share button (verified working with images, and probably isn't needed anyway) */
            if (AuthorID < 0)
            {
                try
                {
                    dynamic data_store = JsonConvert.DeserializeObject(Driver.FindElement(By.XPath("//a[contains(@data-store, 'shareable_uri')]")).GetAttribute("data-store"));
                    if (data_store != null)
                    {
                        var url_params = HttpUtility.ParseQueryString(((string)data_store.shareable_uri).Replace("/", ""));
                        if (url_params.Get("id") != null) AuthorID = Convert.ToInt64(url_params.Get("id"));
                    }
                }
                catch (NoSuchElementException) { }
            }
            /* Attempt to get from actor link (verified working with images) */
            if (AuthorID < 0)
            {
                try
                {
                    AuthorID = await GetUID(Driver.FindElement(By.XPath("//a[@data-sigil='actor-link']")).GetAttribute("href"));
                }
                catch (NoSuchElementException) { }

            }
            if (AuthorID < 0) return -2;

            /* Get post ID */
            PostID = -1;
            /* Attempt to get from post container (not working with images) */
            if (PostID < 0)
            {
                try
                {
                    dynamic data_ft = JsonConvert.DeserializeObject(Driver.FindElement(By.XPath("//div[contains(@data-ft, 'top_level_post_id')]")).GetAttribute("data-ft"));
                    if (data_ft != null) PostID = Convert.ToInt64(data_ft.top_level_post_id);
                }
                catch (NoSuchElementException) { }
            }
            /* Thanks to Facebook's new encrypted pfbid system, there are no other ways of retrieving the post ID (yet) */
            if (PostID < 0) return -2;

            return 0;
        }

        public async Task<Dictionary<long, FBComment>> GetComments(Func<float, bool>? cb = null, bool muid = true, bool p1 = true, bool p2 = false)
        {
            Dictionary<long, FBComment> comments = new Dictionary<long, FBComment>();

            int pass = (p1) ? 1 : 2; // We'll do pass 1 first
            float npass = ((p1) ? 1 : 0) + ((p2) ? 1 : 0); // Number of passes to do

            for (int pn = 0; pn < (int)npass; pn++, pass++) {
                /* Get post page */
                Driver.Navigate().GoToUrl($"https://m.facebook.com/story.php?story_fbid={PostID}&id={AuthorID}");

                Dictionary<string, string> cookies = null;
                if (pass == 2) {
                    /* Back up cookies, then log out by deleting all cookies */
                    cookies = SeCookies.SaveCookies(Driver);
                    SeCookies.ClearCookies(Driver);
                    Driver.Navigate().Refresh(); // Refresh so we end up being logged out

                    /* Remove login popup */
                    ((IJavaScriptExecutor)Driver).ExecuteScript("return document.querySelector('[data-sigil=loggedout_mobile_cta_footer]').parentElement.remove();");
                }

                var elem_post_s = Driver.FindElement(By.XPath("//div[@data-sigil='m-story-view']")); // Get post container element (Selenium)

                /* Load all top-level comments */
                int cnt = 0, cnt_prev = 0; // Current and previous comments count
                do
                {
                    try
                    {
                        cnt_prev = cnt;
                        if (ClickAndWait(".//div[starts-with(@id, 'see_next') or starts-with(@id, 'see_prev')]", elem: elem_post_s) == -1) break;
                        for (int i = 0; i < 5 && cnt_prev == cnt; i++)
                        {
                            Thread.Sleep(200); // Poll for changes 5 times, each time every 200ms (1s in total)
                            cnt = elem_post_s.FindElements(By.XPath(".//div[@data-sigil='comment inline-reply' or @data-sigil='comment']")).Count;
                        }
                    }
                    catch (NoSuchElementException) { break; }
                    catch (StaleElementReferenceException) { continue; }
                } while (cnt != cnt_prev);

                /* Load all replies */
                while (ClickAndWait("//div[starts-with(@data-sigil, 'replies-see')]", elem: elem_post_s) != -1) ;

                /* Selenium seems to be pretty slow when it comes to finding elements, so we'll use HtmlAgilityPack instead */
                HtmlDocument htmldoc = new HtmlDocument();
                htmldoc.LoadHtml(Driver.PageSource);

                /* Read each comment */
                try
                {
                    var comment_elems = htmldoc.DocumentNode.SelectSingleNode("//div[@data-sigil='m-story-view']").SelectNodes(".//div[@data-sigil='comment inline-reply' or @data-sigil='comment']");
                    int n = 0;
                    if (cb != null & cb(0) == false) return null;
                    var ids = comments.Keys; // List of comment IDs from previous pass (so we can skip comments that we've fetched)
                    foreach (var elem in comment_elems)
                    {
                        long id = Convert.ToInt64(elem.Attributes["id"].DeEntitizeValue); // Comment ID
                        if (!ids.Contains(id))
                        {
                            if (!comments.ContainsKey(id))
                            {
                                FBComment comment = new FBComment();
                                comment.ID = id;
                                var elem_profile_pict = elem.SelectSingleNode("./div[contains(@data-sigil, 'feed_story_ring')]");
                                comment.AuthorID = Convert.ToInt64(elem_profile_pict.Attributes["data-sigil"].DeEntitizeValue.Replace("feed_story_ring", ""));
                                var elem_comment = elem_profile_pict.SelectSingleNode("./following-sibling::div[1]");
                                var elem_author = elem_comment.SelectSingleNode(".//div[@class='_2b05']"); // TODO: Find a better way to do this (i.e. without using classes)
                                if (elem_author.SelectSingleNode("./a") != null && elem_author.SelectSingleNode("./a").Attributes["href"] != null) UID.Add(elem_author.SelectSingleNode("./a").Attributes["href"].DeEntitizeValue, comment.AuthorID);
                                comment.AuthorName = elem_author.InnerText; // TODO: Remove the Author text on top of the name
                                var elem_body = elem_comment.SelectSingleNode("./div[1]//div[@data-sigil='comment-body']");
                                if (elem_body != null)
                                {
                                    comment.CommentText = HttpUtility.HtmlDecode(elem_body.InnerText);
                                    comment.CommentText_HTML = elem_body.InnerHtml;
                                }
                                if (comment.CommentText != "")
                                {
                                    int placeholder_cnt = -10;
                                    var elem_mentions = elem_body.SelectNodes("./a");
                                    if (elem_mentions != null)
                                    {
                                        foreach (var elem_mention in elem_mentions)
                                        {
                                            if (elem_mention.Attributes["href"] == null)
                                            {
                                                comment.Mentions_Handle.Add($"{elem_mention.InnerText} ({placeholder_cnt})");
                                                if (muid) comment.Mentions_UID.Add(placeholder_cnt);
                                                placeholder_cnt--;
                                            }
                                            else
                                            {
                                                string url = elem_mention.Attributes["href"].DeEntitizeValue;
                                                if (url.StartsWith("/") && !url.Contains(elem_mention.InnerText) && UID.GetHandle(url) != "")
                                                {
                                                    comment.Mentions_Handle.Add(UID.GetHandle(url));
                                                    if (muid) comment.Mentions_UID.Add(await GetUID(url));
                                                }
                                            }
                                        }
                                    }
                                }
                                if (elem_comment.SelectNodes("./div").Count == 4)
                                {
                                    /* Embedded content */
                                    var elem_embed = elem_comment.SelectSingleNode("./div[2]");
                                    if (elem_embed.SelectSingleNode("./i[contains(@style, 'background-image')]") != null)
                                    {
                                        try { comment.StickerURL = Driver.FindElement(By.XPath($"//div[@data-sigil='comment inline-reply' or @data-sigil='comment'][{n + 1}]/div[contains(@data-sigil, 'feed_story_ring')]/following-sibling::div[1]/div[2]/i[contains(@style, 'background-image')]")).GetCssValue("background-image").Replace("url(", "").Replace(")", "").Replace("\"", "").Replace("'", ""); }
                                        catch (NoSuchElementException) { }
                                    }
                                    var elem_embed2 = elem_embed;
                                    if (!elem_embed2.Attributes.Contains("title")) elem_embed2 = elem_embed.SelectSingleNode("./div[@title]");
                                    if (elem_embed2 != null && elem_embed2.Attributes.Contains("title"))
                                    {
                                        comment.EmbedTitle = elem_embed2.Attributes["title"].DeEntitizeValue;
                                        comment.EmbedURL = elem_embed2.SelectSingleNode("./a").Attributes["href"].DeEntitizeValue;
                                        if (comment.EmbedURL.StartsWith('/')) comment.EmbedURL = "https://m.facebook.com" + comment.EmbedURL;
                                    }
                                    var elem_attach = elem_embed.SelectSingleNode("./div[contains(@class, 'attachment')]/*");
                                    if (elem_attach != null)
                                    {
                                        if (elem_attach.Name == "a" && elem_attach.Attributes.Contains("href") && (elem_attach.Attributes["href"].DeEntitizeValue.Contains("photo.php") || elem_attach.Attributes["href"].DeEntitizeValue.Contains("/photos/"))) comment.ImageURL = "https://m.facebook.com" + elem_attach.Attributes["href"].DeEntitizeValue;
                                        if (elem_attach.Name == "div" && elem_attach.Attributes.Contains("data-store") && elem_attach.Attributes["data-store"].DeEntitizeValue.Contains("videoURL"))
                                        {
                                            dynamic data_store = JsonConvert.DeserializeObject(elem_attach.Attributes["data-store"].DeEntitizeValue);
                                            if (data_store != null) comment.VideoURL = data_store.videoURL;
                                        }
                                    }
                                }
                                comments.Add(id, comment);
                            }
                            FBComment cmt = comments[id];
                            if (cmt.Parent == -1 && elem.Attributes["data-sigil"].DeEntitizeValue.Contains("inline-reply"))
                            {
                                /* This comment is a reply, now find its parent */
                                var elem_parent = elem.SelectSingleNode("./ancestor::div[@data-sigil='comment']");
                                cmt.Parent = Convert.ToInt64(elem_parent.Attributes["id"].DeEntitizeValue);
                            }
                        }

                        n++;
                        if (cb != null && cb((100f / npass) * ((float)n / (float)comment_elems.Count + pn)) == false) return null;
                    }
                }
                catch(NoSuchElementException) { }
                
                if (cookies != null) SeCookies.LoadCookies(Driver, cookies, "https://m.facebook.com"); // Log back in
            }
            return comments;
        }

        public async Task<Dictionary<long, FBReact>> GetReactions(Func<float, bool>? cb = null)
        {
            Dictionary<long, FBReact> reactions = new Dictionary<long, FBReact>();

            /* Load reactions page */
            Driver.Navigate().GoToUrl($"https://m.facebook.com/ufi/reaction/profile/browser/?ft_ent_identifier={PostID}");

            /* As it turns out, Facebook conveniently provides us with a perfectly ordered list of shown users' IDs in the AJAX URL, so we can use that to speedrun the UID retrieval process */
            List<long> shown_users = new List<long>(); // Where we'll save the IDs
            string prev_shown = ""; // Facebook stacks the new page's shown users before the previous pages' shown users, so we'll have to save the previous shown users list to filter out
            /* Load all reactions */
            while (true)
            {
                try
                {
                    string shown = HttpUtility.ParseQueryString(Driver.FindElement(By.XPath("//div[@id='reaction_profile_pager']/a")).GetAttribute("data-ajaxify-href").Split('/').Last()).Get("shown_ids");
                    if (prev_shown.Length > 0) shown = shown.Replace(prev_shown, "");
                    foreach (string uid in shown.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        long uid_long = Convert.ToInt64(uid);
                        if (!shown_users.Contains(uid_long)) shown_users.Add(uid_long);
                    }
                    prev_shown = "," + shown + ((prev_shown.Length > 0) ? "," : "") + prev_shown;
                    ClickAndWait("//div[@id='reaction_profile_pager']");
                }
                catch (NoSuchElementException) { break; }
                catch (StaleElementReferenceException) { continue; }
            }

            /* Go through each reaction */
            HtmlDocument htmldoc = new HtmlDocument();
            htmldoc.LoadHtml(Driver.PageSource); // The Selenium session will be used for getting UIDs, and so we'll externally parse the reactions
            var react_elems = htmldoc.DocumentNode.SelectNodes("//div[@id='reaction_profile_browser']/div");
            if(react_elems != null)
            {
                int n = 0;
                if (cb != null & cb(0) == false) return null;
                foreach (var elem in react_elems)
                {
                    FBReact reaction = new FBReact();

                    /* Get the UID */
                    string link = elem.SelectSingleNode("./div[1]/div[1]//i[contains(@class, 'profpic')]/..").GetAttributeValue("href", "");
                    long uid = -1;
                    /* From shown user ID list */
                    if (n < shown_users.Count) uid = shown_users[n];
                    /* From message button */
                    if (uid == -1)
                    {
                        try
                        {
                            string current_url = Driver.Url; // Just being on the safe side here
                            Driver.FindElement(By.XPath($"//div[@id='reaction_profile_browser']/div[{n + 1}]//div[@data-sigil='send-message-with-attachment']/button")).Click();
                            while (Driver.Url == current_url) Thread.Sleep(10); // Wait until browser URL changes (which is when we can begin to do our magic)
                            uid = Convert.ToInt64(HttpUtility.ParseQueryString(Regex.Replace(HttpUtility.ParseQueryString(Driver.Url.Split('/').Last()).Get("mds"), "(?<=[&?]ids).*(?==)", "")).Get("ids"));
                            Driver.Navigate().Back(); // Go back to previous page
                        }
                        catch (NoSuchElementException) { }
                    }
                    /* From add friend button */
                    if (uid == -1)
                    {
                        var elem_data_store = elem.SelectSingleNode(".//a[contains(@data-store, 'id')]");
                        if (elem_data_store != null)
                        {
                            dynamic data_store = JsonConvert.DeserializeObject(elem_data_store.Attributes["data-store"].DeEntitizeValue);
                            uid = data_store.id;
                        }
                    }
                    /* From follow button */
                    if (uid == -1)
                    {
                        var elem_data_store = elem.SelectSingleNode(".//div[contains(@data-store, 'subject_id')]");
                        if (elem_data_store != null)
                        {
                            dynamic data_store = JsonConvert.DeserializeObject(elem_data_store.Attributes["data-store"].DeEntitizeValue);
                            uid = data_store.subject_id;
                        }
                    }
                    /* From page like button */
                    if (uid == -1)
                    {
                        var elem_data_store = elem.SelectSingleNode(".//div[contains(@data-store, 'pageID')]");
                        if (elem_data_store != null)
                        {
                            dynamic data_store = JsonConvert.DeserializeObject(elem_data_store.Attributes["data-store"].DeEntitizeValue);
                            uid = data_store.pageID;
                        }
                    }
                    /* Use UID lookup services */
                    if (uid == -1) uid = await GetUID(link);
                    else UID.Add(link, uid); // Contribute to the UID cache
                    reaction.UserID = uid;
                    reaction.UserName = elem.SelectSingleNode(".//strong").InnerText;

                    /* Get reaction type */
                    reaction.Reaction = FBReactUtil.GetReaction(elem.SelectSingleNode("./i"));

                    /* Save reaction */
                    reactions.Remove(uid); // Remove previous reaction if it even exists
                    reactions.Add(uid, reaction);

                    n++;
                    if (cb != null && cb(100 * ((float)n / (float)react_elems.Count)) == false) return null;
                }
            }

            return reactions;
        }

        public async Task<Dictionary<long, string>> GetShares(Func<float, bool>? cb = null)
        {
            Dictionary<long, string> shares = new Dictionary<long, string>();

            /* Load shares page */
            Driver.Navigate().GoToUrl($"https://m.facebook.com/browse/shares?id={PostID}");

            /* Load all accounts */
            while (ClickAndWait("//div[@id='m_more_item']") != -1) ;

            /* Go through each reaction */
            HtmlDocument htmldoc = new HtmlDocument();
            htmldoc.LoadHtml(Driver.PageSource); // For speed improvement (see GetComments)
            var share_elems = htmldoc.DocumentNode.SelectNodes("//div[contains(@data-sigil, 'content-pane')]//i[not(contains(@class, 'profpic'))]/..");
            if (share_elems != null)
            {
                int n = 0;
                if (cb != null & cb(0) == false) return null;
                foreach (var elem in share_elems)
                {
                    /* Get the UID */
                    string link = elem.SelectSingleNode("./div[1]/div[1]//i[contains(@class, 'profpic')]/..").Attributes["href"].DeEntitizeValue;
                    long uid = -1;
                    /* These methods turn out to be working with shares too */
                    /* From add friend button */
                    var elem_data_store = elem.SelectSingleNode(".//a[contains(@data-store, 'id')]");
                    if (elem_data_store != null)
                    {
                        dynamic data_store = JsonConvert.DeserializeObject(elem_data_store.Attributes["data-store"].DeEntitizeValue);
                        uid = data_store.id;
                    }
                    /* From follow button */
                    if (uid == -1)
                    {
                        elem_data_store = elem.SelectSingleNode(".//div[contains(@data-store, 'subject_id')]");
                        if (elem_data_store != null)
                        {
                            dynamic data_store = JsonConvert.DeserializeObject(elem_data_store.Attributes["data-store"].DeEntitizeValue);
                            uid = data_store.subject_id;
                        }
                    }
                    /* From page like button */
                    if (uid == -1)
                    {
                        elem_data_store = elem.SelectSingleNode(".//div[contains(@data-store, 'pageID')]");
                        if (elem_data_store != null)
                        {
                            dynamic data_store = JsonConvert.DeserializeObject(elem_data_store.Attributes["data-store"].DeEntitizeValue);
                            uid = data_store.pageID;
                        }
                    }
                    /* Message button is not present so we can ignore it */
                    /* Use UID lookup services */
                    if (uid == -1) uid = await GetUID(link);
                    else UID.Add(link, uid); // Contribute to the UID cache

                    /* Save account */
                    if (!shares.ContainsKey(uid)) shares.Add(uid, elem.SelectSingleNode(".//strong").InnerText);

                    n++;
                    if (cb != null && cb(100 * ((float)n / (float)share_elems.Count)) == false) return null;
                }
            }

            return shares;
        }
    }
}
