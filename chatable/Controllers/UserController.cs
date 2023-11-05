using Microsoft.AspNetCore.Mvc;

namespace chatable.Controllers
{
    public class UserController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
