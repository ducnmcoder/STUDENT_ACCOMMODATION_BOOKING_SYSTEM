using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthApiController : ControllerBase
{
    private readonly AccountRepository _accountRepository;
    private readonly JwtService _jwtService;

    public AuthApiController(AccountRepository accountRepository, JwtService jwtService)
    {
        _accountRepository = accountRepository;
        _jwtService = jwtService;
    }

    /// <summary>
    /// API đăng nhập - trả về JWT token
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { message = "Dữ liệu không hợp lệ", errors = ModelState });
        }

        var account = await _accountRepository.LoginAsync(request.Email, request.Password);

        if (account == null)
        {
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng" });
        }

        var token = _jwtService.GenerateToken(
            account.AccountId,
            account.Email,
            account.Role,
            account.FullName
        );

        return Ok(new
        {
            token = token,
            accountId = account.AccountId,
            email = account.Email,
            fullName = account.FullName,
            role = account.Role,
            expiresIn = 480 * 60 // 8 hours in seconds
        });
    }

    /// <summary>
    /// API lấy thông tin user hiện tại (yêu cầu JWT token)
    /// </summary>
    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public IActionResult GetCurrentUser()
    {
        var accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);
        var role = User.FindFirstValue(ClaimTypes.Role);
        var fullName = User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrEmpty(accountId))
        {
            return Unauthorized(new { message = "Token không hợp lệ" });
        }

        return Ok(new
        {
            accountId = int.Parse(accountId),
            email = email,
            fullName = fullName,
            role = role
        });
    }

    /// <summary>
    /// API test - yêu cầu JWT và role Admin
    /// </summary>
    [HttpGet("admin-only")]
    [Authorize(AuthenticationSchemes = "Bearer", Roles = "Admin")]
    public IActionResult AdminOnly()
    {
        return Ok(new { message = "Chào mừng Admin!", user = User.Identity?.Name });
    }

    /// <summary>
    /// API test - yêu cầu JWT và role Guard
    /// </summary>
    [HttpGet("guard-only")]
    [Authorize(AuthenticationSchemes = "Bearer", Roles = "Guard")]
    public IActionResult GuardOnly()
    {
        return Ok(new { message = "Chào mừng Guard!", user = User.Identity?.Name });
    }

    /// <summary>
    /// API test - yêu cầu JWT và role Student
    /// </summary>
    [HttpGet("student-only")]
    [Authorize(AuthenticationSchemes = "Bearer", Roles = "Student")]
    public IActionResult StudentOnly()
    {
        return Ok(new { message = "Chào mừng Student!", user = User.Identity?.Name });
    }
}

public class LoginRequest
{
    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
    public string Password { get; set; } = string.Empty;
}
