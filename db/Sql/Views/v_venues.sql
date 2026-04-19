CREATE OR REPLACE VIEW v_venues AS
SELECT
    v."Id" AS "VenueId", v."Name", v."Description", v."ImagePath",
    v."Phone", v."Email", v."Website",
    v."IsActive", v."CreatedAt",
    COALESCE(a."Line1", '') AS "AddressLine1",
    a."Line2" AS "AddressLine2",
    COALESCE(a."City", '') AS "City",
    COALESCE(a."State", '') AS "State",
    COALESCE(a."ZipCode", '') AS "ZipCode",
    COALESCE(ec.cnt, 0)::int AS "EventCount",
    img."StorageKey" AS "PrimaryImageKey"
FROM venues v
LEFT JOIN addresses a ON v."AddressId" = a."Id"
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS cnt FROM events e WHERE e."VenueId" = v."Id"
) ec ON true
LEFT JOIN LATERAL (
    SELECT i."StorageKey"
    FROM venue_images vi
    JOIN images i ON i."Id" = vi."ImageId"
    WHERE vi."VenueId" = v."Id" AND vi."IsPrimary" = true
    LIMIT 1
) img ON true;
