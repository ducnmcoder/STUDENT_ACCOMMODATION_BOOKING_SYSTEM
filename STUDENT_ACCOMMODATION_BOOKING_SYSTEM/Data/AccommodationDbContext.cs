using Microsoft.EntityFrameworkCore;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models.Entities;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;

/// <summary>
/// Entity Framework Core DbContext cho hệ thống Student Accommodation Booking System.
/// Phục vụ cho truy vấn và thao tác dữ liệu của Người 3 (Cơ sở vật chất) và các vai trò khác.
/// </summary>
public class AccommodationDbContext : DbContext
{
    public AccommodationDbContext(DbContextOptions<AccommodationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<FacilitiesStaff> FacilitiesStaffs => Set<FacilitiesStaff>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomAsset> RoomAssets => Set<RoomAsset>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<InspectionItem> InspectionItems => Set<InspectionItem>();
    public DbSet<AssetIncident> AssetIncidents => Set<AssetIncident>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Cấu hình bảng ROOM_ASSET
        modelBuilder.Entity<RoomAsset>(entity =>
        {
            entity.ToTable("ROOM_ASSET");
            entity.HasKey(e => e.AssetId).HasName("PK_ROOM_ASSET");

            // Ràng buộc duy nhất trên mã tem thiết bị (asset_tag)
            entity.HasIndex(e => e.AssetTag, "UQ_ROOM_ASSET_tag").IsUnique();

            entity.Property(e => e.AssetTag).IsUnicode(false);
            entity.Property(e => e.RegisteredCondition).IsUnicode(false);
            entity.Property(e => e.AssetStatus).IsUnicode(false);

            // Khóa ngoại tới ROOM
            entity.HasOne(e => e.Room)
                .WithMany(r => r.Assets)
                .HasForeignKey(e => e.RoomId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ROOM_ASSET_room");

            // Khóa ngoại tới nhân viên tạo thiết bị
            entity.HasOne(e => e.CreatedByStaff)
                .WithMany()
                .HasForeignKey(e => e.CreatedByStaffId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ROOM_ASSET_creator");

            // Khóa ngoại tới nhân viên cập nhật thiết bị
            entity.HasOne(e => e.UpdatedByStaff)
                .WithMany()
                .HasForeignKey(e => e.UpdatedByStaffId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ROOM_ASSET_updater");
        });

        // 2. Cấu hình bảng ROOM (Read-only cho Người 3)
        modelBuilder.Entity<Room>(entity =>
        {
            entity.ToTable("ROOM");
            entity.HasKey(e => e.RoomId).HasName("PK_ROOM");
            entity.Property(e => e.RoomCode).IsUnicode(false);
            entity.Property(e => e.RoomStatus).IsUnicode(false);
        });

        // 3. Cấu hình bảng FACILITIES_STAFF
        modelBuilder.Entity<FacilitiesStaff>(entity =>
        {
            entity.ToTable("FACILITIES_STAFF");
            entity.HasKey(e => e.AccountId).HasName("PK_FACILITIES_STAFF");
            entity.Property(e => e.EmployeeCode).IsUnicode(false);

            entity.HasOne(e => e.Account)
                .WithOne()
                .HasForeignKey<FacilitiesStaff>(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 4. Cấu hình bảng AUDIT_LOG
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AUDIT_LOG");
            entity.HasKey(e => e.AuditId).HasName("PK_AUDIT_LOG");
            entity.Property(e => e.ActionType).IsUnicode(false);
            entity.Property(e => e.TargetEntity).IsUnicode(false);
            entity.Property(e => e.TargetKey).IsUnicode(false);

            entity.HasOne(e => e.PerformedByAccount)
                .WithMany()
                .HasForeignKey(e => e.PerformedByAccountId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_AUDIT_LOG_account");
        });

        // 5. Cấu hình bảng INSPECTION và INSPECTION_ITEM
        modelBuilder.Entity<Inspection>(entity =>
        {
            entity.ToTable("INSPECTION");
            entity.HasKey(e => e.InspectionId).HasName("PK_INSPECTION");
            entity.Property(e => e.Phase).IsUnicode(false);
            entity.Property(e => e.OverallCondition).IsUnicode(false);
        });

        modelBuilder.Entity<InspectionItem>(entity =>
        {
            entity.ToTable("INSPECTION_ITEM");
            entity.HasKey(e => e.InspectionItemId).HasName("PK_INSPECTION_ITEM");
            entity.Property(e => e.ObservedState).IsUnicode(false);

            entity.HasOne(e => e.Inspection)
                .WithMany(i => i.Items)
                .HasForeignKey(e => e.InspectionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_INSPECTION_ITEM_inspection");

            entity.HasOne(e => e.Asset)
                .WithMany(a => a.InspectionItems)
                .HasForeignKey(e => e.AssetId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_INSPECTION_ITEM_asset");
        });

        // 6. Cấu hình bảng ASSET_INCIDENT
        modelBuilder.Entity<AssetIncident>(entity =>
        {
            entity.ToTable("ASSET_INCIDENT");
            entity.HasKey(e => e.IncidentId).HasName("PK_ASSET_INCIDENT");

            entity.HasIndex(e => e.InspectionItemId, "UQ_ASSET_INCIDENT_item").IsUnique();

            entity.Property(e => e.IncidentType).IsUnicode(false);
            entity.Property(e => e.ReviewStatus).IsUnicode(false);

            entity.HasOne(e => e.InspectionItem)
                .WithOne(i => i.Incident)
                .HasForeignKey<AssetIncident>(e => e.InspectionItemId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ASSET_INCIDENT_item");

            entity.HasOne(e => e.ReviewedByAdmin)
                .WithMany()
                .HasForeignKey(e => e.ReviewedByAdminId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ASSET_INCIDENT_admin");
        });
    }
}

