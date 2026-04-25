CREATE OR REPLACE VIEW v_event_facets AS
SELECT
    e."Id"                       AS "EventId",
    e."Status"::text             AS "Status",
    e."EndDate"                  AS "EndDate",
    COALESCE(e."Category"::text, '') AS "Category",
    v."Id"                       AS "VenueId",
    v."Name"::text               AS "VenueName",
    COALESCE(addr."City", '')::text AS "VenueCity",
    ettp.min_price               AS "PricePerPersonCents"
FROM events e
JOIN venues v ON v."Id" = e."VenueId"
LEFT JOIN addresses addr ON v."AddressId" = addr."Id"
LEFT JOIN LATERAL (
    SELECT MIN(ett."PriceCents")::int AS min_price
    FROM event_ticket_types ett
    WHERE ett."EventId" = e."Id" AND ett."IsActive" = true
) ettp ON true;
