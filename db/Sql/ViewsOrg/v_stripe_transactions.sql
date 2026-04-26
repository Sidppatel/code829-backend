CREATE OR REPLACE VIEW v_stripe_transactions AS
SELECT
    st."Id" AS "TransactionId",
    st."PaymentIntentId",
    st."Status",
    st."AmountCents",
    st."Currency",
    st."PaidAt",
    st."RefundedAt",
    st."RefundId",
    st."TransferAmountCents",
    st."StripeFeesCents",
    st."TotalChargedCents",
    st."CreatedAt",
    p."Id" AS "PurchaseId",
    p."PurchaseNumber",
    p."Status" AS "PurchaseStatus",
    e."Id" AS "EventId",
    e."Title" AS "EventTitle",
    u."Id" AS "UserId",
    u."Email" AS "UserEmail",
    u."FirstName" AS "UserFirstName",
    u."LastName" AS "UserLastName",
    bu."OrganizationId" AS "OrganizationId"
FROM stripe_transactions st
JOIN purchases p ON p."Id" = st."PurchaseId"
JOIN events e ON e."Id" = p."EventId"
JOIN users u ON u."Id" = p."UserId"
JOIN business_users bu ON bu."Id" = e."BusinessUserId";
