using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Hubs;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Controllers;

[Authorize(Roles = "ADMIN")]
public sealed class AdminController(
    RoomRepository rooms,
    ReputationService reputationService,
    IHubContext<OccupancyHub> occupancyHub,
    ILogger<AdminController> logger) : Controller
{
    private int AccountId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("/admin/rooms")]
    public async Task<IActionResult> ManageRooms(string? filter, string? statusFilter)
    {
        logger.LogInformation("ManageRooms called with filter={Filter}, statusFilter={StatusFilter}", filter, statusFilter);
        
        var roomsList = await rooms.GetAllRoomsAsync(filter, statusFilter);
        
        logger.LogInformation("Found {Count} rooms", roomsList.Count);
        
        var viewModel = new RoomListViewModel
        {
            Rooms = roomsList,
            Filter = filter,
            StatusFilter = statusFilter
        };
        return View("ManageRooms", viewModel);
    }

    [HttpGet("/admin/rooms/create")]
    public IActionResult CreateRoom()
    {
        return View("RoomForm", new RoomViewModel { RoomStatus = "AVAILABLE" });
    }

    [HttpPost("/admin/rooms/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRoom(RoomViewModel input)
    {
        if (!ModelState.IsValid)
            return View("RoomForm", input);
        
        try
        {
            var roomId = await rooms.CreateRoomAsync(input, AccountId());
            
            // Ghi audit log
            await LogAuditAsync("CREATE_ROOM", $"Tạo phòng {input.RoomCode}");
            
            TempData["Success"] = $"Đã tạo phòng {input.RoomCode} thành công.";
            return RedirectToAction(nameof(ManageRooms));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            ModelState.AddModelError(nameof(input.RoomCode), "Mã phòng này đã tồn tại.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Không thể tạo phòng");
            ModelState.AddModelError("", "Không thể tạo phòng. Vui lòng thử lại.");
        }
        return View("RoomForm", input);
    }

    [HttpGet("/admin/rooms/edit/{roomId}")]
    public async Task<IActionResult> EditRoom(int roomId)
    {
        var room = await rooms.GetRoomByIdAsync(roomId);
        if (room == null)
            return NotFound();
        
        return View("RoomForm", room);
    }

    [HttpPost("/admin/rooms/edit/{roomId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRoom(int roomId, RoomViewModel input)
    {
        input.RoomId = roomId;
        
        if (!ModelState.IsValid)
            return View("RoomForm", input);
        
        try
        {
            await rooms.UpdateRoomAsync(input, AccountId());
            
            // Ghi audit log
            await LogAuditAsync("UPDATE_ROOM", $"Cập nhật phòng {input.RoomCode}");
            
            // Thông báo qua SignalR về thay đổi phòng
            await occupancyHub.Clients.All.SendAsync("RoomUpdated", roomId);
            
            TempData["Success"] = $"Đã cập nhật phòng {input.RoomCode} thành công.";
            return RedirectToAction(nameof(ManageRooms));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            ModelState.AddModelError(nameof(input.RoomCode), "Mã phòng này đã tồn tại.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Không thể cập nhật phòng {RoomId}", roomId);
            ModelState.AddModelError("", "Không thể cập nhật phòng. Vui lòng thử lại.");
        }
        return View("RoomForm", input);
    }

    [HttpPost("/admin/rooms/delete/{roomId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRoom(int roomId)
    {
        try
        {
            var room = await rooms.GetRoomByIdAsync(roomId);
            if (room == null)
                return NotFound();
            
            await rooms.DeleteRoomAsync(roomId);
            
            // Ghi audit log
            await LogAuditAsync("DELETE_ROOM", $"Xóa phòng {room.RoomCode}");
            
            TempData["Success"] = $"Đã xóa phòng {room.RoomCode} thành công.";
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Không thể xóa phòng {RoomId}", roomId);
            TempData["Error"] = "Không thể xóa phòng. Phòng có thể đang có booking hoặc dữ liệu liên quan.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Lỗi khi xóa phòng {RoomId}", roomId);
            TempData["Error"] = "Đã xảy ra lỗi. Vui lòng thử lại.";
        }
        
        return RedirectToAction(nameof(ManageRooms));
    }

    // ==================== THỐNG KÊ SỬ DỤNG ====================
    
    [HttpGet("/admin/statistics")]
    public async Task<IActionResult> UsageStatistics(string? startDate, string? endDate)
    {
        DateTime? start = null;
        DateTime? end = null;
        
        if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var parsedStart))
            start = parsedStart;
        if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var parsedEnd))
            end = parsedEnd;
        
        if (start.HasValue && end.HasValue)
        {
            var statistics = await rooms.GetUsageStatisticsAsync(start, end);
            var floorStats = await rooms.GetFloorStatisticsAsync(start, end);
            
            ViewBag.Statistics = statistics;
            ViewBag.FloorStatistics = floorStats;
        }
        
        ViewBag.StartDate = startDate;
        ViewBag.EndDate = endDate;
        
        return View("UsageStatistics");
    }

    // ==================== DUYỆT SỰ CỐ ====================
    
    [HttpGet("/admin/incidents")]
    public async Task<IActionResult> ManageIncidents()
    {
        var incidents = await rooms.GetPendingIncidentsAsync();
        return View("ManageIncidents", incidents);
    }

    [HttpGet("/admin/incidents/{incidentId}")]
    public async Task<IActionResult> ReviewIncident(long incidentId)
    {
        var incident = await rooms.GetIncidentByIdAsync(incidentId);
        if (incident == null)
            return NotFound();
        
        return View("ReviewIncident", incident);
    }

    [HttpPost("/admin/incidents/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveIncident(long incidentId, string? decision, int pointsDeducted = 0, string? adminNotes = null)
    {
        logger.LogInformation("=== ApproveIncident POST Start ===");
        logger.LogInformation("incidentId: {Id}", incidentId);
        logger.LogInformation("decision: {Decision}", decision ?? "NULL");
        logger.LogInformation("pointsDeducted: {Points}", pointsDeducted);
        logger.LogInformation("adminNotes: {Notes}", adminNotes ?? "NULL");
        logger.LogInformation("ModelState.IsValid: {IsValid}", ModelState.IsValid);
        
        // Log all form values
        foreach (var key in Request.Form.Keys)
        {
            logger.LogInformation("Form[{Key}] = {Value}", key, Request.Form[key]);
        }
        
        if (incidentId <= 0)
        {
            logger.LogWarning("Invalid incidentId: {Id}", incidentId);
            TempData["Error"] = "ID sự cố không hợp lệ";
            return RedirectToAction(nameof(ManageIncidents));
        }
        
        if (string.IsNullOrEmpty(decision))
        {
            logger.LogWarning("Decision is null or empty");
            TempData["Error"] = "Vui lòng chọn quyết định (Chấp nhận hoặc Từ chối)";
            return RedirectToAction("ReviewIncident", new { incidentId });
        }
        
        // Map APPROVED → CONFIRMED (database constraint uses CONFIRMED not APPROVED)
        if (decision == "APPROVED")
            decision = "CONFIRMED";
        
        if (decision != "CONFIRMED" && decision != "REJECTED")
        {
            logger.LogWarning("Invalid decision value: {Decision}", decision);
            TempData["Error"] = "Giá trị quyết định không hợp lệ";
            return RedirectToAction("ReviewIncident", new { incidentId });
        }
        
        try
        {
            var incident = await rooms.GetIncidentByIdAsync(incidentId);
            if (incident == null)
            {
                logger.LogWarning("Incident {IncidentId} not found", incidentId);
                TempData["Error"] = "Không tìm thấy sự cố";
                return RedirectToAction(nameof(ManageIncidents));
            }
            
            // Cast dynamic properties for logging
            long bookingId = (long)incident.BookingId;
            string assetType = incident.AssetType?.ToString() ?? "unknown";
            string currentStatus = incident.ReviewStatus?.ToString() ?? "unknown";
            
            logger.LogInformation("Found incident: BookingId={BookingId}, AssetType={AssetType}, CurrentStatus={Status}", 
                bookingId, assetType, currentStatus);
            
            if (currentStatus != "PENDING")
            {
                logger.LogWarning("Incident {IncidentId} is not PENDING (status: {Status})", incidentId, currentStatus);
                TempData["Error"] = "Sự cố này đã được xử lý trước đó";
                return RedirectToAction(nameof(ManageIncidents));
            }
            
            // Cập nhật review_status
            logger.LogInformation("Calling UpdateIncidentStatusAsync...");
            await rooms.UpdateIncidentStatusAsync(incidentId, decision, AccountId(), adminNotes, pointsDeducted);
            logger.LogInformation("✓ Updated incident status to {Decision}", decision);
            
            // Ghi audit log
            await LogAuditAsync("REVIEW_INCIDENT", 
                $"Duyệt sự cố #{incidentId}: {decision}");
            
            // Nếu CONFIRMED và có điểm trừ → gọi service trừ điểm
            if (decision == "CONFIRMED" && pointsDeducted > 0 && bookingId > 0)
            {
                try
                {
                    logger.LogInformation("Deducting {Points} points for booking {BookingId}", pointsDeducted, bookingId);
                    
                    await reputationService.DeductPointsForMissingAssetAsync(
                        (int)bookingId,
                        assetType,
                        adminNotes ?? "Admin xác nhận sự cố tài sản");
                    
                    TempData["Success"] = $"Đã duyệt sự cố và trừ {pointsDeducted} điểm uy tín sinh viên.";
                    logger.LogInformation("✓ Points deducted successfully");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Không thể trừ điểm cho incident {IncidentId}", incidentId);
                    TempData["Warning"] = "Đã duyệt sự cố nhưng không thể trừ điểm uy tín.";
                }
            }
            else
            {
                TempData["Success"] = $"Đã {(decision == "CONFIRMED" ? "chấp nhận" : "từ chối")} sự cố #{incidentId}.";
            }
            
            logger.LogInformation("=== ApproveIncident Success - Redirecting to ManageIncidents ===");
            return RedirectToAction(nameof(ManageIncidents));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Lỗi khi duyệt sự cố {IncidentId}", incidentId);
            TempData["Error"] = "Không thể duyệt sự cố. Vui lòng thử lại.";
            
            return RedirectToAction(nameof(ReviewIncident), new { incidentId });
        }
    }

    // ==================== PHÒNG ĐANG SỬ DỤNG (SignalR) ====================
    
    [HttpGet("/admin/occupied-rooms")]
    public async Task<IActionResult> OccupiedRooms()
    {
        var occupiedRooms = await rooms.GetOccupiedRoomsAsync();
        return View("OccupiedRooms", occupiedRooms);
    }

    // API endpoint cho SignalR
    [HttpGet("/admin/api/occupied-rooms")]
    public async Task<IActionResult> GetOccupiedRoomsApi()
    {
        var occupiedRooms = await rooms.GetOccupiedRoomsAsync();
        return Json(occupiedRooms);
    }

    // ==================== AUDIT LOG ====================
    
    private async Task LogAuditAsync(string actionType, string actionDescription)
    {
        try
        {
            await using var connection = new SqlConnection(
                HttpContext.RequestServices.GetRequiredService<IConfiguration>()
                    .GetConnectionString("AccommodationDb"));
            await connection.OpenAsync();
            
            await using var command = new SqlCommand("""
                INSERT INTO dbo.AUDIT_LOG (performed_by_account_id, action_type, target_entity, target_key, before_summary, after_summary)
                VALUES (@accountId, @actionType, 'ADMIN_ACTION', '', NULL, @actionDescription)
                """, connection);
            
            command.Parameters.AddWithValue("@accountId", AccountId());
            command.Parameters.AddWithValue("@actionType", actionType);
            command.Parameters.AddWithValue("@actionDescription", actionDescription);
            
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không thể ghi audit log");
        }
    }
}
