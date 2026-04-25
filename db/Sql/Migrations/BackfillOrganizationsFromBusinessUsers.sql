-- Backfill organizations + business_users.OrganizationId from existing
-- BusinessUser rows.
--
-- Strategy (per the Connect plan, "Migration backfill" section):
--   Create a 1:1 Organization for every BusinessUser that already has a
--   StripeConnectedAccountId (already onboarded for payouts) OR has
--   Role = 'Admin' (likely to need payouts soon).
--   Other BusinessUsers (Staff/Developer with no Stripe account) stay
--   OrganizationId = NULL and can be linked later via the developer UI.
--
-- BusinessUser.Role is stored as the string enum name (HasConversion<string>),
-- not the integer ordinal — so the WHERE clause uses 'Admin', not 1.
--
-- BusinessUser does NOT carry BusinessName or CountryCode columns in the
-- current schema, so the Organization name is synthesized from the user's
-- first/last name. CountryCode falls back to the column default ('US').

-- 1) Create one Organization per qualifying BusinessUser.
INSERT INTO organizations (
    "Id",
    "Name",
    "LegalName",
    "CountryCode",
    "StripeConnectedAccountId",
    "StripeChargesEnabled",
    "StripePayoutsEnabled",
    "StripeDetailsSubmitted",
    "CreatedAt",
    "UpdatedAt"
)
SELECT
    gen_random_uuid(),
    bu."FirstName" || ' ' || bu."LastName" || '''s Organization',
    NULL,
    'US',
    bu."StripeConnectedAccountId",
    false,
    false,
    false,
    now(),
    now()
FROM business_users bu
WHERE (bu."StripeConnectedAccountId" IS NOT NULL OR bu."Role" = 'Admin')
  AND bu."OrganizationId" IS NULL
ON CONFLICT DO NOTHING;

-- 2a) Link every BusinessUser whose Stripe account matches an Organization
--     row (the only deterministic match — Stripe account IDs are unique).
UPDATE business_users bu
SET "OrganizationId" = o."Id",
    "UpdatedAt" = now()
FROM organizations o
WHERE bu."StripeConnectedAccountId" IS NOT NULL
  AND o."StripeConnectedAccountId" = bu."StripeConnectedAccountId"
  AND bu."OrganizationId" IS NULL;

-- 2b) Link Admin BusinessUsers who had no Stripe account (matched on the
--     synthesized Org name). One row per admin guaranteed by step 1.
UPDATE business_users bu
SET "OrganizationId" = o."Id",
    "UpdatedAt" = now()
FROM organizations o
WHERE bu."Role" = 'Admin'
  AND bu."StripeConnectedAccountId" IS NULL
  AND o."StripeConnectedAccountId" IS NULL
  AND o."Name" = bu."FirstName" || ' ' || bu."LastName" || '''s Organization'
  AND bu."OrganizationId" IS NULL;
