using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace WP_ITN_RSS.Models
{
    public class ItnFeed
    {
        static readonly TimeSpan RefreshTime = TimeSpan.FromHours(1);

        string m_wikicode;
        DateTime m_wikicodeDate;

        static WebClient CreateWebClient()
        {
            var webClient = new WebClient();
            webClient.Headers[HttpRequestHeader.UserAgent] = "[[w:en:User:Svick]] ITN Feed";
            webClient.Encoding = Encoding.UTF8;
            return webClient;
        }

        public SyndicationFeed GetFeed()
        {
            if (m_wikicode == null || DateTime.Now - m_wikicodeDate >= RefreshTime)
            {
                m_wikicode = GetCode();
                m_wikicodeDate = DateTime.Now;
            }

            return CodeToFeed(m_wikicode, m_wikicodeDate);
        }

        static readonly Regex ItemRegex = new Regex(@"^{{\*mp\|(\w+ \d+)}}\s*([^<]*?)$(?=\s*({{\*mp|</ul>))", RegexOptions.Multiline | RegexOptions.Singleline);

        static SyndicationFeed CodeToFeed(string wikicode, DateTime wikicodeDate)
        {
            var matches = ItemRegex.Matches(wikicode);

            var image = GetImage(wikicode);

            var items = from Match match in matches
                        let dateString = match.Groups[1].Value
                        let date = DateTime.ParseExact(dateString, "MMMM d", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime()
                        let fixedDate = date > DateTime.Now.AddDays(7) ? date.AddYears(-1) : date
                        let message = match.Groups[2].Value
                        let title = StripWikiCode(RemovePictured(message))
                        let summary = WikiCodeToHtml(AddImage(message, image))
                        let mainLink = FindMainLink(message)
                        select new SyndicationItem(
                            title,
                            new TextSyndicationContent(summary, TextSyndicationContentKind.XHtml),
                            mainLink != null ? new Uri(FormatPageUrl(mainLink)) : null,
                            mainLink ?? ComputeGuid(title),
                            fixedDate);

            return new SyndicationFeed(
                "Wikipedia In the news",
                "A feed for Wikipedia's In the news",
                new Uri(FormatPageUrl("Wikipedia:In the news")),
                "http://itn.svick.org",
                new DateTimeOffset(wikicodeDate),
                items);
        }

        static readonly Regex WikiLink = new Regex(@"\[\[(?:([^]|]+)\|)?([^]]+)\]\](\w*)");
        static readonly Regex NoWrap = new Regex(@"{{nowrap\|(.*?)}}");
        static readonly Regex Bold = new Regex("'''(.*?)'''");
        static readonly Regex Italic = new Regex("''(.*?)''");

        static string StripWikiCode(string wikicode)
        {
            var replaced = wikicode
                .Replace(WikiLink, "$2$3")
                .Replace(NoWrap, "$1")
                .Replace("'''", "")
                .Replace("''", "")
                .Replace("\n", "");
            return WebUtility.HtmlDecode(replaced);
        }

        static string FormatPageUrl(string page)
        {
            return "http://en.wikipedia.org/wiki/" + Uri.EscapeUriString(page.Replace("&nbsp;", " ").Replace(' ', '_'));
        }

        static string FindMainLink(string wikicode)
        {
            var linksInBold = from Match boldMatch in Bold.Matches(wikicode)
                              from Match linkMatch in WikiLink.Matches(boldMatch.Value)
                              let page = linkMatch.Groups[1].Value
                              let text = linkMatch.Groups[2].Value
                              select page != "" ? page : text;

            var boldInLinks = from Match linkMatch in WikiLink.Matches(wikicode)
                              let page = linkMatch.Groups[1].Value
                              let text = linkMatch.Groups[2].Value
                              let boldMatch = Bold.Match(text)
                              where boldMatch.Success
                              select page != "" ? page : boldMatch.Groups[1].Value;

            return linksInBold.Concat(boldInLinks).FirstOrDefault();
        }

        static string FormatLink(string page, string text, string afterText)
        {
            if (page == "")
                page = text;

            return string.Format("<a href=\"{0}\">{1}{2}</a>", FormatPageUrl(page), text, afterText);
        }

        static string WikiCodeToHtml(string wikicode)
        {
            return wikicode
                .Replace(WikiLink, m => FormatLink(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value))
                .Replace(NoWrap, "<span class=\"nowrap\">$1</span>")
                .Replace(Bold, "<b>$1</b>")
                .Replace(Italic, "<i>$1</i>");
        }

        string GetCode()
        {
            return CreateWebClient().DownloadString("http://en.wikipedia.org/w/index.php?title=Template:In_the_news&action=raw");
        }

        static string ComputeGuid(string text)
        {
            SHA1 sha1 = SHA1.Create();
            byte[] textBytes =
                Encoding.UTF8.GetBytes(
                    text.ToUpper()
                        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.OtherPunctuation)
                        .ToArray());
            byte[] hashBytes = sha1.ComputeHash(textBytes);
            return string.Concat(Array.ConvertAll(hashBytes, x => x.ToString("X2")));
        }

        private static readonly Regex PicturedRegex = new Regex(@" ''\([^(]*pictured[^)]*\)''");

        static string RemovePictured(string titleString)
        {
            return PicturedRegex.Replace(titleString, "");
        }

        static string AddImage(string itemString, WikiImage image)
        {
            if (image == null || !PicturedRegex.IsMatch(itemString))
                return itemString;

            return image.ToHtml() + itemString;
        }

        static readonly Regex ImageRegex = new Regex(@"{{[Ii]n the news/image\n\s*\|\s*image\s*=\s*([^|}]*)\s*\|\s*size\s*=\s*([0-9px]+)\s*|\s*title\s*=\s*([^|}]+)\s*(?:}}|\|)", RegexOptions.Singleline);

        static WikiImage GetImage(string wikicode)
        {
            var match = ImageRegex.Match(wikicode);
            if (!match.Success || string.IsNullOrWhiteSpace(match.Groups[1].Value))
                return null;

            return GetImage(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
        }

        static WikiImage GetImage(string imageName, string sizeString, string title)
        {
            if (!imageName.StartsWith("File:"))
                imageName = "File:" + imageName;

            string queryUrl = "http://en.wikipedia.org/w/api.php?format=xml&action=query&prop=imageinfo&iiprop=url&titles=" + Uri.EscapeUriString(imageName);

            var size = ParseSizeString(sizeString);

            if (size.Item1.HasValue)
            {
                queryUrl += "&iiurlwidth=" + size.Item1.Value;
            }
            if (size.Item2.HasValue)
                queryUrl += "&iiurlheight=" + size.Item2.Value;

            string xml = CreateWebClient().DownloadString(queryUrl);

            var doc = XDocument.Parse(xml);

            var ii = doc.Root.Element("query").Element("pages").Element("page").Element("imageinfo").Element("ii");

            WikiImage image;

            string descriptionUrl = ii.Attribute("descriptionurl").Value;

            if (size.Item1.HasValue || size.Item2.HasValue)
            {
                string thumbUrl = ii.Attribute("thumburl").Value;
                int width = int.Parse(ii.Attribute("thumbwidth").Value);
                int height = int.Parse(ii.Attribute("thumbheight").Value);
                image = new WikiImage(thumbUrl, width, height, descriptionUrl, title);
            }
            else
                image = new WikiImage(ii.Attribute("url").Value, descriptionUrl, title);

            return image;
        }

        static readonly Regex sizeRegex = new Regex(@"(\d+)?(?:x(\d+))?px");

        static Tuple<int?, int?> ParseSizeString(string sizeString)
        {
            var match = sizeRegex.Match(sizeString);
            int? width = null;
            int? height = null;

            if (match.Groups[1].Success)
                width = int.Parse(match.Groups[1].Value);
            if (match.Groups[2].Success)
                height = int.Parse(match.Groups[2].Value);

            return Tuple.Create(width, height);
        }
    }

    static class StringExtensions
    {
        public static string Replace(this string original, Regex regex, string replacement)
        {
            return regex.Replace(original, replacement);
        }

        public static string Replace(this string original, Regex regex, MatchEvaluator evaluator)
        {
            return regex.Replace(original, evaluator);
        }
    }
}