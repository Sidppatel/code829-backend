CREATE EXTENSION IF NOT EXISTS pg_trgm;


CREATE TABLE addresses (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "Line1" character varying(512) NOT NULL,
    "Line2" character varying(512),
    "City" character varying(128) NOT NULL,
    "State" character varying(2) NOT NULL,
    "ZipCode" character varying(10) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_addresses" PRIMARY KEY ("Id")
);


CREATE TABLE admin_logs (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "Timestamp" timestamp with time zone NOT NULL DEFAULT (now()),
    "Action" character varying(128) NOT NULL,
    "ActorId" uuid,
    "ActorEmail" character varying(256),
    "ActorRole" character varying(20),
    "EntityType" character varying(64),
    "EntityId" uuid,
    "Description" character varying(2048),
    "MetadataJson" text,
    "IpAddress" character varying(45),
    CONSTRAINT "PK_admin_logs" PRIMARY KEY ("Id")
);


CREATE TABLE admin_users (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "Email" character varying(256) NOT NULL,
    "EmailHash" character varying(128) NOT NULL,
    "FirstName" character varying(128) NOT NULL,
    "LastName" character varying(128) NOT NULL,
    "PasswordHash" character varying(256) NOT NULL,
    "Role" character varying(20) NOT NULL,
    "IsActive" boolean NOT NULL,
    "LastLoginAt" timestamp with time zone,
    "FailedLoginAttempts" integer NOT NULL,
    "LockedUntil" timestamp with time zone,
    "AvatarPath" character varying(512),
    "Phone" character varying(20),
    "StripeConnectedAccountId" text,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_admin_users" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_admin_users_Role" CHECK ("Role" IN ('Staff','Admin','Developer'))
);


CREATE TABLE app_settings (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "Key" character varying(128) NOT NULL,
    "Value" character varying(4096) NOT NULL,
    "Description" character varying(512),
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_app_settings" PRIMARY KEY ("Id")
);


CREATE TABLE developer_logs (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "Timestamp" timestamp with time zone NOT NULL DEFAULT (now()),
    "Severity" character varying(20) NOT NULL,
    "Message" character varying(4096) NOT NULL,
    "ExceptionType" character varying(512),
    "StackTrace" text,
    "RequestPath" character varying(512),
    "RequestMethod" character varying(10),
    "StatusCode" integer,
    "UserId" uuid,
    "IpAddress" character varying(45),
    "CorrelationId" character varying(64),
    "MetadataJson" text,
    CONSTRAINT "PK_developer_logs" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_developer_logs_Severity" CHECK ("Severity" IN ('Warning','Error','Critical'))
);


CREATE TABLE email_logs (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "Timestamp" timestamp with time zone NOT NULL DEFAULT (now()),
    "Recipient" character varying(256) NOT NULL,
    "Subject" character varying(512) NOT NULL,
    "Body" text NOT NULL,
    "Status" character varying(20),
    CONSTRAINT "PK_email_logs" PRIMARY KEY ("Id")
);


CREATE TABLE magic_link_tokens (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "TokenHash" character varying(128) NOT NULL,
    "Email" character varying(256) NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "IsUsed" boolean NOT NULL,
    "UsedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_magic_link_tokens" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_magic_link_tokens_Usage" CHECK (("IsUsed" = false AND "UsedAt" IS NULL) OR ("IsUsed" = true AND "UsedAt" IS NOT NULL))
);


CREATE TABLE system_logs (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "Timestamp" timestamp with time zone NOT NULL DEFAULT (now()),
    "Category" character varying(30) NOT NULL,
    "Action" character varying(64) NOT NULL,
    "Source" character varying(256),
    "EntityType" character varying(64),
    "EntityId" uuid,
    "BeforeJson" text,
    "AfterJson" text,
    "ActorId" uuid,
    "CorrelationId" character varying(64),
    "DurationMs" bigint,
    "MetadataJson" text,
    CONSTRAINT "PK_system_logs" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_system_logs_Category" CHECK ("Category" IN ('EntityChange','BackgroundWorker','Cache','MockService','Migration')),
    CONSTRAINT "CK_system_logs_DurationMs" CHECK ("DurationMs" IS NULL OR "DurationMs" >= 0)
);


