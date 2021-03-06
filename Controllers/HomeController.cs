﻿using System.Web.Mvc;
using WP_ITN_RSS.Models;

namespace WP_ITN_RSS.Controllers
{
    [HandleError]
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var application = HttpContext.Application;

            const string itnFeedKey = "itnFeed";

            if (application[itnFeedKey] == null)
                application[itnFeedKey] = new ItnFeed();

            var itnFeed = (ItnFeed)application[itnFeedKey];

            return new RssActionResult { Feed = itnFeed.GetFeed() };
        }
    }
}
