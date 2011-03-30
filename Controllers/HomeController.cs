using System.Web;
using System.Web.Mvc;

namespace WP_ITN_RSS.Controllers
{
    [HandleError]
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            var application = HttpContext.Application;

            string itnFeedKey = "itnFeed";

            if (application[itnFeedKey] == null)
                application[itnFeedKey] = new ItnFeed();

            var itnFeed = (ItnFeed)application[itnFeedKey];

            return new RssActionResult { Feed = itnFeed.GetFeed() };
        }
    }
}
