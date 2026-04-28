CREATE OR REPLACE VIEW v_platform_images AS
SELECT
    pi."Id"          AS "PlatformImageId",
    i."Id"           AS "ImageId",
    pi."Tag"         AS "Tag",
    i."StorageKey"   AS "StorageKey",
    i."OriginalName" AS "OriginalName",
    i."SizeBytes"    AS "SizeBytes",
    i."Width"        AS "Width",
    i."Height"       AS "Height",
    i."ContentType"  AS "ContentType",
    i."AltText"      AS "AltText",
    i."Caption"      AS "Caption",
    pi."IsPrimary"   AS "IsPrimary",
    pi."SortOrder"   AS "SortOrder",
    i."CreatedAt"    AS "CreatedAt"
FROM platform_images pi
JOIN images i ON i."Id" = pi."ImageId";
