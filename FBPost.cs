/*
 * FBPost.cs - Functions for gathering information on Facebook posts.
 *             Contrary to the old HRng which is Selenium-based,
 *             this rewritten version will **only** use HttpClient
 *             for all of its operations.
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

namespace HRngBackend
{
    public class FBPost
    {
        /*
         * public long PostID
         *   The Facebook post's ID. This is set by the initializer
         *   below.
         */
        public long PostID = -1;

        /*
         * public long AuthorID
         *   The Facebook post author's user ID. This is set by the
         *   initializer below.
         *   This ID is needed for generating the comments URL
         *   (story.php) reliably. Without it, certain types of posts
         *   such as Facebook Watch videos will not load its comments.
         */
        public long AuthorID = -1;

        /*
         * public bool IsGroupPost
         *   Whether this post is from a group. Group posts require
         *   different handling in how the comments URL is constructed
         *   (i.e. no AuthorID, or else the page will bug out)
         */
        public bool IsGroupPost = false;

        /*
         * private IWebDriver Driver
         *   The Selenium WebDriver instance associated with this post.
         *   Do NOT share drivers across multiple posts concurrently;
         *   Selenium WebDriver instances aren't by any means
         *   thread-safe (even a single state change requires all nodes
         *   to be re-fetched).
         */
        private IWebDriver Driver = null;

        /*
         * public FBPost(IWebDriver driver)
         *   Class constructor. Sets the WebDriver instance.
         */
        public FBPost(IWebDriver driver)
        {
            Driver = driver;
        }

        /*
         * public async Task<int> Initialize(long id)
         * public async Task<int> Initialize(string url)
         *   Attempt to get the IDs of the post and its author.
         *   Input : id : The post's ID.
         *           url: The post's URL.
         *   Output: 0 if the initialization is successful, or one
         *           of these values on failure:
         *            -1  Invalid URL
         *            -2  Invalid webpage output (e.g. wrong URL,
         *                ratelimited by Facebook, or not logged in)
         */
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
            IList<string> uri_segments = new List<string>();
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
                    AuthorID = await UID.Get(Driver.FindElement(By.XPath("//a[@data-sigil='actor-link']")).GetAttribute("href"));
                }
                catch (NoSuchElementException) { }

            }
            if (AuthorID < 0) return -2;

            /* Get post ID */
            PostID = -1;
            /* Attempt to get directly from URL */
            if (IsGroupPost && uri_segments[uri_segments.IndexOf("permalink") + 1].All(char.IsDigit)) AuthorID = Convert.ToInt64(uri_segments[uri_segments.IndexOf("permalink") + 1]);
            else
            {
                var url_params = HttpUtility.ParseQueryString(driver_uri.Query);
                if (url_params.Get("story_fbid") != null) PostID = Convert.ToInt64(url_params.Get("story_fbid"));
            }
            /* Attempt to get from post container (not working with images) */
            if (PostID < 0)
            {
                try
                {
                    dynamic data_ft = JsonConvert.DeserializeObject(Driver.FindElement(By.XPath("//div[contains(@data-ft, 'top_level_post_id')]")).GetAttribute("data-ft"));
                    if (data_ft != null) AuthorID = Convert.ToInt64(data_ft.top_level_post_id);
                }
                catch (NoSuchElementException) { }
            }
            /* Attempt to get from react button */
            if (PostID < 0)
            {
                try
                {
                    dynamic data_store = JsonConvert.DeserializeObject(Driver.FindElement(By.XPath("//div[contains(@data-store, 'feedbackTarget'])")).GetAttribute("data-store"));
                    if (data_store != null) PostID = Convert.ToInt64(data_store.feedbackTarget);
                }
                catch (NoSuchElementException) { }
            }
            /* Attempt to get from comment button */
            if (PostID < 0)
            {
                try
                {
                    dynamic data_store = JsonConvert.DeserializeObject(Driver.FindElement(By.XPath("//div[contains(@data-store, 'click_comment_ufi'])")).GetAttribute("data-store"));
                    if (data_store != null) PostID = Convert.ToInt64(data_store.target_id);
                }
                catch (NoSuchElementException) { }
            }
            /* Attempt to get from comment button, ONLY IF THIS IS NOT A FACEBOOK WATCH POST PAGE (turns out Facebook Watch got its own post ID system) */
            if (PostID < 0 && !uri_segments.Contains("watch"))
            {
                try
                {
                    dynamic data_store = JsonConvert.DeserializeObject(Driver.FindElement(By.XPath("//div[contains(@data-store, 'click_share_ufi'])")).GetAttribute("data-store"));
                    if (data_store != null) PostID = Convert.ToInt64(data_store.target_id);
                }
                catch (NoSuchElementException) { }
            }
            /* Attempt to get from comment section */
            if (PostID < 0)
            {
                try
                {
                    PostID = Convert.ToInt64(Driver.FindElement(By.XPath("//div[starts-with(@id, 'ufi_')]")).GetAttribute("id").Replace("ufi_", ""));
                }
                catch (NoSuchElementException) { }
            }
            if (PostID < 0) return -2;

            return 0;
        }

        /*
         * private int ClickAndWait(string click_xpath, [string check_xpath], [int delay])
         *   Helper function to click an element and wait for the
         *   the AJAX request to finish. Useful for loading more
         *   items.
         *   Input : click_xpath: XPath pointing to the element to
         *                        be clicked.
         *           check_xpath: XPath pointing to the element that
         *                        is present during the loading
         *                        process (optional).
         *                        By default, it's set to watch for
         *                        any elements with the
         *                        async_elem_saving class, which is
         *                        good enough in most cases.
         *           delay      : The interval in milliseconds to wait
         *                        for after the checking is complete
         *                        to let the new elements load.
         *                        Defaults to 250ms.
         *   Output: -1 if there's no element to click, or 0 if
         *           the function succeeds.
         */
        private int ClickAndWait(string click_xpath, string check_xpath = "//*[contains(@class, 'async_elem_saving')]", int delay = 250)
        {
            while (true)
            {
                try { Driver.FindElement(By.XPath(click_xpath)).Click(); break; }
                catch (NoSuchElementException) { return -1; }
                catch (StaleElementReferenceException) { }
                catch (ElementClickInterceptedException) { } // Just try again basically
            }
            while (true)
            {
                try { Driver.FindElement(By.XPath(check_xpath)); }
                catch (NoSuchElementException) { break; }
                catch (StaleElementReferenceException) { }
            }
            Thread.Sleep(delay); // Wait for new content to be loaded
            return 0;
        }

        /*
         * public async Task<IDictionary<long, FBComment>> GetComments([Func<int, int, int> cb])
         *   Scrape all comments from the Facebook post.
         *   Input : cb: Callback function to be called when each
         *               comment has been saved (optional).
         *               This function takes 2 arguments, the first one
         *               being the number of comments fetched, and the
         *               second one being the total number of comments,
         *               and returns an int value that is ignored.
         *   Output: a comment ID => FBComment instance dictionary.
         */
        public async Task<IDictionary<long, FBComment>> GetComments(Func<int, int, int>? cb = null)
        {
            IDictionary<long, FBComment> comments = new Dictionary<long, FBComment>();

            /* Get post page */
            Driver.Navigate().GoToUrl((IsGroupPost) ? $"https://m.facebook.com/{PostID}" : $"https://m.facebook.com/story.php?story_fbid={PostID}&id={AuthorID}");

            /* Load all top-level comments */
            int cnt = 0, cnt_prev = 0; // Current and previous comments count
            do
            {
                try
                {
                    cnt_prev = cnt;
                    if (ClickAndWait("//div[starts-with(@id, 'see_next') or starts-with(@id, 'see_prev')]") == -1) break;
                    for (int i = 0; i < 5 && cnt_prev == cnt; i++)
                    {
                        Thread.Sleep(200); // Poll for changes 5 times, each time every 200ms (1s in total)
                        cnt = Driver.FindElements(By.XPath("//div[@data-sigil='comment inline-reply' or @data-sigil='comment']")).Count;
                    }
                }
                catch (NoSuchElementException) { break; }
                catch (StaleElementReferenceException) { continue; }
            } while (cnt != cnt_prev);

            /* Load all replies */
            while (ClickAndWait("//div[starts-with(@data-sigil, 'replies-see')]") != -1) ;

            /* Selenium seems to be pretty slow when it comes to finding elements, so we'll use HtmlAgilityPack instead */
            HtmlDocument htmldoc = new HtmlDocument();
            htmldoc.LoadHtml(Driver.PageSource);

            /* Read each comment */
            try
            {
                var comment_elems = htmldoc.DocumentNode.SelectNodes("//div[@data-sigil='comment inline-reply' or @data-sigil='comment']");
                int n = 0;
                if (cb != null) cb(n, comment_elems.Count);
                foreach (var elem in comment_elems)
                {
                    long id = Convert.ToInt64(elem.Attributes["id"].DeEntitizeValue); // Comment ID
                    if(!comments.ContainsKey(id))
                    {
                        FBComment comment = new FBComment();
                        comment.ID = id;
                        var elem_profile_pict = elem.SelectSingleNode("./div[contains(@data-sigil, 'feed_story_ring')]");
                        comment.AuthorID = Convert.ToInt64(elem_profile_pict.Attributes["data-sigil"].DeEntitizeValue.Replace("feed_story_ring", ""));
                        var elem_comment = elem_profile_pict.SelectSingleNode("./following-sibling::div[1]");
                        UID.Add(elem_comment.SelectSingleNode("./div[1]//a").Attributes["href"].DeEntitizeValue, comment.AuthorID);
                        comment.AuthorName = elem_comment.SelectSingleNode("./div[1]//a").InnerText; // TODO: Remove the Author text on top of the name
                        var elem_body = elem_comment.SelectSingleNode("./div[1]//div[@data-sigil='comment-body']");
                        if (elem_body != null)
                        {
                            comment.CommentText = HttpUtility.HtmlDecode(elem_body.InnerText);
                            comment.CommentText_HTML = elem_body.InnerHtml;
                        }
                        if (comment.CommentText != "")
                        {
                            var elem_mentions = elem_body.SelectNodes("./a");
                            if (elem_mentions != null)
                            {
                                foreach (var elem_mention in elem_mentions)
                                {
                                    string url = elem_mention.Attributes["href"].DeEntitizeValue;
                                    if (url.StartsWith("/") && !url.Contains(elem_mention.InnerText) && UID.GetHandle(url) != "") comment.Mentions.Add(await UID.Get(url));
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

                        n++;
                        if (cb != null) cb(n, comment_elems.Count);
                    }
                    FBComment cmt = comments[id];
                    if (cmt.Parent == -1 && elem.Attributes["data-sigil"].DeEntitizeValue.Contains("inline-reply"))
                    {
                        /* This comment is a reply, now find its parent */
                        var elem_parent = elem.SelectSingleNode("./ancestor::div[@data-sigil='comment']");
                        cmt.Parent = Convert.ToInt64(elem_parent.Attributes["id"].DeEntitizeValue);
                    }
                }
            }
            catch(NoSuchElementException) { }

            return comments;
        }

        /*
         * public async Task<IDictionary<long, FBReact>> GetReactions([Func<int, int, int> cb])
         *  Get all reactions to the post.
         *   Input : cb: Callback function to be called when each
         *               reaction has been saved (optional).
         *               This function takes 2 arguments, the first one
         *               being the number of reactions fetched, and the
         *               second one being the total number of reactions,
         *               and returns an int value that is ignored.
         *   Output: an user ID => FBReact instance dictionary.
         */
        public async Task<IDictionary<long, FBReact>> GetReactions(Func<int, int, int>? cb = null)
        {
            IDictionary<long, FBReact> reactions = new Dictionary<long, FBReact>();

            /* Load reactions page */
            Driver.Navigate().GoToUrl($"https://m.facebook.com/ufi/reaction/profile/browser/?ft_ent_identifier={PostID}");

            /* Get background-image + background-position => reaction type mapping */
            IDictionary<string, int> react_map = new Dictionary<string, int>(); // Having string as key instead of tuple should result in better performance
            foreach(var elem in Driver.FindElements(By.XPath("//span[@data-sigil='reaction_profile_sigil' and not(contains(@data-store, 'all'))]")))
            {
                var elem_img = elem.FindElement(By.XPath(".//i"));
                dynamic data_store = JsonConvert.DeserializeObject(elem.GetAttribute("data-store"));
                react_map.Add($"{elem_img.GetCssValue("background-image")} {elem_img.GetCssValue("background-position")}", Convert.ToInt32(data_store.reactionType));
            }

            /* As it turns out, Facebook conveniently provides us with a perfectly ordered list of shown users' IDs in the AJAX URL, so we can use that to speedrun the UID retrieval process */
            IList<long> shown_users = new List<long>(); // Where we'll save the IDs
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
                        shown_users.Add(Convert.ToInt64(uid));
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
                if (cb != null) cb(n, react_elems.Count);
                foreach (var elem in react_elems)
                {
                    FBReact reaction = new FBReact();

                    /* Get the UID */
                    string link = elem.SelectSingleNode("./div[1]/div[1]/a").GetAttributeValue("href", "");
                    long uid = -1;
                    /* From shown user ID list */
                    if (n < shown_users.Count) uid = shown_users[n];
                    /* From message button */
                    if (uid == -1)
                    {
                        try
                        {
                            string current_url = Driver.Url; // Just being on the safe side here
                            Driver.FindElement(By.XPath($"//div[@id='reaction_profile_browser']/div[{n + 1}]//button")).Click();
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
                    if (uid == -1) uid = await UID.Get(link);
                    else UID.Add(link, uid); // Contribute to the UID cache
                    reaction.UserID = uid;
                    reaction.UserName = elem.SelectSingleNode(".//strong").InnerText;

                    /* Get reaction type - for this we'll have to go back to Selenium for its CSS capabilities */
                    var elem_type = Driver.FindElement(By.XPath($"//div[@id='reaction_profile_browser']/div[{n + 1}]/i"));
                    reaction.Reaction = (ReactionEnum) react_map[$"{elem_type.GetCssValue("background-image")} {elem_type.GetCssValue("background-position")}"];

                    /* Save reaction */
                    reactions.Remove(uid); // Remove previous reaction if it even exists
                    reactions.Add(uid, reaction);

                    n++;
                    if (cb != null) cb(n, react_elems.Count);
                }
            }

            return reactions;
        }

        /*
         * public async Task<IDictionary<long, string>> GetShares([Func<int, int, int> cb])
         *  Get the list of accounts that shared the post.
         *   Input : cb: Callback function to be called when each
         *               reaction has been saved (optional).
         *               This function takes 2 arguments, the first one
         *               being the number of accounts fetched, and the
         *               second one being the total number of accounts,
         *               and returns an int value that is ignored.
         *   Output: an user ID => user name dictionary.
         */
        public async Task<IDictionary<long, string>> GetShares(Func<int, int, int>? cb = null)
        {
            IDictionary<long, string> shares = new Dictionary<long, string>();

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
                if (cb != null) cb(n, share_elems.Count);
                foreach (var elem in share_elems)
                {
                    /* Get the UID */
                    string link = elem.SelectSingleNode("./div[1]/div[1]/a").Attributes["href"].DeEntitizeValue;
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
                    if (uid == -1) uid = await UID.Get(link);
                    else UID.Add(link, uid); // Contribute to the UID cache

                    /* Save account */
                    if (!shares.ContainsKey(uid)) shares.Add(uid, elem.SelectSingleNode(".//strong").InnerText);

                    n++;
                    if (cb != null) cb(n, share_elems.Count);
                }
            }

            return shares;
        }
    }
}
