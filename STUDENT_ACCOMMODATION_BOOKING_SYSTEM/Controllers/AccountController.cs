using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Controllers;

public sealed class AccountController(AccountRepository accounts, ILogger<AccountController> logger) : Controller
{
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return View(new LoginInput());
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginInput input, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        if (!ModelState.IsValid) return View(input);

        AccountRecord? account;
        try { account = await accounts.FindByEmailAsync(input.Email); }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Không thể kết nối SQL Server khi đăng nhập.");
            ModelState.AddModelError("", "Không thể kết nối cơ sở dữ liệu. Kiểm tra cấu hình SQL Server.");
            return View(input);
        }
        if (account is null)
        {
            ModelState.AddModelError(nameof(input.Email), "Tài khoản không đúng.");
            return View(input);
        }
        if (account.Status != "ACTIVE" || string.IsNullOrEmpty(account.Role))
        {
            ModelState.AddModelError("", "Tài khoản hiện không thể đăng nhập.");
            return View(input);
        }
        if (!accounts.VerifyPassword(account, input.Password))
        {
            ModelState.AddModelError(nameof(input.Password), "Mật khẩu không đúng.");
            return View(input);
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new Claim(ClaimTypes.Name, account.FullName),
            new Claim(ClaimTypes.Role, account.Role)
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        if (Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("access-denied")]
    public IActionResult AccessDenied() => View();
}
