using Microsoft.EntityFrameworkCore;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Helpers;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models.Entities;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models.Facilities;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;

/// <summary>
/// Triển khai Repository sử dụng EF Core và LINQ để phục vụ toàn bộ chức năng của Người 3 (Nhân viên cơ sở vật chất).
/// Tuân thủ quy tắc phân quyền:
/// - Toàn quyền ghi trên ROOM_ASSET và AUDIT_LOG.
/// - Chỉ đọc trên ROOM, INSPECTION, INSPECTION_ITEM, FACILITIES_STAFF.
/// - Không can thiệp vào cột reviewed_by_admin_id của ASSET_INCIDENT (do Admin Người 4 duyệt).
/// </summary>
public class FacilitiesAssetRepository : IFacilitiesAssetRepository
{
    private readonly AccommodationDbContext _db;

    public FacilitiesAssetRepository(AccommodationDbContext db)
    {
        _db = db;
    }

    #region 1. THỐNG KÊ TỔNG QUAN
    /// <summary>
    /// Tính toán các chỉ số thống kê phục vụ cho thanh Right Sidebar (Quick Stats).
    /// </summary>
    public async Task<FacilitiesOverviewStats> GetOverviewStatsAsync()
    {
        var stats = new FacilitiesOverviewStats();

        // Thống kê thiết bị
        stats.TotalAssets = await _db.RoomAssets.CountAsync();
        stats.ActiveAssets = await _db.RoomAssets.CountAsync(a => a.AssetStatus == "ACTIVE");
        stats.MaintenanceAssets = await _db.RoomAssets.CountAsync(a => a.AssetStatus == "MAINTENANCE");
        stats.RetiredAssets = await _db.RoomAssets.CountAsync(a => a.AssetStatus == "RETIRED");
        stats.MissingAssets = await _db.RoomAssets.CountAsync(a => a.AssetStatus == "MISSING");
        stats.DamagedAssets = await _db.RoomAssets.CountAsync(a => a.RegisteredCondition == "DAMAGED");

        // Thống kê sự cố
        stats.TotalIncidents = await _db.AssetIncidents.CountAsync();
        stats.PendingIncidents = await _db.AssetIncidents.CountAsync(i => i.ReviewStatus == "PENDING");

        return stats;
    }
    #endregion

    #region 2. QUẢN LÝ THIẾT BỊ (ROOM_ASSET)
    /// <summary>
    /// Lấy danh sách thiết bị có phân trang và bộ lọc đa tiêu chí (từ khóa, tòa nhà, phòng, tình trạng, trạng thái).
    /// </summary>
    public async Task<PagedResult<RoomAssetDto>> GetAssetsAsync(AssetFilterCriteria filter)
    {
        var query = _db.RoomAssets
            .AsNoTracking()
            .Include(a => a.Room)
            .Include(a => a.CreatedByStaff)
            .ThenInclude(s => s != null ? s.Account : null)
            .Include(a => a.UpdatedByStaff)
            .ThenInclude(s => s != null ? s.Account : null)
            .AsQueryable();

        // 1. Lọc theo từ khóa tìm kiếm (mã tem hoặc tên thiết bị)
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(a => a.AssetTag.ToLower().Contains(search) || a.AssetName.ToLower().Contains(search));
        }

        // 2. Lọc theo tòa nhà
        if (!string.IsNullOrWhiteSpace(filter.Building))
        {
            query = query.Where(a => a.Room != null && a.Room.Building == filter.Building);
        }

        // 3. Lọc theo phòng cụ thể
        if (filter.RoomId.HasValue && filter.RoomId.Value > 0)
        {
            query = query.Where(a => a.RoomId == filter.RoomId.Value);
        }

        // 4. Lọc theo tình trạng (GOOD, WORN, DAMAGED)
        if (!string.IsNullOrWhiteSpace(filter.Condition))
        {
            query = query.Where(a => a.RegisteredCondition == filter.Condition);
        }

