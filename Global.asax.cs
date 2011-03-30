using System.Web.Mvc;
using System.Web.Routing;

namespace WP_ITN_RSS
{
    public class MvcApplication : System.Web.HttpApplication
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                "Default",
                "{controller}.aspx/{action}/{id}",
                new { action = "Index", id = "" }
            );

            routes.MapRoute(
                "Root",
                "",
                new { controller = "Home", action = "Index", id = "" }
            );

        }

        protected void Application_Start()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

            AreaRegistration.RegisterAllAreas();

            RegisterRoutes(RouteTable.Routes);
        }
    }
}