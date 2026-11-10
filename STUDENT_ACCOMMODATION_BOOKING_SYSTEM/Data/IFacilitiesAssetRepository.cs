using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models.Entities;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models.Facilities;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;

/// <summary>
/// Giao diện Repository chuyên biệt cho Người 3 - Nhân viên cơ sở vật chất.
/// Cung cấp các thao tác CRUD thiết bị (ROOM_ASSET), truy vấn sự cố (ASSET_INCIDENT) và ghi nhật ký (AUDIT_LOG).
/// </summary>
public interface IFacilitiesAssetRepository
{
    // ==================== 1. THỐNG KÊ TỔNG QUAN ====================
    Task<FacilitiesOverviewStats> GetOverviewStatsAsync();

    // ==================== 2. QUẢN LÝ THIẾT BỊ (ROOM_ASSET) ====================
    Task<PagedResult<RoomAssetDto>> GetAssetsAsync(AssetFilterCriteria filter);
    Task<RoomAsset?> GetAssetByIdAsync(long assetId);
    Task<bool> IsAssetTagExistsAsync(string assetTag, long? excludeAssetId = null);
    Task<long> CreateAssetAsync(AssetCreateInput input, int staffAccountId);
    Task<bool> UpdateAssetAsync(AssetEditInput input, int staffAccountId);
    Task<bool> UpdateAssetStatusAsync(AssetStatusUpdateInput input, int staffAccountId);
    Task<bool> RetireOrDeleteAssetAsync(long assetId, int staffAccountId);

    // ==================== 3. TRUY VẤN PHÒNG (ROOM - READ ONLY) ====================
    Task<List<RoomLookupDto>> GetActiveRoomsAsync();
    Task<List<string>> GetBuildingsAsync();

    // ==================== 4. SỰ CỐ THIẾT BỊ (ASSET_INCIDENT) ====================
    Task<PagedResult<IncidentItemDto>> GetIncidentsAsync(IncidentFilterCriteria filter);
    Task<IncidentItemDto?> GetIncidentDetailAsync(long incidentId);
    Task<bool> ProcessIncidentAsync(ProcessIncidentInput input, int staffAccountId);

    // ==================== 5. NHẬT KÝ VÀ HỒ SƠ ====================
    Task<List<AuditLogItemDto>> GetRecentAuditLogsAsync(int count = 10);
    Task<FacilitiesStaff?> GetStaffProfileAsync(int accountId);
    Task LogAuditAsync(int accountId, string actionType, string targetKey, string? before, string? after);
}