CREATE TABLE table_templates (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "Name" character varying(128) NOT NULL,
    "DefaultCapacity" integer NOT NULL,
    "DefaultShape" character varying(20) NOT NULL,
    "DefaultColor" character varying(20),
    "DefaultPriceCents" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_table_templates" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_table_templates_DefaultCapacity" CHECK ("DefaultCapacity" > 0),
    CONSTRAINT "CK_table_templates_DefaultPriceCents" CHECK ("DefaultPriceCents" >= 0),
    CONSTRAINT "CK_table_templates_DefaultShape" CHECK ("DefaultShape" IN ('Round','Rectangle','Square','Cocktail'))
);


CREATE TABLE users (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "Email" character varying(256) NOT NULL,
    "EmailHash" character varying(128) NOT NULL,
    "FirstName" character varying(128) NOT NULL,
    "LastName" character varying(128) NOT NULL,
    "IsActive" boolean NOT NULL,
    "LastLoginAt" timestamp with time zone,
    "AddressId" uuid,
    "Phone" text,
    "OptInLocationEmail" boolean NOT NULL,
    "HasCompletedOnboarding" boolean NOT NULL,
    "StripeConnectedAccountId" text,
    "AvatarPath" character varying(512),
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_users" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_users_addresses_AddressId" FOREIGN KEY ("AddressId") REFERENCES addresses ("Id") ON DELETE SET NULL
);


CREATE TABLE venues (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "Name" character varying(256) NOT NULL,
    "Description" character varying(4096),
    "ImagePath" character varying(512),
    "Phone" character varying(20),
    "Email" character varying(256),
    "Website" character varying(512),
    "IsActive" boolean NOT NULL,
    "AddressId" uuid,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_venues" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_venues_addresses_AddressId" FOREIGN KEY ("AddressId") REFERENCES addresses ("Id") ON DELETE SET NULL
);


CREATE TABLE admin_password_reset_tokens (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "AdminUserId" uuid NOT NULL,
    "TokenHash" character varying(128) NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "IsUsed" boolean NOT NULL,
    "UsedAt" timestamp with time zone,
    "Email" character varying(256),
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_admin_password_reset_tokens" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_admin_password_reset_tokens_Usage" CHECK (("IsUsed" = false AND "UsedAt" IS NULL) OR ("IsUsed" = true AND "UsedAt" IS NOT NULL)),
    CONSTRAINT "FK_admin_password_reset_tokens_admin_users_AdminUserId" FOREIGN KEY ("AdminUserId") REFERENCES admin_users ("Id") ON DELETE CASCADE
);


CREATE TABLE invitations (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "Email" character varying(256) NOT NULL,
    "TokenHash" character varying(128) NOT NULL,
    "Role" character varying(20) NOT NULL,
    "InvitedByAdminUserId" uuid NOT NULL,
    "Status" character varying(20) NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "AcceptedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_invitations" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_invitations_Role" CHECK ("Role" IN ('Staff','Admin','Developer')),
    CONSTRAINT "CK_invitations_Status" CHECK ("Status" IN ('Pending','Accepted','Revoked','Expired')),
    CONSTRAINT "FK_invitations_admin_users_InvitedByAdminUserId" FOREIGN KEY ("InvitedByAdminUserId") REFERENCES admin_users ("Id") ON DELETE CASCADE
);


