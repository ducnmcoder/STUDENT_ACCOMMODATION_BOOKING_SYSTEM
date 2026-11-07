using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Controllers;

public sealed class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        // If user is authenticated, redirect to their dashboard
        if (User.Identity?.IsAuthenticated == true)
        {
            var action = User.IsInRole("STUDENT") ? "Student"
                : User.IsInRole("ADMIN") ? "Admin"
                : User.IsInRole("FACILITIES_STAFF") ? "FacilitiesStaff"
                : User.IsInRole("SECURITY_GUARD") ? "SecurityGuard" : null;
            return action is null ? RedirectToAction("AccessDenied", "Account")
                : RedirectToAction(action, "Dashboard");
        }
        
        // If not authenticated, show landing page
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel
        { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
