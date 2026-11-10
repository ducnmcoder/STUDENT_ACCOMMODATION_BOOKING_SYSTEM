using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Controllers;

[Authorize(Roles = "SECURITY_GUARD")]
public class SecurityGuardController : Controller
{
    private readonly GuardBookingRepository _repository;

    public SecurityGuardController(GuardBookingRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("/security-guard/bookings")]
    public async Task<IActionResult> Bookings()
    {
        var bookings = await _repository.GetApprovedBookingsAsync();
        return View(bookings);
    }

    [HttpGet("/api/security-guard/bookings")]
    public async Task<IActionResult> GetBookingsApi()
    {
        var bookings = await _repository.GetApprovedBookingsAsync();
        return Ok(bookings);
    }

    [HttpPost("/api/security-guard/bookings/{bookingId:long}/check-in")]
    public async Task<IActionResult> CheckIn(long bookingId)
    {
        int guardId = int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

        try
        {
            bool success = await _repository.CheckInAsync(bookingId, guardId);

            if (!success)
            {
                return BadRequest(new
                {
                    message = "Booking không hợp lệ, đã hết giờ hoặc đã check-in."
                });
            }

            return Ok(new
            {
                message = "Check-in thành công."
            });
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Conflict(new
            {
                message = "Booking này đã được check-in."
            });
        }
    }

    [HttpGet("/security-guard/checkouts")]
    public async Task<IActionResult> Checkouts()
    {
        var bookings = await _repository.GetApprovedBookingsAsync();

        var checkedInBookings = bookings
            .Where(b => b.CheckInOutcome is "ON_TIME" or "LATE")
            .ToList();

        return View(checkedInBookings);
    }

    [HttpPost("/api/security-guard/bookings/{bookingId:long}/check-out")]
    public async Task<IActionResult> CheckOut(long bookingId)
    {
        int guardId = int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!
        );

        try
        {
            bool success = await _repository.CheckOutAsync(bookingId, guardId);

            if (!success)
            {
                return BadRequest(new
                {
                    message = "Booking không hợp lệ, chưa check-in hoặc đã check-out."
                });
            }

            return Ok(new
            {
                message = "Check-out thành công."
            });
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Conflict(new
            {
                message = "Booking này đã được check-out."
            });
        }
    }

}