CREATE TABLE device_sessions (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "UserId" uuid,
    "AdminUserId" uuid,
    "SessionHash" character varying(128) NOT NULL,
    "DeviceFingerprint" character varying(256),
    "DeviceName" character varying(256),
    "IpAddress" character varying(45),
    "LastActivityAt" timestamp with time zone NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "RevokedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_device_sessions" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_device_sessions_UserType" CHECK (("UserId" IS NOT NULL AND "AdminUserId" IS NULL) OR ("UserId" IS NULL AND "AdminUserId" IS NOT NULL)),
    CONSTRAINT "FK_device_sessions_admin_users_AdminUserId" FOREIGN KEY ("AdminUserId") REFERENCES admin_users ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_device_sessions_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
);


CREATE TABLE feedbacks (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "Name" character varying(100) NOT NULL,
    "Email" character varying(256),
    "Type" character varying(20) NOT NULL,
    "Message" character varying(2000) NOT NULL,
    "Rating" integer NOT NULL,
    "UserId" uuid,
    "UserAgent" character varying(512),
    "IpAddress" character varying(45),
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_feedbacks" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_feedbacks_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE SET NULL
);


CREATE TABLE images (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "EntityType" character varying(20) NOT NULL,
    "EntityId" uuid NOT NULL,
    "StorageKey" character varying(500) NOT NULL,
    "OriginalName" character varying(255),
    "SizeBytes" integer NOT NULL,
    "Width" integer NOT NULL,
    "Height" integer NOT NULL,
    "IsPrimary" boolean NOT NULL,
    "SortOrder" integer NOT NULL,
    "UploadedById" uuid,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_images" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_images_users_UploadedById" FOREIGN KEY ("UploadedById") REFERENCES users ("Id") ON DELETE SET NULL
);


CREATE TABLE events (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "Title" character varying(256) NOT NULL,
    "Slug" character varying(300) NOT NULL,
    "Description" character varying(8192),
    "Status" character varying(20) NOT NULL,
    "Category" character varying(20),
    "StartDate" timestamp with time zone NOT NULL,
    "EndDate" timestamp with time zone NOT NULL,
    "ImagePath" character varying(512),
    "IsFeatured" boolean NOT NULL,
    "LayoutMode" character varying(20) NOT NULL,
    "MaxCapacity" integer,
    "PublishedAt" timestamp with time zone,
    "ScheduledPublishAt" timestamp with time zone,
    "GridRows" integer,
    "GridCols" integer,
    "SearchVector" tsvector GENERATED ALWAYS AS (to_tsvector('english', "Title" || ' ' || coalesce("Description", ''))) STORED,
    "VenueId" uuid NOT NULL,
    "OrganizerId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_events" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_events_Category" CHECK ("Category" IS NULL OR "Category" IN ('Music','Business','Social','Dining','Tech','Arts','Family','Sports')),
    CONSTRAINT "CK_events_CompletedRequiresPublish" CHECK ("Status" <> 'Completed' OR "PublishedAt" IS NOT NULL),
    CONSTRAINT "CK_events_DateRange" CHECK ("EndDate" > "StartDate"),
    CONSTRAINT "CK_events_DraftNoPublishDate" CHECK ("Status" <> 'Draft' OR "PublishedAt" IS NULL),
    CONSTRAINT "CK_events_GridDimensions" CHECK (("GridRows" IS NULL OR "GridRows" > 0) AND ("GridCols" IS NULL OR "GridCols" > 0)),
    CONSTRAINT "CK_events_LayoutMode" CHECK ("LayoutMode" IN ('Grid','Open')),
    CONSTRAINT "CK_events_MaxCapacity" CHECK ("MaxCapacity" IS NULL OR "MaxCapacity" > 0),
    CONSTRAINT "CK_events_PublishLifecycle" CHECK ("Status" <> 'Published' OR "PublishedAt" IS NOT NULL),
    CONSTRAINT "CK_events_Status" CHECK ("Status" IN ('Draft','Published','Completed','Cancelled')),
    CONSTRAINT "FK_events_admin_users_OrganizerId" FOREIGN KEY ("OrganizerId") REFERENCES admin_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_events_venues_VenueId" FOREIGN KEY ("VenueId") REFERENCES venues ("Id") ON DELETE RESTRICT
);


