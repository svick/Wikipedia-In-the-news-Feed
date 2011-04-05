using System;

namespace WP_ITN_RSS.Models
{
    public class WikiImage
    {
        public Uri ImageUrl { get; protected set; }
        public int? Width { get; protected set; }
        public int? Height { get; protected set; }
        public Uri DescriptionUrl { get; protected set; }
        public string Title { get; protected set; }

        public WikiImage(string imageUrl, string descriptionUrl, string title)
        {
            ImageUrl = new Uri(imageUrl);
            DescriptionUrl = new Uri(descriptionUrl);
            Title = title;
        }

        public WikiImage(string imageUrl, int width, int height, string descriptionUrl, string title)
            : this(imageUrl, descriptionUrl, title)
        {
            Width = width;
            Height = height;
        }

        public string ToHtml()
        {
            return string.Format(
                "<a href=\"{0}\"><img style=\"float: right\" align=\"right\" src=\"{1}\" alt=\"{2}\"{3}{4} /></a>",
                DescriptionUrl,
                ImageUrl,
                Title,
                Width.HasValue ? string.Format(" width=\"{0}\"", Width) : "",
                Height.HasValue ? string.Format(" height=\"{0}\"", Height) : "");
        }
    }
}