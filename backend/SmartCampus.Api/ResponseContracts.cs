namespace SmartCampus.Api;

public sealed record PageResponse<T>(T[] Items);
public sealed record BilingualName(string Ar, string En);
public sealed record LocationNames(BilingualName Faculty, BilingualName Building, BilingualName Floor);
public sealed record AssigneeResponse(Guid Id, string Name);
public sealed record HistoryResponse(DateTimeOffset At, Guid Actor, string Status, string Note, Guid? Assignee);
public sealed record AssessmentResponse(string State, string? Type, string? Priority, string? Risk, string? Explanation, bool InputReviewed);
public sealed record DecisionResponse(string? Type, string? Priority, string? Risk);
public sealed record MaintenanceResponse(Guid Id, string Reference, Guid OwnerId, string Description, Guid Faculty, Guid Building,
    Guid Floor, string Details, string Status, DateTimeOffset? CancelledAt, LocationNames LocationNames, int Version,
    DateTimeOffset CreatedAt, Guid DepartmentId, Guid? Assignee, AssigneeResponse[] AllowedAssignees, HistoryResponse[] History,
    AssessmentResponse? Assessment, DecisionResponse? Decision, string? MediaUrl, bool CanEdit, bool CanViewAssessment, bool CanUpdate,
    bool CanReviewAiInput, bool CanReviewAiImage);
public sealed record HandoverResponse(Guid Id, int Version, string Department, string Office, string Status);
public sealed record FoundResponse(Guid Id, Guid UploaderId, Guid? CustodianId, string? CustodianDepartment,
    string Description, string Place, DateOnly Date, string Storage, string State, int Version, DateTimeOffset CreatedAt,
    string MediaUrl, bool CanClaim, bool CanHandover, bool CanReceive, bool CanCancelHandover, HandoverResponse? Handover);
public sealed record ClaimResponse(Guid Id, Guid ItemId, Guid ClaimantId, string ClaimantName, string ClaimantRole,
    DateTimeOffset CreatedAt, string Message, string Status, int Version, string? Phone, string? Place, string? Instructions,
    string? Reason, string? ClosureReason, bool ClaimantConfirmed, bool CustodianConfirmed, DateTimeOffset? ClaimantAt,
    DateTimeOffset? CustodianAt, bool CanApprove, bool CanReject, bool CanCancel);
public sealed record ClaimDetailResponse(ClaimResponse Claim, FoundResponse Item);
public sealed record ScopeResponse(Guid Id, string Code);
public sealed record AccountResponse(Guid Id, string Name, string Email, string[] Roles, ScopeResponse[] Departments,
    string Role, string? Department, string Language, bool IsDemo = false);
public sealed record SearchResponse(Guid[] ItemIds, string RankingVersion, string? Calibration, bool Calibrated);
public sealed record JobStateResponse(string Kind, string State, int Count, int MaximumAttempts);
public sealed record ProviderQuotaResponse(string Provider, int Used, int DailyLimit);
public sealed record AiReadinessResponse(string Provider, bool TextConfigured, bool ImagesConfigured,
    bool TaxonomyConfigured, bool TextTransferEnabled, bool ImageTransferEnabled,
    bool ProviderDataCollectionApproved);
public sealed record OperationsResponse(DateTimeOffset SampledAt, string Deployment, long WorkingSetBytes,
    JobStateResponse[] Jobs, ProviderQuotaResponse[] Quotas, string Worker, bool CalibrationApproved,
    AiReadinessResponse Ai);
