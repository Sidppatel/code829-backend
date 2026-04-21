CREATE OR REPLACE VIEW v_event_summary AS
SELECT
    e."Id" AS "EventId",
    e."Title" AS "Title",
    e."Slug" AS "Slug",
    e."Status"::text AS "Status",
    COALESCE(e."Category"::text, '') AS "Category",
    e."StartDate" AS "StartDate",
    e."EndDate" AS "EndDate",
    e."ImagePath" AS "ImagePath",
    img."StorageKey" AS "PrimaryImageKey",
    e."IsFeatured" AS "IsFeatured",
    e."LayoutMode"::text AS "LayoutMode",
    ettp.min_price::int AS "PricePerPersonCents",
    e."MaxCapacity" AS "MaxCapacity",
    e."VenueId" AS "VenueId",
    v."Name" AS "VenueName",
    COALESCE(a."City", '') AS "VenueCity",
    COALESCE(a."State", '') AS "VenueState",
    e."BusinessUserId" AS "BusinessUserId",
    COALESCE(au."FirstName" || ' ' || au."LastName", '') AS "OrganizerName",
    COALESCE(
        e."MaxCapacity",
        CASE
            WHEN e."LayoutMode"::text = 'Grid' THEN table_cap.total_seats
            ELSE ett_cap.total_qty
        END,
        0
    )::int AS "TotalCapacity",
    COALESCE(bs.sold, 0)::int AS "TotalSold",
    COALESCE(ts.available, 0)::int AS "AvailableTables",
    ts.min_price::int AS "MinTablePriceCents",
    ettp.min_price::int AS "MinTicketTypePriceCents",
    ts.min_total_price::int AS "DisplayMinTablePriceCents",
    ettp.min_total_price::int AS "DisplayMinTicketTypePriceCents",
    e."CreatedAt" AS "CreatedAt"
FROM events e
JOIN venues v ON e."VenueId" = v."Id"
LEFT JOIN addresses a ON v."AddressId" = a."Id"
LEFT JOIN business_users au ON e."BusinessUserId" = au."Id"
LEFT JOIN LATERAL (
    SELECT i."StorageKey"
    FROM event_images ei
    JOIN images i ON i."Id" = ei."ImageId"
    WHERE ei."EventId" = e."Id" AND ei."IsPrimary" = true
    LIMIT 1
) img ON true
LEFT JOIN LATERAL (
    SELECT COALESCE(SUM(b."SeatsReserved"), COUNT(*))::int AS sold
    FROM purchases b
    WHERE b."EventId" = e."Id" AND b."Status" IN ('Paid','CheckedIn')
) bs ON true
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS available, MIN(et."PriceCents") AS min_price, MIN(et."PriceCents" + COALESCE(et."PlatformFeeCents", 0)) AS min_total_price
    FROM tables t
    JOIN event_tables et ON t."EventTableId" = et."Id"
    WHERE t."EventId" = e."Id" AND t."IsActive" = true AND t."Status" = 'Available'
) ts ON true
LEFT JOIN LATERAL (
    SELECT MIN(ett."PriceCents") AS min_price, MIN(ett."PriceCents" + COALESCE(ett."PlatformFeeCents", 0)) AS min_total_price
    FROM event_ticket_types ett
    WHERE ett."EventId" = e."Id" AND ett."IsActive" = true
) ettp ON true
LEFT JOIN LATERAL (
    SELECT SUM(ett."MaxQuantity") AS total_qty
    FROM event_ticket_types ett
    WHERE ett."EventId" = e."Id" AND ett."IsActive" = true
) ett_cap ON true
LEFT JOIN LATERAL (
    SELECT COALESCE(SUM(et."Capacity"), 0)::int AS total_seats
    FROM tables t
    JOIN event_tables et ON t."EventTableId" = et."Id"
    WHERE t."EventId" = e."Id" AND t."IsActive" = true
) table_cap ON true;
