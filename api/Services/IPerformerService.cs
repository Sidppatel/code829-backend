using Contracts.DTOs;
using Contracts.DTOs.Performers;

namespace Api.Services;

public interface IPerformerService
{
    Task<PagedResponse<PerformerDto>> SearchAsync(string? query, int page, int pageSize, bool includePrivateMeta, CancellationToken ct = default);
    Task<PerformerDto?> GetByIdAsync(Guid id, bool includePrivateMeta, CancellationToken ct = default);
    Task<PerformerDto?> GetBySlugAsync(string slug, bool includePrivateMeta, CancellationToken ct = default);
    Task<PerformerDto> CreateAsync(CreatePerformerRequest request, CancellationToken ct = default);
    Task<PerformerDto?> UpdateAsync(Guid id, UpdatePerformerRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<string> ResolveAvailableSlugAsync(string baseSlug, Guid? excludeId, CancellationToken ct = default);
    Task SetEventPerformersAsync(Guid eventId, SetEventPerformersRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<EventPerformerDto>> GetEventPerformersAsync(Guid eventId, bool includePrivateMeta, CancellationToken ct = default);
    Task<IReadOnlyList<EventPerformerDto>> ParseEventViewPerformersAsync(string performersJson, bool includePrivateMeta);
}
