using System.Text.Json;
using Contracts.Enums;
using Db.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Db.Interceptors;

/// <summary>
/// Hooks into SaveChangesAsync to capture before/after state of all entity changes
/// as JSON diffs in the system_logs table. Skips logging changes to log tables themselves
/// to avoid infinite recursion.
/// </summary>
public class ChangeTrackingInterceptor : SaveChangesInterceptor
{
    private static readonly HashSet<Type> ExcludedTypes =
    [
        typeof(DeveloperLog),
        typeof(AdminLog),
        typeof(SystemLog),
        typeof(EmailLog)
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var entries = eventData.Context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => !ExcludedTypes.Contains(e.Entity.GetType()))
            .ToList();

        foreach (var entry in entries)
        {
            var entityType = entry.Entity.GetType().Name;
            var entityId = entry.Properties
                .FirstOrDefault(p => p.Metadata.Name == "Id")?.CurrentValue as Guid?;

            string? beforeJson = null;
            string? afterJson = null;
            string action;

            switch (entry.State)
            {
                case EntityState.Added:
                    action = "insert";
                    afterJson = SerializeEntity(entry);
                    break;
                case EntityState.Modified:
                    action = "update";
                    beforeJson = SerializeOriginalValues(entry);
                    afterJson = SerializeEntity(entry);
                    break;
                case EntityState.Deleted:
                    action = "delete";
                    beforeJson = SerializeEntity(entry);
                    break;
                default:
                    continue;
            }

            var log = new SystemLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Category = LogCategory.EntityChange,
                Action = action,
                Source = "ChangeTrackingInterceptor",
                EntityType = entityType,
                EntityId = entityId,
                BeforeJson = beforeJson,
                AfterJson = afterJson
            };

            eventData.Context.Set<SystemLog>().Add(log);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static string SerializeEntity(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in entry.Properties)
        {
            dict[prop.Metadata.Name] = prop.CurrentValue;
        }
        return JsonSerializer.Serialize(dict, JsonOptions);
    }

    private static string SerializeOriginalValues(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in entry.Properties)
        {
            dict[prop.Metadata.Name] = prop.OriginalValue;
        }
        return JsonSerializer.Serialize(dict, JsonOptions);
    }
}
