using System;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Net;
using System.Text.RegularExpressions;

namespace WP_ITN_RSS
{
    public class ItnFeed
    {
        static readonly TimeSpan RefreshTime = TimeSpan.FromHours(1);

        string m_wikicode;
        DateTime m_wikicodeDate;

        public SyndicationFeed GetFeed()
        {
            if (m_wikicode == null || DateTime.Now - m_wikicodeDate >= RefreshTime)
            {
                m_wikicode = GetCode();
                m_wikicodeDate = DateTime.Now;
            }

            return CodeToFeed(m_wikicode);
        }

        static readonly Regex itemRegex = new Regex(@"^{{\*mp\|(\w+ \d+)}} (.*)$", RegexOptions.Multiline);

        static SyndicationFeed CodeToFeed(string wikicode)
        {
            var matches = itemRegex.Matches(wikicode);

            var items = from Match match in matches
                        let dateString = match.Groups[1].Value
                        let date = DateTime.ParseExact(dateString, "MMMM dd", System.Globalization.CultureInfo.InvariantCulture)
                        let fixedDate = date > DateTime.Now.AddDays(7) ? date.AddYears(-1) : date
                        let message = match.Groups[2].Value
                        select new SyndicationItem
                        {
                            Summary = new TextSyndicationContent(wikiCodeToHtml(message), TextSyndicationContentKind.Html),
                            Title = new TextSyndicationContent(stripWikiCode(message), TextSyndicationContentKind.Plaintext),
                            PublishDate = fixedDate
                        };

            return new SyndicationFeed { Title = new TextSyndicationContent("Wikipedia In the news feed"), Items = items };
        }

        static readonly Regex wikiLink = new Regex(@"\[\[(?:([^]|]+)\|)?([^]]+)\]\]");
        static readonly Regex bold = new Regex("'''(.*?)'''");
        static readonly Regex italic = new Regex("''(.*?)''");

        static string stripWikiCode(string wikicode)
        {
            return wikiLink.Replace(wikicode, "$2").Replace("'''", "").Replace("''", "");
        }

        static string formatLink(string page, string text)
        {
            if (page == "")
                page = text;

            return string.Format("<a href=\"http://en.wikipedia.org/wiki/{0}\">{1}</a>", Uri.EscapeUriString(page), text);
        }

        static string wikiCodeToHtml(string wikicode)
        {
            string result = wikiLink.Replace(
                wikicode,
                m => formatLink(m.Groups[1].Value, m.Groups[2].Value));
            result = bold.Replace(result, "<b>$1</b>");
            result = italic.Replace(result, "<i>$1</i>");

            return result;
        }

        static string GetCode()
        {
            var wc = new WebClient();

            wc.Headers[HttpRequestHeader.UserAgent] = "[[w:en:User:Svick]] ITN Feed";
            wc.Encoding = System.Text.Encoding.UTF8;

            return wc.DownloadString("http://en.wikipedia.org/w/index.php?title=Template:In_the_news&action=raw");
        }
    }
}