using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models.Facilities;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Controllers;

/// <summary>
/// Controller trung tâm cho Người 3 - Nhân viên cơ sở vật chất (FACILITIES_STAFF).
/// Chịu trách nhiệm:
/// 1. Hiển thị Dashboard theo layout wireframe chuyên nghiệp (left rail, sidebar, tabs, main content, right sidebar, bottom bar).
/// 2. CRUD thiết bị phòng (ROOM_ASSET).
/// 3. Cập nhật tình trạng & trạng thái hoạt động (bảo trì, phục hồi, thanh lý).
/// 4. Xem sự cố thiết bị do bảo vệ báo cáo và ghi nhận xử lý kỹ thuật.
/// 5. Tự động ghi nhật ký AUDIT_LOG cho mọi hành động.
/// </summary>
[Authorize(Roles = "FACILITIES_STAFF")]
[Route("facilities")]
public class FacilitiesController : Controller
{
    private readonly IFacilitiesAssetRepository _repo;
    private readonly ILogger<FacilitiesController> _logger;

    public FacilitiesController(IFacilitiesAssetRepository repo, ILogger<FacilitiesController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    /// <summary>
    /// Trang Dashboard chính của Nhân viên cơ sở vật chất.
    /// Hỗ trợ chuyển tab ('assets': Thiết bị, 'incidents': Sự cố, 'logs': Nhật ký) và các bộ lọc tìm kiếm.
    /// </summary>
    [HttpGet("")]
    [HttpGet("index")]
    public async Task<IActionResult> Index(
        string tab = "assets",
        string? search = null,
        string? building = null,
        int? roomId = null,
        string? condition = null,
        string? status = null,
        string? incidentType = null,
        string? reviewStatus = null,
        int page = 1)
    {
        var staffId = GetCurrentAccountId();
        var staff = await _repo.GetStaffProfileAsync(staffId);

        var model = new FacilitiesDashboardViewModel
        {
            ActiveTab = string.IsNullOrWhiteSpace(tab) ? "assets" : tab.ToLower(),
            StaffName = staff?.Account?.FullName ?? User.Identity?.Name ?? "Nhân viên CSVC",
            EmployeeCode = staff?.EmployeeCode ?? "FS-UNKNOWN",
            Department = staff?.Department ?? "Bộ phận Cơ sở vật chất",
            AssignedArea = staff?.AssignedArea ?? "Toàn khu ký túc xá",
            Stats = await _repo.GetOverviewStatsAsync(),
            Rooms = await _repo.GetActiveRoomsAsync(),
            Buildings = await _repo.GetBuildingsAsync()
        };

        // Thiết lập bộ lọc thiết bị
        model.AssetFilter = new AssetFilterCriteria
        {
            Search = search,
            Building = building,
            RoomId = roomId,
            Condition = condition,
            Status = status,
            PageIndex = page,
            PageSize = 8
        };

        // Thiết lập bộ lọc sự cố
        model.IncidentFilter = new IncidentFilterCriteria
        {
            Search = search,
            IncidentType = incidentType,
            ReviewStatus = reviewStatus,
            RoomId = roomId,
            PageIndex = page,
            PageSize = 8
        };

        // Tải dữ liệu tương ứng theo tab
        if (model.ActiveTab == "incidents")
        {
            model.IncidentsPaged = await _repo.GetIncidentsAsync(model.IncidentFilter);
        }
        else if (model.ActiveTab == "logs")
        {
            model.RecentAuditLogs = await _repo.GetRecentAuditLogsAsync(25);
        }
        else
        {
            // Mặc định tab 'assets'
            model.ActiveTab = "assets";
            model.AssetsPaged = await _repo.GetAssetsAsync(model.AssetFilter);
        }

        return View(model);
    }

    /// <summary>
    /// Thêm mới thiết bị vào phòng.
    /// Tự động kiểm tra trùng mã tem (asset_tag) và ghi AUDIT_LOG.
    /// </summary>
    [HttpPost("create-asset")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAsset(AssetCreateInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vui lòng nhập đầy đủ các thông tin bắt buộc khi thêm thiết bị.";
            return RedirectToAction(nameof(Index), new { tab = "assets" });
        }

        try
        {
            var staffId = GetCurrentAccountId();
            var newId = await _repo.CreateAssetAsync(input, staffId);
            TempData["Success"] = $"Đã thêm thành công thiết bị '{input.AssetName}' (Mã tem: {input.AssetTag}).";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi thêm thiết bị mới vào phòng {RoomId}", input.RoomId);
            TempData["Error"] = "Có lỗi xảy ra trong quá trình lưu thiết bị. Vui lòng thử lại.";
        }

        return RedirectToAction(nameof(Index), new { tab = "assets", roomId = input.RoomId });
    }

    /// <summary>
    /// Cập nhật thông tin thiết bị (tên, mã tem, chuyển phòng).
    /// </summary>
    [HttpPost("edit-asset")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAsset(AssetEditInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Dữ liệu chỉnh sửa không hợp lệ.";
            return RedirectToAction(nameof(Index), new { tab = "assets" });
        }

        try
        {
            var staffId = GetCurrentAccountId();
            var success = await _repo.UpdateAssetAsync(input, staffId);
            if (success)
            {
                TempData["Success"] = $"Đã cập nhật thông tin thiết bị '{input.AssetName}'.";
            }
            else
            {
                TempData["Error"] = "Không tìm thấy thiết bị cần cập nhật.";
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi cập nhật thiết bị ID {AssetId}", input.AssetId);
            TempData["Error"] = "Không thể cập nhật thiết bị. Vui lòng kiểm tra lại.";
        }

        return RedirectToAction(nameof(Index), new { tab = "assets" });
    }

    /// <summary>
    /// Cập nhật nhanh tình trạng kỹ thuật và trạng thái hoạt động (bảo trì, phục hồi).
    /// </summary>
    [HttpPost("update-status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAssetStatus(AssetStatusUpdateInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thông tin trạng thái không hợp lệ.";
            return RedirectToAction(nameof(Index), new { tab = "assets" });
        }

        try
        {
            var staffId = GetCurrentAccountId();
            var success = await _repo.UpdateAssetStatusAsync(input, staffId);
            if (success)
            {
                TempData["Success"] = "Đã cập nhật tình trạng và trạng thái thiết bị thành công.";
            }
            else
            {
                TempData["Error"] = "Không tìm thấy thiết bị để cập nhật.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi cập nhật trạng thái thiết bị ID {AssetId}", input.AssetId);
            TempData["Error"] = "Có lỗi xảy ra khi cập nhật trạng thái thiết bị.";
        }

        return RedirectToAction(nameof(Index), new { tab = "assets" });
    }

    /// <summary>
    /// Thanh lý hoặc xóa thiết bị.
    /// Nếu có lịch sử kiểm tra phòng, tự động chuyển trạng thái sang RETIRED để đảm bảo toàn vẹn dữ liệu.
    /// </summary>
    [HttpPost("retire-or-delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetireOrDeleteAsset(long assetId)
    {
        try
        {
            var staffId = GetCurrentAccountId();
            var success = await _repo.RetireOrDeleteAssetAsync(assetId, staffId);
            if (success)
            {
                TempData["Success"] = "Thao tác thanh lý / xóa thiết bị thành công.";
            }
            else
            {
                TempData["Error"] = "Không tìm thấy thiết bị.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi thanh lý/xóa thiết bị ID {AssetId}", assetId);
            TempData["Error"] = "Không thể xóa hoặc thanh lý thiết bị này do ràng buộc dữ liệu.";
        }

        return RedirectToAction(nameof(Index), new { tab = "assets" });
    }

    /// <summary>
    /// API trả về chi tiết sự cố dạng JSON để hiển thị lên Modal xem chi tiết / xử lý kỹ thuật.
    /// </summary>
    [HttpGet("incident-detail/{id}")]
    public async Task<IActionResult> GetIncidentDetail(long id)
    {
        var incident = await _repo.GetIncidentDetailAsync(id);
        if (incident == null) return NotFound(new { message = "Không tìm thấy sự cố." });
        return Json(incident);
    }

    /// <summary>
    /// Xử lý kỹ thuật / bảo dưỡng thiết bị từ sự cố được báo cáo.
    /// Cập nhật registered_condition và asset_status tương ứng và lưu AUDIT_LOG.
    /// </summary>
    [HttpPost("process-incident")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessIncident(ProcessIncidentInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vui lòng chọn hình thức xử lý hợp lệ.";
            return RedirectToAction(nameof(Index), new { tab = "incidents" });
        }

        try
        {
            var staffId = GetCurrentAccountId();
            var success = await _repo.ProcessIncidentAsync(input, staffId);
            if (success)
            {
                TempData["Success"] = "Đã ghi nhận phương án xử lý kỹ thuật và cập nhật trạng thái thiết bị.";
            }
            else
            {
                TempData["Error"] = "Không tìm thấy thiết bị liên quan đến sự cố.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xử lý sự cố thiết bị ID {IncidentId}", input.IncidentId);
            TempData["Error"] = "Có lỗi xảy ra trong quá trình ghi nhận xử lý sự cố.";
        }

        return RedirectToAction(nameof(Index), new { tab = "incidents" });
    }

    /// <summary>
    /// Lấy ID tài khoản của người dùng đang đăng nhập từ Cookie Claims.
    /// </summary>
    private int GetCurrentAccountId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(claim, out var id)) return id;
        throw new InvalidOperationException("Không thể xác định tài khoản đăng nhập hiện tại.");
    }
}

