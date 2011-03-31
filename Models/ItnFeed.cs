using System;
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
            webClient.Encoding = System.Text.Encoding.UTF8;
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

        static readonly Regex itemRegex = new Regex(@"^{{\*mp\|(\w+ \d+)}}(.*)$", RegexOptions.Multiline);

        static SyndicationFeed CodeToFeed(string wikicode, DateTime wikicodeDate)
        {
            var matches = itemRegex.Matches(wikicode);

            var image = GetImage(wikicode);

            var items = from Match match in matches
                        let dateString = match.Groups[1].Value
                        let date = DateTime.ParseExact(dateString, "MMMM dd", System.Globalization.CultureInfo.InvariantCulture)
                        let fixedDate = date > DateTime.Now.AddDays(7) ? date.AddYears(-1) : date
                        let message = match.Groups[2].Value
                        let title = RemovePictured(StripWikiCode(message))
                        let summary = ReplaceImage(WikiCodeToHtml(message), image)
                        select new SyndicationItem
                        {
                            Title = new TextSyndicationContent(title, TextSyndicationContentKind.Plaintext),
                            Summary = new TextSyndicationContent(summary, TextSyndicationContentKind.XHtml),
                            PublishDate = fixedDate,
                            Id = ComputeGuid(title)
                        };

            return new SyndicationFeed(
                "Wikipedia In the news",
                "A feed for Wikipedia's In the news",
                new Uri("http://en.wikipedia.org/wiki/Wikipedia:In_the_news"),
                "http://itn.svick.org",
                new DateTimeOffset(wikicodeDate),
                items);
        }

        static readonly Regex wikiLink = new Regex(@"\[\[(?:([^]|]+)\|)?([^]]+)\]\]");
        static readonly Regex bold = new Regex("'''(.*?)'''");
        static readonly Regex italic = new Regex("''(.*?)''");

        static string StripWikiCode(string wikicode)
        {
            return wikiLink.Replace(wikicode, "$2").Replace("'''", "").Replace("''", "");
        }

        static string FormatLink(string page, string text)
        {
            if (page == "")
                page = text;

            return string.Format("<a href=\"http://en.wikipedia.org/wiki/{0}\">{1}</a>", Uri.EscapeUriString(page), text);
        }

        static string WikiCodeToHtml(string wikicode)
        {
            string result = wikiLink.Replace(
                wikicode,
                m => FormatLink(m.Groups[1].Value, m.Groups[2].Value));
            result = bold.Replace(result, "<b>$1</b>");
            result = italic.Replace(result, "<i>$1</i>");

            return result;
        }

        string GetCode()
        {
            return CreateWebClient().DownloadString("http://en.wikipedia.org/w/index.php?title=Template:In_the_news&action=raw");
        }

        static string ComputeGuid(string text)
        {
            SHA1 sha1 = SHA1CryptoServiceProvider.Create();
            byte[] textBytes = Encoding.UTF8.GetBytes(text);
            byte[] hashBytes = sha1.ComputeHash(textBytes);
            return string.Concat(Array.ConvertAll(hashBytes, x => x.ToString("X2")));
        }

        static string RemovePictured(string titleString)
        {
            return titleString.Replace("(pictured)", "");
        }

        static string ReplaceImage(string itemString, WikiImage image)
        {
            const string pictured = "<i>(pictured)</i>";

            if (!itemString.Contains(pictured))
                return itemString;

            return image.ToHtml() + itemString.Replace(pictured, "");
        }

        static readonly Regex imageRegex = new Regex(@"{{In the news/image\n\s*\|\s*image\s*=\s*([^|}]+)\s*\|\s*size\s*=\s*([0-9px]+)\s*|\s*title\s*=\s*([^|}]+)\s*(?:}}|\|)", RegexOptions.Singleline);

        static WikiImage GetImage(string wikicode)
        {
            var match = imageRegex.Match(wikicode);
            if (!match.Success)
                return null;

            return GetImage(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
        }

        static WikiImage GetImage(string imageName, string sizeString, string title)
        {
            string queryUrl = "http://en.wikipedia.org/w/api.php?format=xml&action=query&prop=imageinfo&iiprop=url&titles=File:" + Uri.EscapeUriString(imageName);

            var size = ParseSizeString(sizeString);

            if (size.Item1.HasValue)
                queryUrl += "&iiurlwidth=" + size.Item1.Value;
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
}