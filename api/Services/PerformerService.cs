using System.Text;
using System.Text.Json;
using Contracts.DTOs;
using Contracts.DTOs.Performers;
using Db;
using Db.Entities;
using Db.Entities.Views;
using Db.Repositories.StoredProcedures;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Services;

public class PerformerService(
    EventPlatformDbContext context,
    IPerformerProcedures performerProc,
    IFileStorageService fileStorage
) : IPerformerService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<PagedResponse<PerformerDto>> SearchAsync(string? query, int page, int pageSize, bool includePrivateMeta, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var baseQuery = context.PerformerViews.AsNoTracking().AsQueryable();
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
        return new PagedResponse<PerformerDto>(dtos, total, page, pageSize);
    }

    public async Task<PerformerDto?> GetByIdAsync(Guid id, bool includePrivateMeta, CancellationToken ct = default)
    {
        var v = await context.PerformerViews.AsNoTracking().FirstOrDefaultAsync(p => p.PerformerId == id, ct);
        return v is null ? null : MapView(v, includePrivateMeta);
    }

    public async Task<PerformerDto?> GetBySlugAsync(string slug, bool includePrivateMeta, CancellationToken ct = default)
    {
        var v = await context.PerformerViews.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug, ct);
        return v is null ? null : MapView(v, includePrivateMeta);
    }

    public async Task<PerformerDto> CreateAsync(CreatePerformerRequest request, CancellationToken ct = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        var baseSlug = NormalizeSlug(string.IsNullOrWhiteSpace(request.Slug) ? Slugify(name) : request.Slug);
        var slug = await EnsureUniqueSlugAsync(baseSlug, null, ct);
        var metaJson = SerializeMeta(request.Meta);

        var id = await CreateWithSlugRetryAsync(name, slug, baseSlug, request.PrimaryImagePath, metaJson, ct);
        var v = await context.PerformerViews.AsNoTracking().FirstAsync(p => p.PerformerId == id, ct);
        return MapView(v, true);
    }

    public async Task<PerformerDto?> UpdateAsync(Guid id, UpdatePerformerRequest request, CancellationToken ct = default)
    {
        var existing = await context.PerformerViews.AsNoTracking().FirstOrDefaultAsync(p => p.PerformerId == id, ct);
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

        var v = await context.PerformerViews.AsNoTracking().FirstAsync(p => p.PerformerId == id, ct);
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
                return await performerProc.CreatePerformerAsync(name, candidate, primaryImagePath, metaJson, ct);
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
                await performerProc.UpdatePerformerAsync(id, name, candidate, primaryImagePath, metaJson, ct);
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
        ex.SqlState == "23505" && ex.ConstraintName == "IX_performers_Slug";

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var exists = await context.PerformerViews.AsNoTracking().AnyAsync(p => p.PerformerId == id, ct);
        if (!exists) return false;
        await performerProc.DeletePerformerAsync(id, ct);
        return true;
    }

    public async Task SetEventPerformersAsync(Guid eventId, SetEventPerformersRequest request, CancellationToken ct = default)
    {
        var sanitized = request.Performers
            .Select((p, idx) => new
            {
                performerId = p.PerformerId,
                sortOrder = p.SortOrder >= 0 ? p.SortOrder : idx,
                eventMeta = p.EventMeta ?? Array.Empty<PerformerMetaItemDto>()
            })
            .ToList();
        var json = JsonSerializer.Serialize(sanitized, JsonOpts);
        await performerProc.SetEventPerformersAsync(eventId, json, ct);
    }

    public async Task<IReadOnlyList<EventPerformerDto>> GetEventPerformersAsync(Guid eventId, bool includePrivateMeta, CancellationToken ct = default)
    {
        var ev = await context.EventViews.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId, ct);
        if (ev is null) return Array.Empty<EventPerformerDto>();
        return await ParseEventViewPerformersAsync(ev.Performers, includePrivateMeta);
    }

    public Task<IReadOnlyList<EventPerformerDto>> ParseEventViewPerformersAsync(string performersJson, bool includePrivateMeta)
    {
        if (string.IsNullOrWhiteSpace(performersJson)) return Task.FromResult<IReadOnlyList<EventPerformerDto>>(Array.Empty<EventPerformerDto>());

        var rows = JsonSerializer.Deserialize<List<EventPerformerProjection>>(performersJson, JsonOpts)
                   ?? new List<EventPerformerProjection>();

        var dtos = rows
            .OrderBy(r => r.SortOrder)
            .Select(r =>
            {
                var meta = includePrivateMeta
                    ? r.EffectiveMeta
                    : r.EffectiveMeta.Where(m => m.IsPublic).ToList();
                var metaDtos = meta
                    .OrderBy(m => m.SortOrder)
                    .Select(m => new PerformerMetaItemDto(m.Key, m.Value, m.IsPublic, m.SortOrder))
                    .ToList();
                return new EventPerformerDto(
                    r.PerformerId,
                    r.Name,
                    r.Slug,
                    string.IsNullOrEmpty(r.PrimaryImagePath) ? null : fileStorage.GetPublicUrl(r.PrimaryImagePath),
                    r.SortOrder,
                    metaDtos
                );
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<EventPerformerDto>>(dtos);
    }

    private PerformerDto MapView(PerformerView v, bool includePrivate)
    {
        var meta = ParseMeta(v.Meta);
        if (!includePrivate) meta = meta.Where(m => m.IsPublic).ToList();
        var metaDtos = meta
            .OrderBy(m => m.SortOrder)
            .Select(m => new PerformerMetaItemDto(m.Key, m.Value, m.IsPublic, m.SortOrder))
            .ToList();
        var imageUrl = string.IsNullOrEmpty(v.PrimaryImagePath) ? null : fileStorage.GetPublicUrl(v.PrimaryImagePath);
        return new PerformerDto(
            v.PerformerId,
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

    private static List<PerformerMetaItem> ParseMeta(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<PerformerMetaItem>();
        try
        {
            return JsonSerializer.Deserialize<List<PerformerMetaItem>>(json, JsonOpts) ?? new List<PerformerMetaItem>();
        }
        catch (JsonException)
        {
            return new List<PerformerMetaItem>();
        }
    }

    private static string SerializeMeta(IReadOnlyList<PerformerMetaItemDto>? meta)
    {
        if (meta is null || meta.Count == 0) return "[]";
        var cleaned = meta
            .Where(m => !string.IsNullOrWhiteSpace(m.Key))
            .Select((m, idx) => new PerformerMetaItem
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
        while (await context.PerformerViews.AsNoTracking().AnyAsync(p => p.Slug == candidate && (excludeId == null || p.PerformerId != excludeId), ct))
        {
            candidate = $"{baseSlug}-{i++}";
            if (candidate.Length > 220) candidate = candidate[..220];
        }
        return candidate;
    }

    private static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "performer";
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
        if (s.Length == 0) s = "performer";
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
        if (result.Length == 0) result = "performer";
        if (result.Length > 220) result = result[..220];
        return result;
    }
}