CREATE TABLE event_tables (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "Label" character varying(128) NOT NULL,
    "Capacity" integer NOT NULL,
    "Shape" character varying(20) NOT NULL,
    "Color" character varying(20),
    "PriceCents" integer NOT NULL,
    "PlatformFeeCents" integer,
    "IsActive" boolean NOT NULL,
    "EventId" uuid NOT NULL,
    "TableTemplateId" uuid,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_event_tables" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_event_tables_Capacity" CHECK ("Capacity" > 0),
    CONSTRAINT "CK_event_tables_PriceCents" CHECK ("PriceCents" >= 0),
    CONSTRAINT "CK_event_tables_Shape" CHECK ("Shape" IN ('Round','Rectangle','Square','Cocktail')),
    CONSTRAINT "FK_event_tables_events_EventId" FOREIGN KEY ("EventId") REFERENCES events ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_event_tables_table_templates_TableTemplateId" FOREIGN KEY ("TableTemplateId") REFERENCES table_templates ("Id") ON DELETE SET NULL
);


CREATE TABLE event_ticket_types (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "Label" character varying(128) NOT NULL,
    "PriceCents" integer NOT NULL,
    "PlatformFeeCents" integer,
    "MaxQuantity" integer,
    "SortOrder" integer NOT NULL,
    "Description" text,
    "IsActive" boolean NOT NULL,
    "EventId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_event_ticket_types" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_event_ticket_types_MaxQuantity" CHECK ("MaxQuantity" IS NULL OR "MaxQuantity" > 0),
    CONSTRAINT "CK_event_ticket_types_PriceCents" CHECK ("PriceCents" >= 0),
    CONSTRAINT "CK_event_ticket_types_SortOrder" CHECK ("SortOrder" >= 0),
    CONSTRAINT "FK_event_ticket_types_events_EventId" FOREIGN KEY ("EventId") REFERENCES events ("Id") ON DELETE CASCADE
);


CREATE TABLE tables (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "Label" character varying(20) NOT NULL,
    "GridRow" integer NOT NULL,
    "GridCol" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL,
    "Status" character varying(20) NOT NULL DEFAULT 'Available',
    "LockedByUserId" uuid,
    "LockExpiresAt" timestamp with time zone,
    "EventTableId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_tables" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_tables_AvailableNoLock" CHECK ("Status" <> 'Available' OR ("LockedByUserId" IS NULL AND "LockExpiresAt" IS NULL)),
    CONSTRAINT "CK_tables_GridCol" CHECK ("GridCol" >= 0),
    CONSTRAINT "CK_tables_GridRow" CHECK ("GridRow" >= 0),
    CONSTRAINT "CK_tables_LockedRequiresOwner" CHECK ("Status" <> 'Locked' OR ("LockedByUserId" IS NOT NULL AND "LockExpiresAt" IS NOT NULL)),
    CONSTRAINT "CK_tables_Status" CHECK ("Status" IN ('Available','Locked','Booked')),
    CONSTRAINT "FK_tables_event_tables_EventTableId" FOREIGN KEY ("EventTableId") REFERENCES event_tables ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_tables_events_EventId" FOREIGN KEY ("EventId") REFERENCES events ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_tables_users_LockedByUserId" FOREIGN KEY ("LockedByUserId") REFERENCES users ("Id") ON DELETE SET NULL
);


