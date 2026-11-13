using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Services;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Controllers;

[Authorize(Roles = "STUDENT")]
public sealed class StudentRoomsController(StudentBookingService bookings, ILogger<StudentRoomsController> logger) : Controller
{
    [HttpGet("student/rooms")]
    public async Task<IActionResult> Index()
    {
        var model = new RoomsViewModel
        {
            Rooms = await bookings.GetRoomsAsync(),
            Buildings = await bookings.GetBuildingsAsync(),
            Eligibility = await bookings.GetEligibilityAsync(StudentId())
        };
        return View(model);
    }

    [HttpGet("student/bookings")]
    public async Task<IActionResult> MyBookings() => View(await bookings.GetBookingsAsync(StudentId()));

    [HttpPost("student/bookings")]
    public async Task<IActionResult> Book(BookRoomInput input)
    {
        if (!ModelState.IsValid || input.StartAt is null || input.EndAt is null ||
            input.StartAt.Value.UtcDateTime <= DateTime.UtcNow ||
            input.EndAt.Value.UtcDateTime <= input.StartAt.Value.UtcDateTime ||
            input.StartAt.Value.UtcDateTime.AddHours(7).Date !=
                input.EndAt.Value.UtcDateTime.AddSeconds(-1).AddHours(7).Date)
        {
            TempData["Error"] = "Vui lòng chọn thời gian trong tương lai và trong cùng một ngày đặt.";
            return RedirectToAction(nameof(Index));
        }
        try
        {
            var bookingId = await bookings.BookAsync(StudentId(), input);
            TempData["Success"] = $"Đã đặt phòng thành công. Mã đặt phòng: {bookingId}.";
            return RedirectToAction(nameof(MyBookings));
        }
        catch (SqlException exception) when (exception.Number is >= 51003 and <= 51008 or 51019 or 51020 or 51021)
        {
            TempData["Error"] = exception.Number switch
            {
                51004 => "Hiện chưa có chính sách đặt phòng có hiệu lực.",
                51005 => "Tài khoản sinh viên không còn đủ điều kiện đặt phòng.",
                51006 => "Phòng này hiện không khả dụng.",
                51007 => "Điểm uy tín tháng này thấp hơn mức cho phép đặt phòng.",
                51008 => "Khung giờ này vừa được đặt. Vui lòng tìm phòng trống khác.",
                51019 => "Chỉ có thể đặt phòng trong tương lai.",
                51020 => "Lịch đặt phải nằm trong một ngày theo giờ Việt Nam.",
                51021 => "Bạn đã dùng hết lượt đặt phòng trong tuần này.",
                _ => "Khoảng thời gian đặt phòng không hợp lệ."
            };
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Không thể đặt phòng cho sinh viên {StudentId}", StudentId());
            TempData["Error"] = "Không thể đặt phòng lúc này. Vui lòng thử lại.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("student/bookings/cancel")]
    public async Task<IActionResult> Cancel(CancelBookingInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Yêu cầu hủy đặt phòng không hợp lệ.";
            return RedirectToAction(nameof(MyBookings));
        }
        try
        {
            await bookings.CancelAsync(StudentId(), input);
            TempData["Success"] = "Đã hủy đặt phòng.";
        }
        catch (SqlException exception) when (exception.Number == 51009)
        {
            TempData["Error"] = "Lịch đặt này không thuộc tài khoản của bạn hoặc đã quá thời hạn hủy.";
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Không thể hủy đặt phòng {BookingId} cho sinh viên {StudentId}", input.BookingId, StudentId());
            TempData["Error"] = "Không thể hủy đặt phòng lúc này. Vui lòng thử lại.";
        }
        return RedirectToAction(nameof(MyBookings));
    }

    private int StudentId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

[ApiController]
[Authorize(Roles = "STUDENT")]
[Route("api/student/rooms")]
public sealed class StudentRoomsApiController(StudentBookingService bookings) : ControllerBase
{
    [HttpGet("available")]
    public async Task<IActionResult> Available([FromQuery] AvailabilityQuery query)
    {
        if (query.Date is null || query.Date == DateOnly.MaxValue ||
            query.Date < DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)) ||
            query.Floor < 0 || query.Capacity < 1 || query.Building?.Length > 100)
            return BadRequest(new { message = "Chọn ngày đặt chưa qua, tầng và sức chứa phù hợp." });

        return Ok(await bookings.GetAvailableRoomsAsync(query));
    }
}
