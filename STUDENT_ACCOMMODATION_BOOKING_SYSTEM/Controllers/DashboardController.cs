using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Controllers;

[Authorize]
public sealed class DashboardController(AccountRepository accounts, ReputationRepository reputation, ILogger<DashboardController> logger) : Controller
{
    [HttpGet("student")]
    [Authorize(Roles = "STUDENT")]
    public Task<IActionResult> Student() => Show("STUDENT");

    [HttpGet("admin")]
    [Authorize(Roles = "ADMIN")]
    public Task<IActionResult> Admin() => Show("ADMIN");

    [HttpGet("facilities-staff")]
    [Authorize(Roles = "FACILITIES_STAFF")]
    public Task<IActionResult> FacilitiesStaff() => Show("FACILITIES_STAFF");

    [HttpGet("security-guard")]
    [Authorize(Roles = "SECURITY_GUARD")]
    public Task<IActionResult> SecurityGuard() => Show("SECURITY_GUARD");

    [HttpGet("student/reputation")]
    [Authorize(Roles = "STUDENT")]
    public async Task<IActionResult> StudentReputation() => View("StudentReputation", await reputation.GetStudentHistoryAsync(AccountId()));

    [HttpGet("admin/reputation")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> AdminReputation() => View("AdminReputation", await reputation.GetAdminHistoryAsync());

    private async Task<IActionResult> Show(string role)
    {
        var profile = await accounts.GetProfileAsync(AccountId());
        if (profile is null || profile.Role != role) return Forbid();
        return View("Index", profile);
    }

    [HttpPost("profile")]
    public async Task<IActionResult> Save(ProfileViewModel input)
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
        input.Role = role;
        if (role == "STUDENT" && string.IsNullOrWhiteSpace(input.StudentCode))
            ModelState.AddModelError(nameof(input.StudentCode), "Vui lòng nhập mã sinh viên.");
        if (role is "FACILITIES_STAFF" or "SECURITY_GUARD" && string.IsNullOrWhiteSpace(input.EmployeeCode))
            ModelState.AddModelError(nameof(input.EmployeeCode), "Vui lòng nhập mã nhân viên.");
        if (input.Items.Count > 30)
            ModelState.AddModelError(nameof(input.Items), "Tối đa 30 mục thông tin trong một lần lưu.");
        foreach (var item in input.Items.Where(i => !i.Delete))
        {
            if (string.IsNullOrWhiteSpace(item.Label) || string.IsNullOrWhiteSpace(item.Value))
                ModelState.AddModelError(nameof(input.Items), "Mỗi mục thêm phải có tên và nội dung.");
        }
        if (!ModelState.IsValid) return View("Index", input);

        try
        {
            await accounts.SaveProfileAsync(AccountId(), role, input);
            TempData["Success"] = "Đã lưu thông tin xuống cơ sở dữ liệu.";
            return RedirectToAction(RoleAction(role));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            ModelState.AddModelError("", "Email hoặc mã định danh này đã được sử dụng.");
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException)
        {
            logger.LogError(exception, "Không thể lưu hồ sơ cho tài khoản {AccountId}", AccountId());
            ModelState.AddModelError("", "Không thể lưu thông tin. Vui lòng thử lại.");
        }
        return View("Index", input);
    }

    private int AccountId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static string RoleAction(string role) => role switch
    {
        "STUDENT" => nameof(Student),
        "ADMIN" => nameof(Admin),
        "FACILITIES_STAFF" => nameof(FacilitiesStaff),
        "SECURITY_GUARD" => nameof(SecurityGuard),
        _ => nameof(Student)
    };
}