CREATE TABLE purchases (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "PurchaseNumber" character varying(20) NOT NULL,
    "Status" character varying(20) NOT NULL,
    "UserId" uuid NOT NULL,
    "EventId" uuid NOT NULL,
    "SubtotalCents" integer NOT NULL,
    "FeeCents" integer NOT NULL,
    "TotalCents" integer NOT NULL,
    "QrToken" character varying(128),
    "TableId" uuid,
    "SeatsReserved" integer,
    "EventTicketTypeId" uuid,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_purchases" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_purchases_FeeCents" CHECK ("FeeCents" >= 0),
    CONSTRAINT "CK_purchases_SeatsReserved" CHECK ("SeatsReserved" IS NULL OR "SeatsReserved" > 0),
    CONSTRAINT "CK_purchases_Status" CHECK ("Status" IN ('Pending','Paid','CheckedIn','Cancelled','Refunded','Expired')),
    CONSTRAINT "CK_purchases_SubtotalCents" CHECK ("SubtotalCents" >= 0),
    CONSTRAINT "CK_purchases_TotalCents" CHECK ("TotalCents" >= 0),
    CONSTRAINT "CK_purchases_TotalFormula" CHECK ("TotalCents" = "SubtotalCents" + "FeeCents"),
    CONSTRAINT "FK_purchases_event_ticket_types_EventTicketTypeId" FOREIGN KEY ("EventTicketTypeId") REFERENCES event_ticket_types ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_purchases_events_EventId" FOREIGN KEY ("EventId") REFERENCES events ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_purchases_tables_TableId" FOREIGN KEY ("TableId") REFERENCES tables ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_purchases_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE RESTRICT
);


CREATE TABLE purchase_tickets (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "TicketCode" character varying(20) NOT NULL,
    "QrToken" character varying(128) NOT NULL,
    "SeatNumber" integer NOT NULL,
    "PurchaseId" uuid NOT NULL,
    "GuestUserId" uuid,
    "InviteTokenHash" character varying(128),
    "InviteExpiresAt" timestamp with time zone,
    "InvitedEmail" character varying(256),
    "InviteSentAt" timestamp with time zone,
    "ClaimedAt" timestamp with time zone,
    "Status" character varying(20) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_purchase_tickets" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_purchase_tickets_SeatNumber" CHECK ("SeatNumber" > 0),
    CONSTRAINT "CK_purchase_tickets_Status" CHECK ("Status" IN ('Unassigned','Invited','Claimed','CheckedIn')),
    CONSTRAINT "FK_purchase_tickets_purchases_PurchaseId" FOREIGN KEY ("PurchaseId") REFERENCES purchases ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_purchase_tickets_users_GuestUserId" FOREIGN KEY ("GuestUserId") REFERENCES users ("Id") ON DELETE SET NULL
);


CREATE TABLE stripe_transactions (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "PurchaseId" uuid NOT NULL,
    "PaymentIntentId" character varying(128) NOT NULL,
    "Status" character varying(30) NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "AmountCents" integer NOT NULL,
    "TransferAmountCents" integer,
    "TaxCalculationId" character varying(128),
    "TaxTransactionId" character varying(128),
    "TotalChargedCents" integer,
    "TaxAmountCents" integer,
    "StripeFeesCents" integer,
    "PaidAt" timestamp with time zone,
    "RefundId" character varying(128),
    "RefundedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT "PK_stripe_transactions" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_stripe_transactions_AmountCents" CHECK ("AmountCents" >= 0),
    CONSTRAINT "CK_stripe_transactions_Currency" CHECK ("Currency" IN ('usd')),
    CONSTRAINT "CK_stripe_transactions_NotRefundedNoRefundDate" CHECK ("Status" = 'Refunded' OR "RefundedAt" IS NULL),
    CONSTRAINT "CK_stripe_transactions_PaidLifecycle" CHECK ("Status" NOT IN ('Succeeded','Refunded') OR "PaidAt" IS NOT NULL),
    CONSTRAINT "CK_stripe_transactions_PendingNoPaidDate" CHECK ("Status" NOT IN ('RequiresConfirmation','Failed') OR "PaidAt" IS NULL),
    CONSTRAINT "CK_stripe_transactions_RefundLifecycle" CHECK ("Status" <> 'Refunded' OR "RefundedAt" IS NOT NULL),
    CONSTRAINT "CK_stripe_transactions_Status" CHECK ("Status" IN ('RequiresConfirmation','Succeeded','Failed','Refunded')),
    CONSTRAINT "CK_stripe_transactions_StripeFees" CHECK ("StripeFeesCents" IS NULL OR "StripeFeesCents" >= 0),
    CONSTRAINT "CK_stripe_transactions_TaxAmount" CHECK ("TaxAmountCents" IS NULL OR "TaxAmountCents" >= 0),
    CONSTRAINT "CK_stripe_transactions_TotalCharged" CHECK ("TotalChargedCents" IS NULL OR "TotalChargedCents" >= 0),
    CONSTRAINT "CK_stripe_transactions_TransferAmount" CHECK ("TransferAmountCents" IS NULL OR "TransferAmountCents" >= 0),
    CONSTRAINT "FK_stripe_transactions_purchases_PurchaseId" FOREIGN KEY ("PurchaseId") REFERENCES purchases ("Id") ON DELETE RESTRICT
);


