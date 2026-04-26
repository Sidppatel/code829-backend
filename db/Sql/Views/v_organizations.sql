CREATE OR REPLACE VIEW v_organizations AS
SELECT
    o."Id" AS "OrganizationId",
    o."Name",
    o."LegalName",
    o."CountryCode",
    o."StripeConnectedAccountId",
    o."StripeChargesEnabled",
    o."StripePayoutsEnabled",
    o."StripeDetailsSubmitted",
    o."StripeOnboardedAt",
    o."CreatedAt",
    o."ArchivedAt",
    COALESCE(mc.cnt, 0)::int AS "MemberCount",
    COALESCE(ec.cnt, 0)::int AS "EventCount",
    COALESCE(rev.total, 0)::bigint AS "TotalRevenueCents"
FROM organizations o
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS cnt FROM business_users bu WHERE bu."OrganizationId" = o."Id"
) mc ON true
LEFT JOIN LATERAL (
    SELECT COUNT(*)::int AS cnt 
    FROM events e 
    JOIN business_users bu ON bu."Id" = e."BusinessUserId"
    WHERE bu."OrganizationId" = o."Id"
) ec ON true
LEFT JOIN LATERAL (
    SELECT SUM(p."TotalCents")::bigint AS total
    FROM purchases p
    JOIN events e ON e."Id" = p."EventId"
    JOIN business_users bu ON bu."Id" = e."BusinessUserId"
    WHERE bu."OrganizationId" = o."Id"
      AND p."Status" IN ('Paid', 'CheckedIn')
) rev ON true;
