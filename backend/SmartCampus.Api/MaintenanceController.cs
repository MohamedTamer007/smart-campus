using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartCampus.Infrastructure;

namespace SmartCampus.Api;

public sealed record VersionInput(int Version);
public sealed class ReportForm
{
    public string Description { get; set; } = "";
    public Guid Faculty { get; set; }
    public Guid Building { get; set; }
    public Guid Floor { get; set; }
    public string Details { get; set; } = "";
    public IFormFile? Photo { get; set; }
    public int Version { get; set; }
    public MaintenanceInput Input() => new(Description, Faculty, Building, Floor, Details);
}

[ApiController, Authorize, Route("api/v1/maintenance")]
public sealed class MaintenanceController(MaintenanceService service) : ControllerBase
{
    [HttpGet, ProducesResponseType<PageResponse<MaintenanceResponse>>(200)] public async Task<object> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        new { items = await service.List(Math.Max(1, page), Math.Clamp(pageSize, 1, 100)) };
    [HttpGet("{id:guid}"), ProducesResponseType<MaintenanceResponse>(200)] public Task<object> Detail(Guid id) => service.Detail(id);
    [HttpPost, RequestSizeLimit(6000000), ProducesResponseType<MaintenanceResponse>(200)] public Task<object> Create([FromForm] ReportForm input) => service.Create(input.Input(), input.Photo);
    [HttpPut("{id:guid}"), RequestSizeLimit(6000000), ProducesResponseType<MaintenanceResponse>(200)] public Task<object> Edit(Guid id, [FromForm] ReportForm input) => service.Edit(id, input.Version, input.Input(), input.Photo);
    [HttpPost("{id:guid}/cancel"), ProducesResponseType<MaintenanceResponse>(200)] public Task<object> Cancel(Guid id, VersionInput input) => service.Cancel(id, input.Version);
    [HttpPost("{id:guid}/updates"), ProducesResponseType<MaintenanceResponse>(200)] public Task<object> Update(Guid id, MaintenanceUpdate input) => service.Update(id, input);
    [HttpPost("{id:guid}/ai-review"), ProducesResponseType<MaintenanceResponse>(200)] public Task<object> ReviewAi(Guid id, AiReviewInput input) => service.ReviewAi(id, input);
}
