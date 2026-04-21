CREATE OR REPLACE VIEW v_events AS
SELECT
    e."Id" AS "EventId",
    e."Title" AS "Title",
    e."Slug" AS "Slug",
    e."Description" AS "Description",
    e."Status"::text AS "Status",
    COALESCE(e."Category"::text, '') AS "Category",
    e."StartDate" AS "StartDate",
    e."EndDate" AS "EndDate",
    e."ImagePath" AS "ImagePath",
    e."IsFeatured" AS "IsFeatured",
    e."LayoutMode"::text AS "LayoutMode",
    e."MaxCapacity" AS "MaxCapacity",
    ettp.min_price::int AS "PricePerPersonCents",
    e."GridRows" AS "GridRows",
    e."GridCols" AS "GridCols",
    e."PublishedAt" AS "PublishedAt",
    e."ScheduledPublishAt" AS "ScheduledPublishAt",
    e."VenueId" AS "VenueId",
    e."BusinessUserId" AS "BusinessUserId",
    e."CreatedAt" AS "CreatedAt",
    e."UpdatedAt" AS "UpdatedAt",
    v."Name" AS "VenueName",
    COALESCE(a."Line1", '') AS "VenueAddress",
    COALESCE(a."City", '') AS "VenueCity",
    COALESCE(a."State", '') AS "VenueState",
    COALESCE(a."ZipCode", '') AS "VenueZipCode",
    v."Description" AS "VenueDescription",
    v."ImagePath" AS "VenueImagePath",
    v."Phone" AS "VenuePhone",
    v."Email" AS "VenueEmail",
    v."Website" AS "VenueWebsite",
    v."IsActive" AS "VenueIsActive",
    v."CreatedAt" AS "VenueCreatedAt",
    COALESCE(au."FirstName", '') AS "OrganizerFirstName",
    COALESCE(au."LastName", '') AS "OrganizerLastName",
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
    ettp.min_total_price::int AS "DisplayMinTicketTypePriceCents"
FROM events e
JOIN venues v ON e."VenueId" = v."Id"
LEFT JOIN addresses a ON v."AddressId" = a."Id"
LEFT JOIN business_users au ON e."BusinessUserId" = au."Id"
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