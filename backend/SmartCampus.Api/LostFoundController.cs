using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;

namespace SmartCampus.Api;

public sealed class FoundForm
{
    public string Description { get; set; } = "";
    public string Place { get; set; } = "";
    public DateOnly Date { get; set; }
    public string Storage { get; set; } = "";
    public Guid? Destination { get; set; }
    public string? Office { get; set; }
    public IFormFile? Photo { get; set; }
}
public sealed record ClaimInput(string? Message);
public sealed class SearchForm { public string Description { get; set; } = ""; public IFormFile? Photo { get; set; } }

[ApiController, Authorize, Route("api/v1/found")]
public sealed class LostFoundController(LostFoundService service, RetrievalService retrieval) : ControllerBase
{
    [HttpGet, ProducesResponseType<PageResponse<FoundResponse>>(200)] public async Task<object> List(int page = 1, int pageSize = 50) => new { items = await service.List(Math.Max(1, page), Math.Clamp(pageSize, 1, 100)) };
    [HttpGet("{id:guid}"), ProducesResponseType<FoundResponse>(200)] public Task<object> Detail(Guid id) => service.Detail(id);
    [HttpPost, RequestSizeLimit(6000000), ProducesResponseType<FoundResponse>(200)] public Task<object> Register([FromForm] FoundForm input) => service.Register(
        new(input.Description, input.Place, input.Date, input.Storage, input.Destination, input.Office), input.Photo ?? throw new RuleException("image_required"));
    [HttpPost("search"), RequestSizeLimit(6000000), ProducesResponseType<SearchResponse>(200)] public Task<object> Search([FromForm] SearchForm input) => retrieval.Search(input.Description, input.Photo);
    [HttpPost("{id:guid}/handovers"), ProducesResponseType<FoundResponse>(200)] public Task<object> Handover(Guid id, HandoverInput input) => service.RequestHandover(id, input);
    [HttpPost("{id:guid}/handovers/confirm"), ProducesResponseType<FoundResponse>(200)] public Task<object> Confirm(Guid id, VersionInput input) => service.HandoverAction(id, input.Version, "confirmed");
    [HttpPost("{id:guid}/handovers/not-received"), ProducesResponseType<FoundResponse>(200)] public Task<object> NotReceived(Guid id, VersionInput input) => service.HandoverAction(id, input.Version, "not-received");
    [HttpPost("{id:guid}/handovers/cancel"), ProducesResponseType<FoundResponse>(200)] public Task<object> Cancel(Guid id, VersionInput input) => service.HandoverAction(id, input.Version, "cancelled");
    [HttpPost("{id:guid}/claims"), ProducesResponseType<ClaimResponse>(200)] public Task<object> Claim(Guid id, ClaimInput input) => service.Claim(id, input.Message);
}

[ApiController, Authorize, Route("api/v1/claims")]
public sealed class ClaimsController(LostFoundService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<PageResponse<ClaimResponse>>(200)] public async Task<object> List(int page = 1, int pageSize = 50) => new { items = await service.Claims(Math.Max(1, page), Math.Clamp(pageSize, 1, 100)) };
    [HttpGet("{id:guid}"), ProducesResponseType<ClaimDetailResponse>(200)] public Task<object> Detail(Guid id) => service.ClaimDetail(id);
    [HttpPost("{id:guid}/approve"), ProducesResponseType<ClaimResponse>(200)] public Task<object> Approve(Guid id, ClaimDecision input) => service.Decide(id, input, "approved");
    [HttpPost("{id:guid}/reject"), ProducesResponseType<ClaimResponse>(200)] public Task<object> Reject(Guid id, ClaimDecision input) => service.Decide(id, input, "rejected");
    [HttpPost("{id:guid}/cancel"), ProducesResponseType<ClaimResponse>(200)] public Task<object> Cancel(Guid id, ClaimDecision input) => service.Decide(id, input, "cancelled");
    [HttpPost("{id:guid}/confirm"), ProducesResponseType<ClaimResponse>(200)] public Task<object> Confirm(Guid id, VersionInput input) => service.Confirm(id, input.Version);
}
