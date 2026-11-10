using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Controllers;

[Authorize(Roles = "SECURITY_GUARD")]
public class GuardInspectionController : Controller
{
    private readonly GuardInspectionRepository _repository;

    public GuardInspectionController(GuardInspectionRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("/security-guard/inspections")]
    public async Task<IActionResult> Index()
    {
        var bookings = await _repository.GetBookingsAsync();

        return View(
            "~/Views/SecurityGuard/Inspections.cshtml",
            bookings
        );
    }

    [HttpGet("/security-guard/inspections/{bookingId:long}/{phase}")]
    public async Task<IActionResult> Detail(long bookingId, string phase)
    {
        phase = phase.ToUpperInvariant();

        if (phase is not ("IN" or "OUT"))
            return BadRequest("Loại kiểm tra không hợp lệ.");

        var model = await _repository.GetFormAsync(bookingId, phase);

        if (model == null)
            return NotFound(
                "Booking không hợp lệ hoặc biên bản này đã được ghi nhận.");

        return View(
            "~/Views/SecurityGuard/InspectionForm.cshtml",
            model
        );
    }

    [HttpPost("/api/security-guard/inspections")]
    public async Task<IActionResult> Save(
        [FromForm] GuardInspectionForm input)
    {
        int guardId = int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

        try
        {
            bool success = await _repository.SaveAsync(input, guardId);

            if (!success)
            {
                return BadRequest(new
                {
                    message = "Booking không hợp lệ hoặc đã có biên bản."
                });
            }

            return Ok(new
            {
                message = "Đã lưu biên bản kiểm tra thiết bị."
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Conflict(new
            {
                message = "Biên bản hoặc thiết bị đã được ghi nhận."
            });
        }
    }
}
