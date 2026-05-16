using System.Text.Json;
using System.Text.Json.Nodes;
using Contracts.Enums;

namespace Api.Services;

public class AdminLogService(IAuditLogService audit) : IAdminLogService
{
    public async Task LogAsync(string action, string? entityType, Guid? entityId, string description,
        Guid? adminUserId = null, string? metadataJson = null, string? ipAddress = null)
    {
        var mergedMetadata = MergeDescription(metadataJson, description);

        await audit.LogAsync(
            eventType: action,
            actorType: AuditActorType.Admin,
            actorId: adminUserId,
            subjectType: entityType,
            subjectId: entityId,
            action: action,
            metadataJson: mergedMetadata,
            ip: ipAddress);
    }

    private static string MergeDescription(string? metadataJson, string? description)
    {
        JsonObject root;
        if (!string.IsNullOrWhiteSpace(metadataJson))
        {
            try
            {
                root = (JsonNode.Parse(metadataJson) as JsonObject) ?? new JsonObject();
            }
            catch (JsonException)
            {
                root = new JsonObject { ["raw"] = metadataJson };
            }
        }
        else
        {
            root = new JsonObject();
        }

        if (!string.IsNullOrWhiteSpace(description))
            root["description"] = description;

        return root.ToJsonString();
    }
}