CREATE INDEX "IX_admin_logs_Action" ON admin_logs ("Action");


CREATE INDEX "IX_admin_logs_Timestamp" ON admin_logs ("Timestamp");


CREATE INDEX "IX_admin_password_reset_tokens_AdminUserId" ON admin_password_reset_tokens ("AdminUserId");


CREATE INDEX "IX_admin_password_reset_tokens_ExpiresAt" ON admin_password_reset_tokens ("ExpiresAt");


CREATE UNIQUE INDEX "IX_admin_password_reset_tokens_TokenHash" ON admin_password_reset_tokens ("TokenHash");


CREATE UNIQUE INDEX "IX_admin_users_Email" ON admin_users ("Email");


CREATE UNIQUE INDEX "IX_admin_users_EmailHash" ON admin_users ("EmailHash");


CREATE UNIQUE INDEX "IX_app_settings_Key" ON app_settings ("Key");


CREATE UNIQUE INDEX "IX_purchase_tickets_PurchaseId_SeatNumber" ON purchase_tickets ("PurchaseId", "SeatNumber");


CREATE INDEX "IX_purchase_tickets_GuestUserId" ON purchase_tickets ("GuestUserId");


CREATE UNIQUE INDEX "IX_purchase_tickets_InviteTokenHash" ON purchase_tickets ("InviteTokenHash") WHERE "InviteTokenHash" IS NOT NULL;


CREATE UNIQUE INDEX "IX_purchase_tickets_QrToken" ON purchase_tickets ("QrToken");


CREATE UNIQUE INDEX "IX_purchases_PurchaseNumber" ON purchases ("PurchaseNumber");


CREATE INDEX "IX_purchases_EventId_Status" ON purchases ("EventId", "Status");


CREATE INDEX "IX_purchases_EventTicketTypeId" ON purchases ("EventTicketTypeId");


CREATE UNIQUE INDEX "IX_purchases_QrToken" ON purchases ("QrToken") WHERE "QrToken" IS NOT NULL;


CREATE INDEX "IX_purchases_Status" ON purchases ("Status");


CREATE INDEX "IX_purchases_TableId" ON purchases ("TableId");


CREATE INDEX "IX_purchases_UserId" ON purchases ("UserId");


CREATE INDEX "IX_purchases_UserId_CreatedAt" ON purchases ("UserId", "CreatedAt");


CREATE INDEX "IX_developer_logs_Severity" ON developer_logs ("Severity");


CREATE INDEX "IX_developer_logs_Timestamp" ON developer_logs ("Timestamp");


CREATE INDEX "IX_device_sessions_Active" ON device_sessions ("ExpiresAt", "RevokedAt") WHERE "RevokedAt" IS NULL;


CREATE INDEX "IX_device_sessions_AdminUserId" ON device_sessions ("AdminUserId");


CREATE UNIQUE INDEX "IX_device_sessions_SessionHash" ON device_sessions ("SessionHash");


CREATE INDEX "IX_device_sessions_UserId" ON device_sessions ("UserId");


