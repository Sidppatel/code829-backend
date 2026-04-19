CREATE OR REPLACE VIEW v_venue_images AS
SELECT
    vi."Id"          AS "VenueImageId",
    vi."VenueId"     AS "VenueId",
    i."Id"           AS "ImageId",
    i."StorageKey"   AS "StorageKey",
    i."OriginalName" AS "OriginalName",
    i."SizeBytes"    AS "SizeBytes",
    i."Width"        AS "Width",
    i."Height"       AS "Height",
    i."ContentType"  AS "ContentType",
    i."AltText"      AS "AltText",
    i."Caption"      AS "Caption",
    vi."IsPrimary"   AS "IsPrimary",
    vi."SortOrder"   AS "SortOrder",
    i."CreatedAt"    AS "CreatedAt"
FROM venue_images vi
JOIN images i ON i."Id" = vi."ImageId";
