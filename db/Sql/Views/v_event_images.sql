CREATE OR REPLACE VIEW v_event_images AS
SELECT
    ei."Id"          AS "EventImageId",
    ei."EventId"     AS "EventId",
    i."Id"           AS "ImageId",
    i."StorageKey"   AS "StorageKey",
    i."OriginalName" AS "OriginalName",
    i."SizeBytes"    AS "SizeBytes",
    i."Width"        AS "Width",
    i."Height"       AS "Height",
    i."ContentType"  AS "ContentType",
    i."AltText"      AS "AltText",
    i."Caption"      AS "Caption",
    ei."IsPrimary"   AS "IsPrimary",
    ei."SortOrder"   AS "SortOrder",
    i."CreatedAt"    AS "CreatedAt"
FROM event_images ei
JOIN images i ON i."Id" = ei."ImageId";
