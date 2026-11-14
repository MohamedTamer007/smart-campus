using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

public sealed class CampusUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";
    public string Language { get; set; } = "ar";
    public bool Active { get; set; } = true;
}

public sealed class SessionTicket
{
    public string Id { get; set; } = "";
    public Guid UserId { get; set; }
    public string ProtectedTicket { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset AbsoluteExpiry { get; set; }
}

public sealed class CampusDb(DbContextOptions<CampusDb> options) : IdentityDbContext<CampusUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<DepartmentScope> Scopes => Set<DepartmentScope>();
    public DbSet<CampusLocation> Locations => Set<CampusLocation>();
    public DbSet<MaintenanceReport> Reports => Set<MaintenanceReport>();
    public DbSet<MaintenanceHistory> History => Set<MaintenanceHistory>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<FoundItem> Items => Set<FoundItem>();
    public DbSet<CustodyHandover> Handovers => Set<CustodyHandover>();
    public DbSet<RecoveryRequest> Claims => Set<RecoveryRequest>();
    public DbSet<MediaObject> Media => Set<MediaObject>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditEvent> Audit => Set<AuditEvent>();
    public DbSet<BackgroundJob> Jobs => Set<BackgroundJob>();
    public DbSet<SearchGrant> SearchGrants => Set<SearchGrant>();
    public DbSet<SessionTicket> Sessions => Set<SessionTicket>();
    public DbSet<OutboundEmail> OutboundEmails => Set<OutboundEmail>();
    public DbSet<InferenceUsage> InferenceUsage => Set<InferenceUsage>();
    public DbSet<AiEvaluationRun> AiEvaluationRuns => Set<AiEvaluationRun>();
    public DbSet<AiEvaluationCase> AiEvaluationCases => Set<AiEvaluationCase>();
    public DbSet<AiEvaluationVector> AiEvaluationVectors => Set<AiEvaluationVector>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.HasDefaultSchema("smart_campus");
        foreach (var entity in model.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(string)))
                property.SetMaxLength(4000);
        model.Entity<OutboundEmail>().Property(email => email.ProviderMessageId).HasMaxLength(200);
        model.Entity<OutboundEmail>().Property(email => email.FailureCode).HasMaxLength(100);
        foreach (var type in new[] { typeof(MaintenanceReport), typeof(FoundItem), typeof(CustodyHandover), typeof(RecoveryRequest) })
            model.Entity(type).Property("Version").IsConcurrencyToken();
        model.Entity<Department>().HasIndex(x => x.Code).IsUnique();
        model.Entity<DepartmentScope>().HasIndex(x => new { x.UserId, x.DepartmentId }).IsUnique();
        model.Entity<DepartmentScope>().HasOne<CampusUser>().WithMany().HasForeignKey(x => x.UserId);
        model.Entity<DepartmentScope>().HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId);
        model.Entity<DepartmentScope>().HasOne<CampusUser>().WithMany().HasForeignKey(x => x.GrantedBy).OnDelete(DeleteBehavior.Restrict);
        model.Entity<CampusLocation>().HasOne<CampusLocation>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<CampusLocation>().HasOne<Department>().WithMany().HasForeignKey(x => x.MaintenanceDepartmentId);
        model.Entity<CampusLocation>().Property(x => x.PositionVersion).HasDefaultValue(1).IsConcurrencyToken();
        model.Entity<MaintenanceReport>().HasIndex(x => x.Reference).IsUnique();
        model.Entity<MaintenanceReport>().HasIndex(x => new { x.ReporterId, x.CreatedAt });
        model.Entity<MaintenanceReport>().HasIndex(x => new { x.DepartmentId, x.Status });
        model.Entity<MaintenanceReport>().HasOne<CampusUser>().WithMany().HasForeignKey(x => x.ReporterId);
        model.Entity<MaintenanceReport>().HasOne<CampusUser>().WithMany().HasForeignKey(x => x.AssigneeId);
        model.Entity<MaintenanceReport>().HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId);
        model.Entity<MaintenanceReport>().HasOne<CampusLocation>().WithMany().HasForeignKey(x => x.FacultyId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<MaintenanceReport>().HasOne<CampusLocation>().WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<MaintenanceReport>().HasOne<CampusLocation>().WithMany().HasForeignKey(x => x.FloorId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<MaintenanceReport>().ToTable(t => t.HasCheckConstraint("maintenance_status", "\"Status\" IN ('submitted','review','repair','resolved','rejected')"));
        model.Entity<MaintenanceHistory>().HasOne<MaintenanceReport>().WithMany().HasForeignKey(x => x.ReportId);
        model.Entity<MaintenanceHistory>().HasOne<CampusUser>().WithMany().HasForeignKey(x => x.ActorId);
        model.Entity<MaintenanceHistory>().HasOne<CampusUser>().WithMany().HasForeignKey(x => x.AssigneeId);
        model.Entity<Assessment>().HasOne<MaintenanceReport>().WithMany().HasForeignKey(x => x.ReportId);
        model.Entity<Assessment>().HasOne<CampusUser>().WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Assessment>().HasIndex(x => new { x.ReportId, x.InputVersion }).IsUnique();
        model.Entity<FoundItem>().HasOne<CampusUser>().WithMany().HasForeignKey(x => x.UploaderId);
        model.Entity<FoundItem>().HasOne<CampusUser>().WithMany().HasForeignKey(x => x.CustodianId);
        model.Entity<FoundItem>().HasOne<Department>().WithMany().HasForeignKey(x => x.CustodianDepartmentId);
        model.Entity<FoundItem>().ToTable(t => {
            t.HasCheckConstraint("custodian_xor", "(\"CustodianId\" IS NULL) <> (\"CustodianDepartmentId\" IS NULL)");
            t.HasCheckConstraint("item_state", "\"State\" IN ('available','reserved','delivered')");
        });
        model.Entity<CustodyHandover>().HasOne<FoundItem>().WithMany().HasForeignKey(x => x.ItemId);
        model.Entity<CustodyHandover>().HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId);
        model.Entity<CustodyHandover>().HasOne<CampusUser>().WithMany().HasForeignKey(x => x.FromUserId);
        model.Entity<CustodyHandover>().HasOne<Department>().WithMany().HasForeignKey(x => x.FromDepartmentId);
        model.Entity<CustodyHandover>().HasOne<CampusUser>().WithMany().HasForeignKey(x => x.ReceivedBy);
        model.Entity<CustodyHandover>().HasOne<CampusUser>().WithMany().HasForeignKey(x => x.NotReceivedBy);
        model.Entity<CustodyHandover>().HasIndex(x => x.ItemId).IsUnique().HasFilter("\"Status\" IN ('pending','not-received')");
        model.Entity<CustodyHandover>().ToTable(t => t.HasCheckConstraint("source_custodian_xor", "(\"FromUserId\" IS NULL) <> (\"FromDepartmentId\" IS NULL)"));
        model.Entity<RecoveryRequest>().HasOne<FoundItem>().WithMany().HasForeignKey(x => x.ItemId);
        model.Entity<RecoveryRequest>().HasOne<CampusUser>().WithMany().HasForeignKey(x => x.RequesterId);
        model.Entity<RecoveryRequest>().HasOne<CampusUser>().WithMany().HasForeignKey(x => x.ReviewedBy);
        model.Entity<RecoveryRequest>().HasOne<CampusUser>().WithMany().HasForeignKey(x => x.DeliveredBy);
        model.Entity<AuditEvent>().HasOne<CampusUser>().WithMany().HasForeignKey(x => x.ActorId);
        model.Entity<RecoveryRequest>().HasIndex(x => x.ItemId).IsUnique().HasFilter("\"ReservationActive\"");
        model.Entity<RecoveryRequest>().HasIndex(x => new { x.ItemId, x.RequesterId }).IsUnique().HasFilter("\"Status\" IN ('pending','approved')");
        model.Entity<RecoveryRequest>().ToTable(t => t.HasCheckConstraint("reservation_approved", "NOT \"ReservationActive\" OR \"Status\" = 'approved'"));
        model.Entity<MediaObject>().HasOne<MaintenanceReport>().WithMany().HasForeignKey(x => x.ReportId);
        model.Entity<MediaObject>().HasOne<FoundItem>().WithMany().HasForeignKey(x => x.ItemId);
        model.Entity<MediaObject>().ToTable(t => t.HasCheckConstraint("media_parent_xor", "(\"ReportId\" IS NULL) <> (\"ItemId\" IS NULL)"));
        model.Entity<Notification>().HasOne<CampusUser>().WithMany().HasForeignKey(x => x.RecipientId);
        model.Entity<Notification>().HasIndex(x => new { x.RecipientId, x.ReadAt, x.CreatedAt });
        model.Entity<BackgroundJob>().HasIndex(x => x.Key).IsUnique();
        model.Entity<SearchGrant>().HasOne<CampusUser>().WithMany().HasForeignKey(x => x.UserId);
        model.Entity<SearchGrant>().HasOne<FoundItem>().WithMany().HasForeignKey(x => x.ItemId);
        model.Entity<SearchGrant>().HasIndex(x => new { x.UserId, x.ItemId, x.ExpiresAt });
        model.Entity<SessionTicket>().HasKey(x => x.Id);
        model.Entity<SessionTicket>().HasOne<CampusUser>().WithMany().HasForeignKey(x => x.UserId);
        model.Entity<SessionTicket>().HasIndex(x => x.UserId);
        model.Entity<InferenceUsage>().HasKey(x => x.Key);
        model.Entity<InferenceUsage>().Property(x => x.Key).HasMaxLength(100);
        model.Entity<InferenceUsage>().HasIndex(x => new { x.Provider, x.WindowStart });
        model.Entity<InferenceUsage>().ToTable(t => t.HasCheckConstraint("inference_count_positive", "\"Count\" > 0"));
        model.Entity<AiEvaluationRun>().Property(x => x.Suite).HasMaxLength(40);
        model.Entity<AiEvaluationRun>().Property(x => x.SuiteVersion).HasMaxLength(40);
        model.Entity<AiEvaluationRun>().Property(x => x.State).HasMaxLength(24);
        model.Entity<AiEvaluationRun>().HasIndex(x => new { x.State, x.CreatedAt });
        model.Entity<AiEvaluationRun>().HasIndex(x => x.IsActive).IsUnique().HasFilter("\"IsActive\"");
        model.Entity<AiEvaluationCase>().Property(x => x.CaseKey).HasMaxLength(80);
        model.Entity<AiEvaluationCase>().Property(x => x.Kind).HasMaxLength(40);
        model.Entity<AiEvaluationCase>().Property(x => x.State).HasMaxLength(24);
        model.Entity<AiEvaluationCase>().Property(x => x.Model).HasMaxLength(120);
        model.Entity<AiEvaluationCase>().Property(x => x.ErrorCode).HasMaxLength(100);
        model.Entity<AiEvaluationCase>().Property(x => x.InputJson).HasColumnType("text");
        model.Entity<AiEvaluationCase>().Property(x => x.ExpectedJson).HasColumnType("text");
        model.Entity<AiEvaluationCase>().Property(x => x.ActualJson).HasColumnType("text");
        model.Entity<AiEvaluationCase>().Property(x => x.EmbeddingCacheKey).HasColumnType("text");
        model.Entity<AiEvaluationCase>().HasIndex(x => new { x.RunId, x.CaseKey }).IsUnique();
        model.Entity<AiEvaluationCase>().HasOne<AiEvaluationRun>().WithMany(x => x.Cases).HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<AiEvaluationCase>().HasIndex(x => new { x.RunId, x.State });
        model.Entity<AiEvaluationVector>().HasKey(x => new { x.CacheKey, x.Revision });
        model.Entity<AiEvaluationVector>().Property(x => x.CacheKey).HasMaxLength(120);
        model.Entity<AiEvaluationVector>().Property(x => x.Revision).HasMaxLength(120);
        model.Entity<AiEvaluationVector>().Property(x => x.VectorJson).HasColumnType("text");
    }
}