CREATE INDEX "IX_email_logs_Timestamp" ON email_logs ("Timestamp");


CREATE INDEX "IX_event_tables_EventId_Label" ON event_tables ("EventId", "Label");


CREATE INDEX "IX_event_tables_TableTemplateId" ON event_tables ("TableTemplateId");


CREATE INDEX "IX_event_ticket_types_EventId_Label" ON event_ticket_types ("EventId", "Label");


CREATE INDEX "IX_event_ticket_types_EventId_SortOrder" ON event_ticket_types ("EventId", "SortOrder");


CREATE INDEX "IX_events_Category" ON events ("Category");


CREATE INDEX "IX_events_OrganizerId" ON events ("OrganizerId");


CREATE INDEX "IX_events_SearchVector" ON events USING GIN ("SearchVector");


CREATE UNIQUE INDEX "IX_events_Slug" ON events ("Slug");


CREATE INDEX "IX_events_StartDate" ON events ("StartDate");


CREATE INDEX "IX_events_Status" ON events ("Status");


CREATE INDEX "IX_events_Status_StartDate" ON events ("Status", "StartDate");


CREATE INDEX "IX_events_VenueId" ON events ("VenueId");


CREATE INDEX "IX_feedbacks_CreatedAt" ON feedbacks ("CreatedAt");


CREATE INDEX "IX_feedbacks_Type" ON feedbacks ("Type");


CREATE INDEX "IX_feedbacks_UserId" ON feedbacks ("UserId");


CREATE INDEX "IX_images_EntityType_EntityId" ON images ("EntityType", "EntityId");


CREATE INDEX "IX_images_UploadedById" ON images ("UploadedById");


CREATE INDEX "IX_invitations_Email" ON invitations ("Email");


CREATE INDEX "IX_invitations_InvitedByAdminUserId" ON invitations ("InvitedByAdminUserId");


CREATE UNIQUE INDEX "IX_invitations_TokenHash" ON invitations ("TokenHash");


CREATE INDEX "IX_magic_link_tokens_Email" ON magic_link_tokens ("Email");


CREATE INDEX "IX_magic_link_tokens_ExpiresAt" ON magic_link_tokens ("ExpiresAt");


CREATE UNIQUE INDEX "IX_magic_link_tokens_TokenHash" ON magic_link_tokens ("TokenHash");


CREATE UNIQUE INDEX "IX_stripe_transactions_PurchaseId" ON stripe_transactions ("PurchaseId");


CREATE UNIQUE INDEX "IX_stripe_transactions_PaymentIntentId" ON stripe_transactions ("PaymentIntentId");


CREATE INDEX "IX_stripe_transactions_Status_PaidAt" ON stripe_transactions ("Status", "PaidAt");


CREATE INDEX "IX_system_logs_Category" ON system_logs ("Category");


CREATE INDEX "IX_system_logs_Timestamp" ON system_logs ("Timestamp");


CREATE INDEX "IX_tables_EventId" ON tables ("EventId");


CREATE UNIQUE INDEX "IX_tables_EventId_GridRow_GridCol" ON tables ("EventId", "GridRow", "GridCol");


CREATE UNIQUE INDEX "IX_tables_EventId_Label" ON tables ("EventId", "Label");


CREATE INDEX "IX_tables_EventId_Status" ON tables ("EventId", "Status");


CREATE INDEX "IX_tables_EventTableId" ON tables ("EventTableId");


CREATE INDEX "IX_tables_LockedByUserId" ON tables ("LockedByUserId");


CREATE INDEX "IX_users_AddressId" ON users ("AddressId");


CREATE UNIQUE INDEX "IX_users_Email" ON users ("Email");


CREATE UNIQUE INDEX "IX_users_EmailHash" ON users ("EmailHash");


CREATE INDEX "IX_venues_AddressId" ON venues ("AddressId");


CREATE INDEX "IX_venues_Name" ON venues ("Name");


