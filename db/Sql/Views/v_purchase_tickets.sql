CREATE OR REPLACE VIEW v_purchase_tickets AS
SELECT
    bt."Id" AS "PurchaseTicketId", bt."TicketCode", bt."QrToken", bt."SeatNumber",
    bt."Status"::text,
    bt."CreatedAt",
    bt."InvitedEmail", bt."InviteSentAt", bt."InviteExpiresAt", bt."ClaimedAt",
    bt."PurchaseId",
    b."PurchaseNumber", b."Status"::text AS "PurchaseStatus",
    bt."GuestUserId",
    gu."Email" AS "GuestEmail",
    gu."FirstName" AS "GuestFirstName",
    gu."LastName" AS "GuestLastName",
    e."Id" AS "EventId",
    e."Title" AS "EventTitle",
    e."StartDate" AS "EventStartDate",
    e."EndDate" AS "EventEndDate",
    v."Name" AS "VenueName",
    COALESCE(addr."City", '') AS "VenueCity",
    b."UserId" AS "PurchaseUserId",
    bu."Email" AS "PurchaseUserEmail",
    bt."InviteTokenHash",
    bu."FirstName" AS "PurchaseUserFirstName",
    bu."LastName" AS "PurchaseUserLastName",
    b."TableId" AS "PurchaseTableId"
FROM purchase_tickets bt
JOIN purchases b ON bt."PurchaseId" = b."Id"
JOIN events e ON b."EventId" = e."Id"
JOIN venues v ON e."VenueId" = v."Id"
LEFT JOIN addresses addr ON v."AddressId" = addr."Id"
LEFT JOIN users gu ON bt."GuestUserId" = gu."Id"
JOIN users bu ON b."UserId" = bu."Id";
