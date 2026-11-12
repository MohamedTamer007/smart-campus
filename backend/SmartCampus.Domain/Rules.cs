using System.Net.Mail;

namespace SmartCampus.Domain;

public sealed class RuleException(string code, int status = 400, DateTimeOffset? retryAt = null) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
    public DateTimeOffset? RetryAt { get; } = retryAt;
}

public static class Rules
{
    public static string UniversityEmail(string input)
    {
        var email = input.Trim();
        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email ||
            !parsed.Host.Equals("o6u.edu.eg", StringComparison.OrdinalIgnoreCase) || parsed.User.Length > 64)
            throw new RuleException("invalid_email");
        return email.ToLowerInvariant();
    }

    public static string Required(string? input, int maximum = 4000)
    {
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > maximum) throw new RuleException("invalid_input");
        return text;
    }

    public static void Version(int current, int expected)
    {
        if (current != expected) throw new RuleException("conflict", 409);
    }

    public static void Transition(string current, string next)
    {
        var allowed = (current, next) switch
        {
            ("submitted", "review") or ("review", "review") or ("review", "repair") or
            ("review", "rejected") or ("repair", "repair") or ("repair", "resolved") => true,
            _ => false
        };
        if (!allowed) throw new RuleException("invalid_transition", 409);
    }
}

public abstract class Record
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public abstract class VersionedRecord : Record
{
    public int Version { get; set; } = 1;
}

public sealed class Department : Record
{
    public string Code { get; set; } = "";
    public string Ar { get; set; } = "";
    public string En { get; set; } = "";
    public bool CanReceive { get; set; }
    public bool Maintenance { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class DepartmentScope : Record
{
    public Guid UserId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid GrantedBy { get; set; }
}

public sealed class CampusLocation : Record
{
    public Guid? ParentId { get; set; }
    public string Kind { get; set; } = "";
    public string Ar { get; set; } = "";
    public string En { get; set; } = "";
    public Guid? MaintenanceDepartmentId { get; set; }
    public double? MapX { get; set; }
    public double? MapY { get; set; }
    public int PositionVersion { get; set; } = 1;
}

public sealed class MaintenanceReport : VersionedRecord
{
    public int ContentVersion { get; set; } = 1;
    public string Reference { get; set; } = "SC-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
    public Guid ReporterId { get; set; }
    public Guid FacultyId { get; set; }
    public Guid BuildingId { get; set; }
    public Guid FloorId { get; set; }
    public Guid DepartmentId { get; set; }
    public string Description { get; set; } = "";
    public string Details { get; set; } = "";
    public string Status { get; set; } = "submitted";
    public Guid? AssigneeId { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? FaultType { get; set; }
    public string? Priority { get; set; }
    public string? Risk { get; set; }
}

public sealed class MaintenanceHistory : Record
{
    public Guid ReportId { get; set; }
    public Guid ActorId { get; set; }
    public string Status { get; set; } = "";
    public string Note { get; set; } = "";
    public Guid? AssigneeId { get; set; }
    public string? DecisionJson { get; set; }
}

public sealed class Assessment : Record
{
    public Guid ReportId { get; set; }
    public int InputVersion { get; set; }
    public string State { get; set; } = "pending";
    public string? FaultType { get; set; }
    public string? Priority { get; set; }
    public string? Risk { get; set; }
    public string? Explanation { get; set; }
    public string? Model { get; set; }
    public string? FailureCode { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedImageId { get; set; }
}

public sealed class FoundItem : VersionedRecord
{
    public Guid UploaderId { get; set; }
    public Guid? CustodianId { get; set; }
    public Guid? CustodianDepartmentId { get; set; }
    public string Description { get; set; } = "";
    public string Place { get; set; } = "";
    public DateOnly Date { get; set; }
    public string Storage { get; set; } = "";
    public string State { get; set; } = "available";
}

public sealed class CustodyHandover : VersionedRecord
{
    public Guid ItemId { get; set; }
    public Guid? FromUserId { get; set; }
    public Guid? FromDepartmentId { get; set; }
    public Guid DepartmentId { get; set; }
    public string Office { get; set; } = "";
    public string Status { get; set; } = "pending";
    public Guid? ReceivedBy { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
    public Guid? NotReceivedBy { get; set; }
    public DateTimeOffset? NotReceivedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}

public sealed class RecoveryRequest : VersionedRecord
{
    public Guid ItemId { get; set; }
    public Guid RequesterId { get; set; }
    public string Message { get; set; } = "";
    public string Status { get; set; } = "pending";
    public bool ReservationActive { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? Phone { get; set; }
    public string? Place { get; set; }
    public string? Instructions { get; set; }
    public string? Reason { get; set; }
    public string? ClosureReason { get; set; }
    public Guid? DeliveredBy { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}

public sealed class MediaObject : Record
{
    public Guid? ReportId { get; set; }
    public Guid? ItemId { get; set; }
    public string Bucket { get; set; } = "";
    public string ObjectKey { get; set; } = "";
    public string ContentType { get; set; } = "image/jpeg";
}

public sealed class Notification : Record
{
    public Guid RecipientId { get; set; }
    public string Kind { get; set; } = "";
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

public sealed class AuditEvent : Record
{
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = "";
    public Guid EntityId { get; set; }
}

public sealed class BackgroundJob : Record
{
    public string Key { get; set; } = "";
    public string Kind { get; set; } = "";
    public Guid EntityId { get; set; }
    public int InputVersion { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset AvailableAt { get; set; } = DateTimeOffset.UtcNow;
    public string State { get; set; } = "pending";
}

public sealed class SearchGrant : Record
{
    public Guid UserId { get; set; }
    public Guid ItemId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class OutboundEmail : Record
{
    public string ProtectedPayload { get; set; } = "";
    public DateTimeOffset? SentAt { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? FailureCode { get; set; }
}

public sealed class InferenceUsage
{
    public string Key { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Window { get; set; } = "";
    public DateTimeOffset WindowStart { get; set; }
    public int Count { get; set; }
}
