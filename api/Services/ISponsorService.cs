using Contracts.DTOs;
using Contracts.DTOs.Sponsors;

namespace Api.Services;

public interface ISponsorService
{
    Task<PagedResponse<SponsorDto>> SearchAsync(string? query, int page, int pageSize, bool includePrivateMeta, CancellationToken ct = default);
    Task<SponsorDto?> GetByIdAsync(Guid id, bool includePrivateMeta, CancellationToken ct = default);
    Task<SponsorDto?> GetBySlugAsync(string slug, bool includePrivateMeta, CancellationToken ct = default);
    Task<SponsorDto> CreateAsync(CreateSponsorRequest request, CancellationToken ct = default);
    Task<SponsorDto?> UpdateAsync(Guid id, UpdateSponsorRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<string> ResolveAvailableSlugAsync(string baseSlug, Guid? excludeId, CancellationToken ct = default);
    Task SetEventSponsorsAsync(Guid eventId, SetEventSponsorsRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<EventSponsorDto>> GetEventSponsorsAsync(Guid eventId, bool includePrivateMeta, CancellationToken ct = default);
    Task<IReadOnlyList<EventSponsorDto>> ParseEventViewSponsorsAsync(string sponsorsJson, bool includePrivateMeta);
}