        // 5. Lọc theo trạng thái hoạt động (ACTIVE, MAINTENANCE, RETIRED, MISSING)
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(a => a.AssetStatus == filter.Status);
        }

        var totalItems = await query.CountAsync();
        var pageIndex = filter.PageIndex > 0 ? filter.PageIndex : 1;
        var pageSize = filter.PageSize > 0 ? filter.PageSize : 10;

        var items = await query
            .OrderByDescending(a => a.AssetId)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new RoomAssetDto
            {
                AssetId = a.AssetId,
                RoomId = a.RoomId,
                RoomCode = a.Room != null ? a.Room.RoomCode : "N/A",
                Building = a.Room != null ? a.Room.Building : "N/A",
                Floor = a.Room != null ? a.Room.Floor : (short)0,
                AssetTag = a.AssetTag,
                AssetName = a.AssetName,
                RegisteredCondition = a.RegisteredCondition,
                AssetStatus = a.AssetStatus,
                CreatedByStaffName = a.CreatedByStaff != null && a.CreatedByStaff.Account != null 
                    ? a.CreatedByStaff.Account.FullName 
                    : "Hệ thống",
                UpdatedByStaffName = a.UpdatedByStaff != null && a.UpdatedByStaff.Account != null 
                    ? a.UpdatedByStaff.Account.FullName 
                    : null,
                IncidentCount = a.InspectionItems.Count(i => i.Incident != null)
            })
            .ToListAsync();

        // Khắc phục lỗi font chữ Mojibake tiếng Việt
        foreach (var item in items)
        {
            item.CreatedByStaffName = TextHelper.FixEncoding(item.CreatedByStaffName);
            item.UpdatedByStaffName = TextHelper.FixEncoding(item.UpdatedByStaffName);
        }

        return new PagedResult<RoomAssetDto>
        {
            Items = items,
            TotalItems = totalItems,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Lấy chi tiết entity RoomAsset theo ID.
    /// </summary>
    public async Task<RoomAsset?> GetAssetByIdAsync(long assetId)
    {
        return await _db.RoomAssets
            .Include(a => a.Room)
            .FirstOrDefaultAsync(a => a.AssetId == assetId);
    }

    /// <summary>
    /// Kiểm tra trùng lặp mã tem thiết bị (asset_tag) để đảm bảo ràng buộc UQ_ROOM_ASSET_tag.
    /// </summary>
    public async Task<bool> IsAssetTagExistsAsync(string assetTag, long? excludeAssetId = null)
    {
        var tagNormalized = assetTag.Trim().ToUpper();
        if (excludeAssetId.HasValue)
        {
            return await _db.RoomAssets.AnyAsync(a => a.AssetId != excludeAssetId.Value && a.AssetTag.ToUpper() == tagNormalized);
        }
        return await _db.RoomAssets.AnyAsync(a => a.AssetTag.ToUpper() == tagNormalized);
    }

    /// <summary>
    /// Thêm mới một thiết bị vào phòng.
    /// Tự động gán created_by_staff_id và ghi nhật ký vào AUDIT_LOG.
    /// </summary>
    public async Task<long> CreateAssetAsync(AssetCreateInput input, int staffAccountId)
    {
        // 1. Kiểm tra mã tem trùng lặp
        if (await IsAssetTagExistsAsync(input.AssetTag))
        {
            throw new InvalidOperationException($"Mã tem '{input.AssetTag}' đã tồn tại trong hệ thống. Vui lòng nhập mã tem khác.");
        }

        // 2. Tạo entity RoomAsset
        var asset = new RoomAsset
        {
            RoomId = input.RoomId,
            AssetTag = input.AssetTag.Trim(),
            AssetName = input.AssetName.Trim(),
            RegisteredCondition = input.RegisteredCondition,
            AssetStatus = input.AssetStatus,
            CreatedByStaffId = staffAccountId,
            UpdatedByStaffId = null,
            LastConfiguredByAdminId = null
        };

        _db.RoomAssets.Add(asset);
        await _db.SaveChangesAsync();

        // 3. Ghi vết vào AUDIT_LOG
        await LogAuditAsync(
            staffAccountId,
            "CREATE_ASSET",
            asset.AssetId.ToString(),
            before: null,
            after: $"Thêm thiết bị '{asset.AssetName}' (Tem: {asset.AssetTag}, Tình trạng: {asset.RegisteredCondition}, Trạng thái: {asset.AssetStatus}) vào phòng ID {asset.RoomId}"
        );

        return asset.AssetId;
    }

    /// <summary>
    /// Chỉnh sửa thông tin thiết bị (tên, mã tem, phòng điều chuyển).
    /// Ghi nhận updated_by_staff_id và lưu vết thay đổi vào AUDIT_LOG.
    /// </summary>
    public async Task<bool> UpdateAssetAsync(AssetEditInput input, int staffAccountId)
    {
        var asset = await _db.RoomAssets.FindAsync(input.AssetId);
        if (asset == null) return false;

        // Kiểm tra nếu mã tem bị đổi và trùng với thiết bị khác
        if (!asset.AssetTag.Equals(input.AssetTag, StringComparison.OrdinalIgnoreCase))
        {
            if (await IsAssetTagExistsAsync(input.AssetTag, asset.AssetId))
            {
                throw new InvalidOperationException($"Mã tem '{input.AssetTag}' đã được sử dụng cho thiết bị khác.");
            }
        }

        var beforeSummary = $"Phòng: {asset.RoomId}, Tên: '{asset.AssetName}', Tem: '{asset.AssetTag}', Tình trạng: {asset.RegisteredCondition}, Trạng thái: {asset.AssetStatus}";

        // Cập nhật thông tin
        asset.RoomId = input.RoomId;
        asset.AssetTag = input.AssetTag.Trim();
        asset.AssetName = input.AssetName.Trim();
        asset.RegisteredCondition = input.RegisteredCondition;
        asset.AssetStatus = input.AssetStatus;
        asset.UpdatedByStaffId = staffAccountId;

        await _db.SaveChangesAsync();

        var afterSummary = $"Phòng: {asset.RoomId}, Tên: '{asset.AssetName}', Tem: '{asset.AssetTag}', Tình trạng: {asset.RegisteredCondition}, Trạng thái: {asset.AssetStatus}";

        // Ghi AUDIT_LOG
        await LogAuditAsync(staffAccountId, "UPDATE_ASSET", asset.AssetId.ToString(), beforeSummary, afterSummary);
        return true;
    }

    /// <summary>
    /// Cập nhật nhanh tình trạng và trạng thái hoạt động (bảo trì, sửa chữa, phục hồi).
    /// </summary>
    public async Task<bool> UpdateAssetStatusAsync(AssetStatusUpdateInput input, int staffAccountId)
    {
        var asset = await _db.RoomAssets.FindAsync(input.AssetId);
        if (asset == null) return false;

        var beforeSummary = $"Tình trạng: {asset.RegisteredCondition}, Trạng thái: {asset.AssetStatus}";

        asset.RegisteredCondition = input.RegisteredCondition;
        asset.AssetStatus = input.AssetStatus;
        asset.UpdatedByStaffId = staffAccountId;

        await _db.SaveChangesAsync();

        var afterSummary = $"Tình trạng: {asset.RegisteredCondition}, Trạng thái: {asset.AssetStatus}" +
                           (!string.IsNullOrWhiteSpace(input.Note) ? $" (Ghi chú: {input.Note})" : "");

        await LogAuditAsync(staffAccountId, "UPDATE_STATUS", asset.AssetId.ToString(), beforeSummary, afterSummary);
        return true;
    }

    /// <summary>
    /// Thanh lý hoặc xóa thiết bị:
    /// Nếu thiết bị đã có trong biên bản kiểm tra (INSPECTION_ITEM), không thể xóa cứng (dính FK),
    /// hệ thống sẽ tự động chuyển sang trạng thái thanh lý (RETIRED) để bảo đảm toàn vẹn dữ liệu.
    /// Nếu chưa từng kiểm tra, xóa hoàn toàn khỏi cơ sở dữ liệu.
    /// </summary>
    public async Task<bool> RetireOrDeleteAssetAsync(long assetId, int staffAccountId)
    {
        var asset = await _db.RoomAssets
            .Include(a => a.InspectionItems)
            .FirstOrDefaultAsync(a => a.AssetId == assetId);

        if (asset == null) return false;

        if (asset.InspectionItems.Any())
        {
            // Có lịch sử kiểm tra -> Chuyển sang thanh lý (RETIRED)
            var before = $"Trạng thái: {asset.AssetStatus}";
            asset.AssetStatus = "RETIRED";
            asset.UpdatedByStaffId = staffAccountId;
            await _db.SaveChangesAsync();

            await LogAuditAsync(staffAccountId, "RETIRE_ASSET", asset.AssetId.ToString(), before, "Chuyển sang trạng thái thanh lý (RETIRED) do có lịch sử kiểm tra.");
            return true;
        }

        // Chưa có lịch sử -> Cho phép xóa hoàn toàn
        var deleteSummary = $"Xóa thiết bị '{asset.AssetName}' (Tem: {asset.AssetTag}) khỏi phòng {asset.RoomId}";
        _db.RoomAssets.Remove(asset);
        await _db.SaveChangesAsync();

        await LogAuditAsync(staffAccountId, "DELETE_ASSET", assetId.ToString(), deleteSummary, "Đã xóa khỏi hệ thống");
        return true;
    }
    #endregion

    #region 3. TRUY VẤN PHÒNG (ROOM - READ ONLY)
    /// <summary>
    /// Lấy danh sách các phòng khả dụng để hiển thị trong dropdown chọn phòng hoặc bộ lọc.
    /// </summary>
    public async Task<List<RoomLookupDto>> GetActiveRoomsAsync()
    {
        return await _db.Rooms
            .AsNoTracking()
            .OrderBy(r => r.Building)
            .ThenBy(r => r.Floor)
            .ThenBy(r => r.RoomCode)
            .Select(r => new RoomLookupDto
            {
                RoomId = r.RoomId,
                RoomCode = r.RoomCode,
                Building = r.Building,
                Floor = r.Floor,
                Capacity = r.Capacity,
                RoomStatus = r.RoomStatus,
                AssetCount = r.Assets.Count
            })
            .ToListAsync();
    }

    /// <summary>
    /// Lấy danh sách tên tất cả các tòa nhà để phục vụ bộ lọc.
    /// </summary>
    public async Task<List<string>> GetBuildingsAsync()
    {
        return await _db.Rooms
            .AsNoTracking()
            .Select(r => r.Building)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync();
    }
    #endregion

    #region 4. SỰ CỐ THIẾT BỊ (ASSET_INCIDENT)
    /// <summary>
    /// Lấy danh sách sự cố thiết bị do bảo vệ báo cáo (từ biên bản kiểm tra).
    /// </summary>
    public async Task<PagedResult<IncidentItemDto>> GetIncidentsAsync(IncidentFilterCriteria filter)
    {
        var query = _db.AssetIncidents
            .AsNoTracking()
            .Include(i => i.InspectionItem)
            .ThenInclude(ii => ii != null ? ii.Asset : null)
            .ThenInclude(a => a != null ? a.Room : null)
            .Include(i => i.InspectionItem)
            .ThenInclude(ii => ii != null ? ii.Inspection : null)
            .ThenInclude(ins => ins != null ? ins.GuardAccount : null)
            .Include(i => i.ReviewedByAdmin)
            .AsQueryable();

        // 1. Lọc theo từ khóa (mã tem, tên thiết bị, ghi chú)
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(i =>
                (i.InspectionItem != null && i.InspectionItem.Asset != null &&
                    (i.InspectionItem.Asset.AssetTag.ToLower().Contains(search) ||
                     i.InspectionItem.Asset.AssetName.ToLower().Contains(search))) ||
                (i.IncidentNotes != null && i.IncidentNotes.ToLower().Contains(search)));
        }

        // 2. Lọc theo loại sự cố (DAMAGED / MISSING)
        if (!string.IsNullOrWhiteSpace(filter.IncidentType))
        {
            query = query.Where(i => i.IncidentType == filter.IncidentType);
        }

        // 3. Lọc theo trạng thái xem xét của Admin (PENDING, CONFIRMED, REJECTED)
        if (!string.IsNullOrWhiteSpace(filter.ReviewStatus))
        {
            query = query.Where(i => i.ReviewStatus == filter.ReviewStatus);
        }

        // 4. Lọc theo phòng
        if (filter.RoomId.HasValue && filter.RoomId.Value > 0)
        {
            query = query.Where(i => i.InspectionItem != null && 
                                     i.InspectionItem.Asset != null && 
                                     i.InspectionItem.Asset.RoomId == filter.RoomId.Value);
        }

        var totalItems = await query.CountAsync();
        var pageIndex = filter.PageIndex > 0 ? filter.PageIndex : 1;
        var pageSize = filter.PageSize > 0 ? filter.PageSize : 10;

        var items = await query
            .OrderByDescending(i => i.ReportedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new IncidentItemDto
            {
                IncidentId = i.IncidentId,
                InspectionItemId = i.InspectionItemId,
                InspectionId = i.InspectionItem != null ? i.InspectionItem.InspectionId : 0,
                BookingId = (i.InspectionItem != null && i.InspectionItem.Inspection != null) ? i.InspectionItem.Inspection.BookingId : 0,
                AssetId = i.InspectionItem != null ? i.InspectionItem.AssetId : 0,
                AssetTag = (i.InspectionItem != null && i.InspectionItem.Asset != null) ? i.InspectionItem.Asset.AssetTag : "N/A",
                AssetName = (i.InspectionItem != null && i.InspectionItem.Asset != null) ? i.InspectionItem.Asset.AssetName : "N/A",
                RoomCode = (i.InspectionItem != null && i.InspectionItem.Asset != null && i.InspectionItem.Asset.Room != null) 
                    ? i.InspectionItem.Asset.Room.RoomCode 
                    : "N/A",
                Building = (i.InspectionItem != null && i.InspectionItem.Asset != null && i.InspectionItem.Asset.Room != null) 
                    ? i.InspectionItem.Asset.Room.Building 
                    : "N/A",
                CurrentAssetStatus = (i.InspectionItem != null && i.InspectionItem.Asset != null) ? i.InspectionItem.Asset.AssetStatus : "",
                CurrentAssetCondition = (i.InspectionItem != null && i.InspectionItem.Asset != null) ? i.InspectionItem.Asset.RegisteredCondition : "",
                IncidentType = i.IncidentType,
                ReportedAt = i.ReportedAt,
                ReviewStatus = i.ReviewStatus,
                VerifiedMissing = i.VerifiedMissing,
                IncidentNotes = i.IncidentNotes,
                Phase = (i.InspectionItem != null && i.InspectionItem.Inspection != null) ? i.InspectionItem.Inspection.Phase : "OUT",
                GuardName = (i.InspectionItem != null && i.InspectionItem.Inspection != null && i.InspectionItem.Inspection.GuardAccount != null) 
                    ? i.InspectionItem.Inspection.GuardAccount.FullName 
                    : "Bảo vệ",
                AdminReviewerName = i.ReviewedByAdmin != null ? i.ReviewedByAdmin.FullName : null,
                ReviewedAt = i.ReviewedAt
            })
            .ToListAsync();

        // Khắc phục lỗi font chữ Mojibake tiếng Việt
        foreach (var inc in items)
        {
            inc.GuardName = TextHelper.FixEncoding(inc.GuardName);
            inc.AdminReviewerName = TextHelper.FixEncoding(inc.AdminReviewerName);
            inc.IncidentNotes = TextHelper.FixEncoding(inc.IncidentNotes);
        }

        return new PagedResult<IncidentItemDto>
        {
            Items = items,
            TotalItems = totalItems,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Lấy chi tiết sự cố theo ID.
    /// </summary>
    public async Task<IncidentItemDto?> GetIncidentDetailAsync(long incidentId)
    {
        var detail = await _db.AssetIncidents
            .AsNoTracking()
            .Include(i => i.InspectionItem)
            .ThenInclude(ii => ii != null ? ii.Asset : null)
            .ThenInclude(a => a != null ? a.Room : null)
            .Include(i => i.InspectionItem)
            .ThenInclude(ii => ii != null ? ii.Inspection : null)
            .ThenInclude(ins => ins != null ? ins.GuardAccount : null)
            .Include(i => i.ReviewedByAdmin)
            .Where(i => i.IncidentId == incidentId)
            .Select(i => new IncidentItemDto
            {
                IncidentId = i.IncidentId,
                InspectionItemId = i.InspectionItemId,
                InspectionId = i.InspectionItem != null ? i.InspectionItem.InspectionId : 0,
                BookingId = (i.InspectionItem != null && i.InspectionItem.Inspection != null) ? i.InspectionItem.Inspection.BookingId : 0,
                AssetId = i.InspectionItem != null ? i.InspectionItem.AssetId : 0,
                AssetTag = (i.InspectionItem != null && i.InspectionItem.Asset != null) ? i.InspectionItem.Asset.AssetTag : "N/A",
                AssetName = (i.InspectionItem != null && i.InspectionItem.Asset != null) ? i.InspectionItem.Asset.AssetName : "N/A",
                RoomCode = (i.InspectionItem != null && i.InspectionItem.Asset != null && i.InspectionItem.Asset.Room != null) 
                    ? i.InspectionItem.Asset.Room.RoomCode 
                    : "N/A",
                Building = (i.InspectionItem != null && i.InspectionItem.Asset != null && i.InspectionItem.Asset.Room != null) 
                    ? i.InspectionItem.Asset.Room.Building 
                    : "N/A",
                CurrentAssetStatus = (i.InspectionItem != null && i.InspectionItem.Asset != null) ? i.InspectionItem.Asset.AssetStatus : "",
                CurrentAssetCondition = (i.InspectionItem != null && i.InspectionItem.Asset != null) ? i.InspectionItem.Asset.RegisteredCondition : "",
                IncidentType = i.IncidentType,
                ReportedAt = i.ReportedAt,
                ReviewStatus = i.ReviewStatus,
                VerifiedMissing = i.VerifiedMissing,
                IncidentNotes = i.IncidentNotes,
                Phase = (i.InspectionItem != null && i.InspectionItem.Inspection != null) ? i.InspectionItem.Inspection.Phase : "OUT",
                GuardName = (i.InspectionItem != null && i.InspectionItem.Inspection != null && i.InspectionItem.Inspection.GuardAccount != null) 
                    ? i.InspectionItem.Inspection.GuardAccount.FullName 
                    : "Bảo vệ",
                AdminReviewerName = i.ReviewedByAdmin != null ? i.ReviewedByAdmin.FullName : null,
                ReviewedAt = i.ReviewedAt
            })
            .FirstOrDefaultAsync();

        if (detail != null)
        {
            detail.GuardName = TextHelper.FixEncoding(detail.GuardName);
            detail.AdminReviewerName = TextHelper.FixEncoding(detail.AdminReviewerName);
            detail.IncidentNotes = TextHelper.FixEncoding(detail.IncidentNotes);
        }

        return detail;
    }

    /// <summary>
    /// Xử lý kỹ thuật / bảo trì sự cố cho thiết bị:
    /// - Nhân viên CSVC cập nhật registered_condition và asset_status cho thiết bị (VD: đưa vào MAINTENANCE, phục hồi ACTIVE, hoặc thanh lý RETIRED).
    /// - Không can thiệp vào cột reviewed_by_admin_id (thuộc Người 4 Admin).
    /// - Ghi vết đầy đủ vào AUDIT_LOG.
    /// </summary>
    public async Task<bool> ProcessIncidentAsync(ProcessIncidentInput input, int staffAccountId)
    {
        var asset = await _db.RoomAssets.FindAsync(input.AssetId);
        if (asset == null) return false;

        var beforeSummary = $"Thiết bị {asset.AssetTag}: Tình trạng {asset.RegisteredCondition}, Trạng thái {asset.AssetStatus}";

        string newStatus = input.Action switch
        {
            "MAINTENANCE" => "MAINTENANCE",
            "RESTORE" => "ACTIVE",
            "RETIRE" => "RETIRED",
            _ => "MAINTENANCE"
        };

        asset.AssetStatus = newStatus;
        asset.RegisteredCondition = input.TargetCondition;
        asset.UpdatedByStaffId = staffAccountId;

        await _db.SaveChangesAsync();

        var actionText = input.Action switch
        {
            "MAINTENANCE" => "Đưa thiết bị vào bảo trì / sửa chữa",
            "RESTORE" => "Đã sửa chữa / thay mới hoàn tất, đưa vào hoạt động",
            "RETIRE" => "Thiết bị hỏng nặng không thể khắc phục, đề xuất thanh lý",
            _ => "Xử lý kỹ thuật"
        };

        var afterSummary = $"{actionText}. Cập nhật tình trạng: {asset.RegisteredCondition}, trạng thái: {asset.AssetStatus}" +
                           (!string.IsNullOrWhiteSpace(input.MaintenanceNotes) ? $" | Ghi chú: {input.MaintenanceNotes}" : "");

        await LogAuditAsync(staffAccountId, "INCIDENT_MAINTENANCE", asset.AssetId.ToString(), beforeSummary, afterSummary);
        return true;
    }
    #endregion

    #region 5. NHẬT KÝ VÀ HỒ SƠ
    /// <summary>
    /// Lấy danh sách các dòng nhật ký thao tác gần nhất của nhân viên CSVC trên ROOM_ASSET.
    /// </summary>
    public async Task<List<AuditLogItemDto>> GetRecentAuditLogsAsync(int count = 10)
    {
        var logs = await _db.AuditLogs
            .AsNoTracking()
            .Include(l => l.PerformedByAccount)
            .Where(l => l.TargetEntity == "ROOM_ASSET")
            .OrderByDescending(l => l.OccurredAt)
            .Take(count)
            .Select(l => new AuditLogItemDto
            {
                AuditId = l.AuditId,
                ActionType = l.ActionType,
                TargetKey = l.TargetKey,
                OccurredAt = l.OccurredAt,
                BeforeSummary = l.BeforeSummary,
                AfterSummary = l.AfterSummary,
                PerformedByName = l.PerformedByAccount != null ? l.PerformedByAccount.FullName : "Tài khoản #" + l.PerformedByAccountId
            })
            .ToListAsync();

        foreach (var log in logs)
        {
            log.PerformedByName = TextHelper.FixEncoding(log.PerformedByName);
            log.BeforeSummary = TextHelper.FixEncoding(log.BeforeSummary);
            log.AfterSummary = TextHelper.FixEncoding(log.AfterSummary);
        }

        return logs;
    }

    /// <summary>
    /// Lấy thông tin chi tiết của nhân viên cơ sở vật chất từ FACILITIES_STAFF.
    /// </summary>
    public async Task<FacilitiesStaff?> GetStaffProfileAsync(int accountId)
    {
        return await _db.FacilitiesStaffs
            .Include(f => f.Account)
            .FirstOrDefaultAsync(f => f.AccountId == accountId);
    }

    /// <summary>
    /// Ghi bản ghi vào bảng AUDIT_LOG theo chuẩn hệ thống.
    /// </summary>
    public async Task LogAuditAsync(int accountId, string actionType, string targetKey, string? before, string? after)
    {
        var log = new AuditLog
        {
            PerformedByAccountId = accountId,
            ActionType = actionType,
            TargetEntity = "ROOM_ASSET",
            TargetKey = targetKey,
            OccurredAt = DateTime.UtcNow,
            BeforeSummary = before,
            AfterSummary = after
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync();
    }
    #endregion
}
