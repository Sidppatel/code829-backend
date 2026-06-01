using System.Text;
using System.Text.Json;
using Contracts.DTOs;
using Contracts.DTOs.Sponsors;
using Db;
using Db.Entities;
using Db.Entities.Views;
using Db.Repositories.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Services;

public class SponsorService(
    EventPlatformDbContext context,
    ISponsorProcedures sponsorProc,
    IFileStorageService fileStorage
) : ISponsorService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<PagedResponse<SponsorDto>> SearchAsync(string? query, int page, int pageSize, bool includePrivateMeta, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var baseQuery = context.SponsorViews.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var like = $"%{query}%";
            baseQuery = baseQuery.Where(p => EF.Functions.ILike(p.Name, like));
        }

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(p => p.UpcomingEventCount)
            .ThenBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = items.Select(v => MapView(v, includePrivateMeta)).ToList();
        return new PagedResponse<SponsorDto>(dtos, total, page, pageSize);
    }

    public async Task<SponsorDto?> GetByIdAsync(Guid id, bool includePrivateMeta, CancellationToken ct = default)
    {
        var v = await context.SponsorViews.AsNoTracking().FirstOrDefaultAsync(p => p.SponsorId == id, ct);
        return v is null ? null : MapView(v, includePrivateMeta);
    }

    public async Task<SponsorDto?> GetBySlugAsync(string slug, bool includePrivateMeta, CancellationToken ct = default)
    {
        var v = await context.SponsorViews.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug, ct);
        return v is null ? null : MapView(v, includePrivateMeta);
    }

    public async Task<SponsorDto> CreateAsync(CreateSponsorRequest request, CancellationToken ct = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        var baseSlug = NormalizeSlug(string.IsNullOrWhiteSpace(request.Slug) ? Slugify(name) : request.Slug);
        var slug = await EnsureUniqueSlugAsync(baseSlug, null, ct);
        var metaJson = SerializeMeta(request.Meta);

        var id = await CreateWithSlugRetryAsync(name, slug, baseSlug, request.PrimaryImagePath, metaJson, ct);
        var v = await context.SponsorViews.AsNoTracking().FirstAsync(p => p.SponsorId == id, ct);
        return MapView(v, true);
    }

    public async Task<SponsorDto?> UpdateAsync(Guid id, UpdateSponsorRequest request, CancellationToken ct = default)
    {
        var existing = await context.SponsorViews.AsNoTracking().FirstOrDefaultAsync(p => p.SponsorId == id, ct);
        if (existing is null) return null;

        var newName = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
        string? newSlug = null;
        string? baseSlug = null;
        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            baseSlug = NormalizeSlug(request.Slug);
            newSlug = baseSlug;
            if (newSlug != existing.Slug)
            {
                newSlug = await EnsureUniqueSlugAsync(baseSlug, id, ct);
            }
        }
        var newMetaJson = request.Meta is null ? null : SerializeMeta(request.Meta);

        await UpdateWithSlugRetryAsync(id, newName, newSlug, baseSlug, request.PrimaryImagePath, newMetaJson, ct);

        var v = await context.SponsorViews.AsNoTracking().FirstAsync(p => p.SponsorId == id, ct);
        return MapView(v, true);
    }

    public Task<string> ResolveAvailableSlugAsync(string baseSlug, Guid? excludeId, CancellationToken ct = default)
    {
        var normalized = NormalizeSlug(baseSlug);
        return EnsureUniqueSlugAsync(normalized, excludeId, ct);
    }

    private async Task<Guid> CreateWithSlugRetryAsync(string name, string slug, string baseSlug, string? primaryImagePath, string metaJson, CancellationToken ct)
    {
        var attempt = 0;
        var candidate = slug;
        while (true)
        {
            try
            {
                return await sponsorProc.CreateSponsorAsync(name, candidate, primaryImagePath, metaJson, ct);
            }
            catch (PostgresException ex) when (IsSlugUniqueViolation(ex) && attempt < 10)
            {
                attempt++;
                candidate = await EnsureUniqueSlugAsync(baseSlug, null, ct);
            }
        }
    }

    private async Task UpdateWithSlugRetryAsync(Guid id, string? name, string? slug, string? baseSlug, string? primaryImagePath, string? metaJson, CancellationToken ct)
    {
        var attempt = 0;
        var candidate = slug;
        while (true)
        {
            try
            {
                await sponsorProc.UpdateSponsorAsync(id, name, candidate, primaryImagePath, metaJson, ct);
                return;
            }
            catch (PostgresException ex) when (IsSlugUniqueViolation(ex) && baseSlug is not null && attempt < 10)
            {
                attempt++;
                candidate = await EnsureUniqueSlugAsync(baseSlug, id, ct);
            }
        }
    }

    private static bool IsSlugUniqueViolation(PostgresException ex) =>
        ex.SqlState == "23505" && ex.ConstraintName == "IX_sponsors_Slug";

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var exists = await context.SponsorViews.AsNoTracking().AnyAsync(p => p.SponsorId == id, ct);
        if (!exists) return false;
        await sponsorProc.DeleteSponsorAsync(id, ct);
        return true;
    }

    public async Task SetEventSponsorsAsync(Guid eventId, SetEventSponsorsRequest request, CancellationToken ct = default)
    {
        var sanitized = request.Sponsors
            .Select((p, idx) => new
            {
                sponsorId = p.SponsorId,
                sortOrder = p.SortOrder >= 0 ? p.SortOrder : idx,
                eventMeta = p.EventMeta ?? Array.Empty<SponsorMetaItemDto>()
            })
            .ToList();
        var json = JsonSerializer.Serialize(sanitized, JsonOpts);
        await sponsorProc.SetEventSponsorsAsync(eventId, json, ct);
    }

    public async Task<IReadOnlyList<EventSponsorDto>> GetEventSponsorsAsync(Guid eventId, bool includePrivateMeta, CancellationToken ct = default)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId, ct);
        if (ev is null) return Array.Empty<EventSponsorDto>();
        return await ParseEventViewSponsorsAsync(ev.Sponsors, includePrivateMeta);
    }

    public Task<IReadOnlyList<EventSponsorDto>> ParseEventViewSponsorsAsync(string sponsorsJson, bool includePrivateMeta)
    {
        if (string.IsNullOrWhiteSpace(sponsorsJson)) return Task.FromResult<IReadOnlyList<EventSponsorDto>>(Array.Empty<EventSponsorDto>());

        var rows = JsonSerializer.Deserialize<List<EventSponsorProjection>>(sponsorsJson, JsonOpts)
                   ?? new List<EventSponsorProjection>();

        var dtos = rows
            .OrderBy(r => r.SortOrder)
            .Select(r =>
            {
                var meta = includePrivateMeta
                    ? r.EffectiveMeta
                    : r.EffectiveMeta.Where(m => m.IsPublic).ToList();
                var metaDtos = meta
                    .OrderBy(m => m.SortOrder)
                    .Select(m => new SponsorMetaItemDto(m.Key, m.Value, m.IsPublic, m.SortOrder))
                    .ToList();
                return new EventSponsorDto(
                    r.SponsorId,
                    r.Name,
                    r.Slug,
                    string.IsNullOrEmpty(r.PrimaryImagePath) ? null : fileStorage.GetPublicUrl(r.PrimaryImagePath),
                    r.SortOrder,
                    metaDtos
                );
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<EventSponsorDto>>(dtos);
    }

    private SponsorDto MapView(SponsorView v, bool includePrivate)
    {
        var meta = ParseMeta(v.Meta);
        if (!includePrivate) meta = meta.Where(m => m.IsPublic).ToList();
        var metaDtos = meta
            .OrderBy(m => m.SortOrder)
            .Select(m => new SponsorMetaItemDto(m.Key, m.Value, m.IsPublic, m.SortOrder))
            .ToList();
        var imageUrl = string.IsNullOrEmpty(v.PrimaryImagePath) ? null : fileStorage.GetPublicUrl(v.PrimaryImagePath);
        return new SponsorDto(
            v.SponsorId,
            v.Name,
            v.Slug,
            v.PrimaryImagePath,
            imageUrl,
            metaDtos,
            v.EventCount,
            v.UpcomingEventCount,
            v.CreatedAt,
            v.UpdatedAt
        );
    }

    private static List<SponsorMetaItem> ParseMeta(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<SponsorMetaItem>();
        try
        {
            return JsonSerializer.Deserialize<List<SponsorMetaItem>>(json, JsonOpts) ?? new List<SponsorMetaItem>();
        }
        catch (JsonException)
        {
            return new List<SponsorMetaItem>();
        }
    }

    private static string SerializeMeta(IReadOnlyList<SponsorMetaItemDto>? meta)
    {
        if (meta is null || meta.Count == 0) return "[]";
        var cleaned = meta
            .Where(m => !string.IsNullOrWhiteSpace(m.Key))
            .Select((m, idx) => new SponsorMetaItem
            {
                Key = m.Key.Trim(),
                Value = string.IsNullOrWhiteSpace(m.Value) ? null : m.Value.Trim(),
                IsPublic = m.IsPublic,
                SortOrder = m.SortOrder >= 0 ? m.SortOrder : idx
            })
            .ToList();
        return JsonSerializer.Serialize(cleaned, JsonOpts);
    }

    private async Task<string> EnsureUniqueSlugAsync(string baseSlug, Guid? excludeId, CancellationToken ct)
    {
        var candidate = baseSlug;
        var i = 2;
        while (await context.SponsorViews.AsNoTracking().AnyAsync(p => p.Slug == candidate && (excludeId == null || p.SponsorId != excludeId), ct))
        {
            candidate = $"{baseSlug}-{i++}";
            if (candidate.Length > 220) candidate = candidate[..220];
        }
        return candidate;
    }

    private static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "sponsor";
        var sb = new StringBuilder(input.Length);
        var lastDash = false;
        foreach (var ch in input.ToLowerInvariant())
        {
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(ch);
                lastDash = false;
            }
            else if (!lastDash && sb.Length > 0)
            {
                sb.Append('-');
                lastDash = true;
            }
        }
        var s = sb.ToString().Trim('-');
        if (s.Length == 0) s = "sponsor";
        if (s.Length > 220) s = s[..220];
        return s;
    }

    private static string NormalizeSlug(string s)
    {
        s = s.Trim().ToLowerInvariant();
        var sb = new StringBuilder(s.Length);
        var lastDash = false;
        foreach (var ch in s)
        {
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(ch);
                lastDash = false;
            }
            else if ((ch == '-' || ch == ' ' || ch == '_') && !lastDash && sb.Length > 0)
            {
                sb.Append('-');
                lastDash = true;
            }
        }
        var result = sb.ToString().Trim('-');
        if (result.Length == 0) result = "sponsor";
        if (result.Length > 220) result = result[..220];
        return result;
    }
}
