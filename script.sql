CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE addresses (
        "Id" uuid NOT NULL,
        "Line1" character varying(512) NOT NULL,
        "Line2" character varying(512),
        "City" character varying(128) NOT NULL,
        "State" character varying(2) NOT NULL,
        "ZipCode" character varying(10) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_addresses" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE admin_logs (
        "Id" uuid NOT NULL,
        "Timestamp" timestamp with time zone NOT NULL,
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE app_settings (
        "Id" uuid NOT NULL,
        "Key" character varying(128) NOT NULL,
        "EncryptedValue" character varying(4096) NOT NULL,
        "Description" character varying(512),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_app_settings" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE developer_logs (
        "Id" uuid NOT NULL,
        "Timestamp" timestamp with time zone NOT NULL,
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
        CONSTRAINT "PK_developer_logs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE email_logs (
        "Id" uuid NOT NULL,
        "Timestamp" timestamp with time zone NOT NULL,
        "Recipient" character varying(256) NOT NULL,
        "Subject" character varying(512) NOT NULL,
        "Body" text NOT NULL,
        "Status" character varying(20),
        CONSTRAINT "PK_email_logs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE event_templates (
        "Id" uuid NOT NULL,
        "Name" character varying(128) NOT NULL,
        "Description" character varying(512),
        "Category" character varying(20),
        "LayoutMode" character varying(20),
        "DefaultMaxCapacity" integer,
        "DefaultPlatformFeePercent" integer,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_event_templates" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE magic_link_tokens (
        "Id" uuid NOT NULL,
        "TokenHash" character varying(128) NOT NULL,
        "Email" character varying(256) NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "IsUsed" boolean NOT NULL,
        "UsedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_magic_link_tokens" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE pricing_rule_templates (
        "Id" uuid NOT NULL,
        "Name" character varying(128) NOT NULL,
        "Type" character varying(20) NOT NULL,
        "DefaultPriceCents" integer NOT NULL,
        "DefaultFeePercent" integer,
        "DefaultFeeFlatCents" integer,
        "Description" character varying(512),
        "IsActive" boolean NOT NULL,
        "SortOrder" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_pricing_rule_templates" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE system_logs (
        "Id" uuid NOT NULL,
        "Timestamp" timestamp with time zone NOT NULL,
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
        CONSTRAINT "PK_system_logs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE ticket_type_templates (
        "Id" uuid NOT NULL,
        "Name" character varying(128) NOT NULL,
        "Description" character varying(512),
        "DefaultPriceCents" integer NOT NULL,
        "DefaultPlatformFeeCents" integer NOT NULL,
        "IsActive" boolean NOT NULL,
        "SortOrder" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ticket_type_templates" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE users (
        "Id" uuid NOT NULL,
        "Email" character varying(256) NOT NULL,
        "EmailHash" character varying(128) NOT NULL,
        "FirstName" character varying(128) NOT NULL,
        "LastName" character varying(128) NOT NULL,
        "Role" character varying(20) NOT NULL,
        "IsActive" boolean NOT NULL,
        "LastLoginAt" timestamp with time zone,
        "AddressId" uuid,
        "Phone" text,
        "OptInLocationEmail" boolean NOT NULL,
        "HasCompletedOnboarding" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_users" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_users_addresses_AddressId" FOREIGN KEY ("AddressId") REFERENCES addresses ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE venues (
        "Id" uuid NOT NULL,
        "Name" character varying(256) NOT NULL,
        "Description" character varying(4096),
        "ImagePath" character varying(512),
        "Phone" character varying(20),
        "Email" character varying(256),
        "Website" character varying(512),
        "IsActive" boolean NOT NULL,
        "AddressId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_venues" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_venues_addresses_AddressId" FOREIGN KEY ("AddressId") REFERENCES addresses ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE table_types (
        "Id" uuid NOT NULL,
        "Name" character varying(128) NOT NULL,
        "DefaultCapacity" integer NOT NULL,
        "DefaultShape" character varying(20) NOT NULL,
        "DefaultColor" character varying(20),
        "DefaultPriceCents" integer NOT NULL,
        "PlatformFeeCents" integer NOT NULL,
        "IsActive" boolean NOT NULL,
        "VenueId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_table_types" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_table_types_venues_VenueId" FOREIGN KEY ("VenueId") REFERENCES venues ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE venue_layouts (
        "Id" uuid NOT NULL,
        "Name" character varying(128) NOT NULL,
        "LayoutMode" character varying(20) NOT NULL,
        "EditorMode" character varying(20),
        "GridRows" integer,
        "GridCols" integer,
        "IsDefault" boolean NOT NULL,
        "IsActive" boolean NOT NULL,
        "VenueId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_venue_layouts" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_venue_layouts_venues_VenueId" FOREIGN KEY ("VenueId") REFERENCES venues ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE events (
        "Id" uuid NOT NULL,
        "Title" character varying(256) NOT NULL,
        "Slug" character varying(300) NOT NULL,
        "Description" character varying(8192),
        "Status" character varying(20) NOT NULL,
        "Category" character varying(20),
        "StartDate" timestamp with time zone NOT NULL,
        "EndDate" timestamp with time zone NOT NULL,
        "ImagePath" character varying(512),
        "IsFeatured" boolean NOT NULL,
        "LayoutMode" character varying(20),
        "MaxCapacity" integer,
        "PlatformFeePercent" integer,
        "PublishedAt" timestamp with time zone,
        "ScheduledPublishAt" timestamp with time zone,
        "EditorMode" character varying(20),
        "GridRows" integer,
        "GridCols" integer,
        "SearchVector" tsvector GENERATED ALWAYS AS (to_tsvector('english', "Title" || ' ' || coalesce("Description", ''))) STORED,
        "VenueId" uuid NOT NULL,
        "OrganizerId" uuid NOT NULL,
        "EventTemplateId" uuid,
        "VenueLayoutId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_events" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_events_event_templates_EventTemplateId" FOREIGN KEY ("EventTemplateId") REFERENCES event_templates ("Id"),
        CONSTRAINT "FK_events_users_OrganizerId" FOREIGN KEY ("OrganizerId") REFERENCES users ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_events_venue_layouts_VenueLayoutId" FOREIGN KEY ("VenueLayoutId") REFERENCES venue_layouts ("Id"),
        CONSTRAINT "FK_events_venues_VenueId" FOREIGN KEY ("VenueId") REFERENCES venues ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE venue_layout_tables (
        "Id" uuid NOT NULL,
        "Label" character varying(20) NOT NULL,
        "Section" character varying(64),
        "GridRow" integer,
        "GridCol" integer,
        "SortOrder" integer NOT NULL,
        "PriceType" character varying(20) NOT NULL,
        "PriceCents" integer NOT NULL,
        "IsActive" boolean NOT NULL,
        "VenueLayoutId" uuid NOT NULL,
        "TableTypeId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_venue_layout_tables" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_venue_layout_tables_table_types_TableTypeId" FOREIGN KEY ("TableTypeId") REFERENCES table_types ("Id"),
        CONSTRAINT "FK_venue_layout_tables_venue_layouts_VenueLayoutId" FOREIGN KEY ("VenueLayoutId") REFERENCES venue_layouts ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE bookings (
        "Id" uuid NOT NULL,
        "BookingNumber" character varying(20) NOT NULL,
        "Status" character varying(20) NOT NULL,
        "UserId" uuid NOT NULL,
        "EventId" uuid NOT NULL,
        "SubtotalCents" integer NOT NULL,
        "FeeCents" integer NOT NULL,
        "TotalCents" integer NOT NULL,
        "QrToken" character varying(128),
        "Notes" character varying(1024),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_bookings" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_bookings_events_EventId" FOREIGN KEY ("EventId") REFERENCES events ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_bookings_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE pricing_rules (
        "Id" uuid NOT NULL,
        "EventId" uuid NOT NULL,
        "TableTypeId" uuid,
        "Name" character varying(128),
        "Type" character varying(20),
        "PriceCents" integer,
        "ValidFrom" timestamp with time zone,
        "ValidUntil" timestamp with time zone,
        "MaxCount" integer,
        "UsedCount" integer NOT NULL,
        "IsActive" boolean NOT NULL,
        "SortOrder" integer NOT NULL,
        "FeePercent" integer,
        "FeeFlatCents" integer,
        "Description" character varying(512),
        "TemplateId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_pricing_rules" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_pricing_rules_events_EventId" FOREIGN KEY ("EventId") REFERENCES events ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_pricing_rules_pricing_rule_templates_TemplateId" FOREIGN KEY ("TemplateId") REFERENCES pricing_rule_templates ("Id"),
        CONSTRAINT "FK_pricing_rules_table_types_TableTypeId" FOREIGN KEY ("TableTypeId") REFERENCES table_types ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE ticket_types (
        "Id" uuid NOT NULL,
        "Name" character varying(128),
        "Description" character varying(512),
        "PriceCents" integer,
        "QuantityTotal" integer NOT NULL,
        "QuantitySold" integer NOT NULL,
        "SortOrder" integer NOT NULL,
        "PlatformFeeCents" integer,
        "EventId" uuid NOT NULL,
        "TemplateId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ticket_types" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ticket_types_events_EventId" FOREIGN KEY ("EventId") REFERENCES events ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_ticket_types_ticket_type_templates_TemplateId" FOREIGN KEY ("TemplateId") REFERENCES ticket_type_templates ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE tables (
        "Id" uuid NOT NULL,
        "Label" character varying(20) NOT NULL,
        "Capacity" integer,
        "Shape" character varying(20),
        "Color" character varying(20),
        "Section" character varying(64),
        "PriceType" character varying(20) NOT NULL,
        "PriceCents" integer NOT NULL,
        "PriceOverrideCents" integer,
        "IsActive" boolean NOT NULL,
        "GridRow" integer,
        "GridCol" integer,
        "SortOrder" integer NOT NULL,
        "TableTypeId" uuid,
        "EventId" uuid,
        "VenueId" uuid NOT NULL,
        "VenueLayoutTableId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_tables" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_tables_events_EventId" FOREIGN KEY ("EventId") REFERENCES events ("Id"),
        CONSTRAINT "FK_tables_table_types_TableTypeId" FOREIGN KEY ("TableTypeId") REFERENCES table_types ("Id"),
        CONSTRAINT "FK_tables_venue_layout_tables_VenueLayoutTableId" FOREIGN KEY ("VenueLayoutTableId") REFERENCES venue_layout_tables ("Id"),
        CONSTRAINT "FK_tables_venues_VenueId" FOREIGN KEY ("VenueId") REFERENCES venues ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE payments (
        "Id" uuid NOT NULL,
        "BookingId" uuid NOT NULL,
        "PaymentIntentId" character varying(128) NOT NULL,
        "Status" character varying(30) NOT NULL,
        "AmountCents" integer NOT NULL,
        "Currency" character varying(3) NOT NULL,
        "RefundId" character varying(128),
        "PaidAt" timestamp with time zone,
        "RefundedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_payments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_payments_bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES bookings ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE seats (
        "Id" uuid NOT NULL,
        "Label" character varying(20) NOT NULL,
        "SeatNumber" integer NOT NULL,
        "TableId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_seats" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_seats_tables_TableId" FOREIGN KEY ("TableId") REFERENCES tables ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE booking_items (
        "Id" uuid NOT NULL,
        "BookingId" uuid NOT NULL,
        "TicketTypeId" uuid NOT NULL,
        "SeatId" uuid,
        "PriceCents" integer NOT NULL,
        "QrToken" character varying(128),
        "GuestName" character varying(256),
        "GuestEmail" character varying(256),
        "InvitationToken" character varying(128),
        "InvitationSent" boolean NOT NULL,
        "IsCheckedIn" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_booking_items" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_booking_items_bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES bookings ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_booking_items_seats_SeatId" FOREIGN KEY ("SeatId") REFERENCES seats ("Id"),
        CONSTRAINT "FK_booking_items_ticket_types_TicketTypeId" FOREIGN KEY ("TicketTypeId") REFERENCES ticket_types ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE TABLE seat_holds (
        "Id" uuid NOT NULL,
        "SeatId" uuid NOT NULL,
        "EventId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "TicketTypeId" uuid NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_seat_holds" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_seat_holds_events_EventId" FOREIGN KEY ("EventId") REFERENCES events ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_seat_holds_seats_SeatId" FOREIGN KEY ("SeatId") REFERENCES seats ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_seat_holds_ticket_types_TicketTypeId" FOREIGN KEY ("TicketTypeId") REFERENCES ticket_types ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_seat_holds_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_admin_logs_Action" ON admin_logs ("Action");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_admin_logs_Timestamp" ON admin_logs ("Timestamp");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_app_settings_Key" ON app_settings ("Key");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_booking_items_BookingId" ON booking_items ("BookingId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_booking_items_InvitationToken" ON booking_items ("InvitationToken") WHERE "InvitationToken" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_booking_items_QrToken" ON booking_items ("QrToken") WHERE "QrToken" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_booking_items_SeatId" ON booking_items ("SeatId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_booking_items_TicketTypeId" ON booking_items ("TicketTypeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_bookings_BookingNumber" ON bookings ("BookingNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_bookings_EventId" ON bookings ("EventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_bookings_QrToken" ON bookings ("QrToken") WHERE "QrToken" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_bookings_Status" ON bookings ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_bookings_UserId" ON bookings ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_developer_logs_Severity" ON developer_logs ("Severity");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_developer_logs_Timestamp" ON developer_logs ("Timestamp");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_email_logs_Timestamp" ON email_logs ("Timestamp");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_events_Category" ON events ("Category");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_events_EventTemplateId" ON events ("EventTemplateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_events_OrganizerId" ON events ("OrganizerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_events_SearchVector" ON events USING GIN ("SearchVector");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_events_Slug" ON events ("Slug");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_events_StartDate" ON events ("StartDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_events_Status" ON events ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_events_VenueId" ON events ("VenueId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_events_VenueLayoutId" ON events ("VenueLayoutId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_magic_link_tokens_Email" ON magic_link_tokens ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_magic_link_tokens_ExpiresAt" ON magic_link_tokens ("ExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_magic_link_tokens_TokenHash" ON magic_link_tokens ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_payments_BookingId" ON payments ("BookingId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_payments_PaymentIntentId" ON payments ("PaymentIntentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_pricing_rules_EventId" ON pricing_rules ("EventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_pricing_rules_TableTypeId" ON pricing_rules ("TableTypeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_pricing_rules_TemplateId" ON pricing_rules ("TemplateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_seat_holds_EventId" ON seat_holds ("EventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_seat_holds_ExpiresAt" ON seat_holds ("ExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_seat_holds_SeatId_EventId_IsActive" ON seat_holds ("SeatId", "EventId", "IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_seat_holds_TicketTypeId" ON seat_holds ("TicketTypeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_seat_holds_UserId" ON seat_holds ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_seats_TableId" ON seats ("TableId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_system_logs_Category" ON system_logs ("Category");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_system_logs_Timestamp" ON system_logs ("Timestamp");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_table_types_VenueId" ON table_types ("VenueId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_tables_EventId" ON tables ("EventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_tables_TableTypeId" ON tables ("TableTypeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_tables_VenueId" ON tables ("VenueId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_tables_VenueLayoutTableId" ON tables ("VenueLayoutTableId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_ticket_types_EventId" ON ticket_types ("EventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_ticket_types_TemplateId" ON ticket_types ("TemplateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_users_AddressId" ON users ("AddressId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_users_Email" ON users ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_users_EmailHash" ON users ("EmailHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_venue_layout_tables_TableTypeId" ON venue_layout_tables ("TableTypeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_venue_layout_tables_VenueLayoutId" ON venue_layout_tables ("VenueLayoutId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_venue_layouts_VenueId" ON venue_layouts ("VenueId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_venues_AddressId" ON venues ("AddressId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    CREATE INDEX "IX_venues_Name" ON venues ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN

    CREATE VIEW v_events AS
    SELECT e."Id", e."Title", e."Slug", e."Description", e."Status",
      COALESCE(e."Category", et."Category") AS "Category",
      e."StartDate", e."EndDate", e."ImagePath", e."IsFeatured",
      COALESCE(e."LayoutMode", vl."LayoutMode", 'None') AS "LayoutMode",
      COALESCE(e."EditorMode", vl."EditorMode") AS "EditorMode",
      COALESCE(e."GridRows", vl."GridRows") AS "GridRows",
      COALESCE(e."GridCols", vl."GridCols") AS "GridCols",
      COALESCE(e."MaxCapacity", et."DefaultMaxCapacity") AS "MaxCapacity",
      COALESCE(e."PlatformFeePercent", et."DefaultPlatformFeePercent") AS "PlatformFeePercent",
      e."PublishedAt", e."ScheduledPublishAt",
      e."VenueId", e."OrganizerId", e."SearchVector", e."CreatedAt", e."UpdatedAt",
      v."Name" AS "VenueName",
      a."Line1" AS "VenueAddress",
      a."City" AS "VenueCity",
      a."State" AS "VenueState",
      a."ZipCode" AS "VenueZipCode"
    FROM events e
    JOIN venues v ON e."VenueId" = v."Id"
    LEFT JOIN addresses a ON v."AddressId" = a."Id"
    LEFT JOIN venue_layouts vl ON e."VenueLayoutId" = vl."Id"
    LEFT JOIN event_templates et ON e."EventTemplateId" = et."Id";

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN

    CREATE VIEW v_ticket_types AS
    SELECT tt."Id", tt."EventId",
      COALESCE(tt."Name", tpl."Name") AS "Name",
      COALESCE(tt."Description", tpl."Description") AS "Description",
      COALESCE(tt."PriceCents", tpl."DefaultPriceCents", 0) AS "PriceCents",
      COALESCE(tt."PlatformFeeCents", tpl."DefaultPlatformFeeCents", 0) AS "PlatformFeeCents",
      tt."QuantityTotal", tt."QuantitySold", tt."SortOrder",
      tt."TemplateId", tt."CreatedAt", tt."UpdatedAt"
    FROM ticket_types tt
    LEFT JOIN ticket_type_templates tpl ON tt."TemplateId" = tpl."Id";

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN

    CREATE VIEW v_tables AS
    SELECT
        t."Id", t."EventId", t."VenueId", t."TableTypeId",
        t."Label",
        COALESCE(t."Capacity", ttype."DefaultCapacity", 0) AS "Capacity",
        COALESCE(t."Shape", ttype."DefaultShape", 'Round') AS "Shape",
        COALESCE(t."Color", ttype."DefaultColor") AS "Color",
        t."Section", t."PriceType",
        COALESCE(t."PriceOverrideCents", t."PriceCents", ttype."DefaultPriceCents", 0) AS "EffectivePriceCents",
        COALESCE(ttype."PlatformFeeCents", 0) AS "PlatformFeeCents",
        t."IsActive",
        t."GridRow", t."GridCol", t."SortOrder",
        t."CreatedAt", t."UpdatedAt"
    FROM tables t
    LEFT JOIN table_types ttype ON t."TableTypeId" = ttype."Id";

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN

    CREATE VIEW v_pricing_rules AS
    SELECT pr."Id", pr."EventId", pr."TableTypeId",
      COALESCE(pr."Name", prt."Name") AS "Name",
      COALESCE(pr."Type", prt."Type") AS "Type",
      COALESCE(pr."PriceCents", prt."DefaultPriceCents", 0) AS "PriceCents",
      pr."ValidFrom", pr."ValidUntil", pr."MaxCount", pr."UsedCount", pr."IsActive", pr."SortOrder",
      COALESCE(pr."FeePercent", prt."DefaultFeePercent") AS "FeePercent",
      COALESCE(pr."FeeFlatCents", prt."DefaultFeeFlatCents") AS "FeeFlatCents",
      COALESCE(pr."Description", prt."Description") AS "Description",
      pr."TemplateId", pr."CreatedAt", pr."UpdatedAt"
    FROM pricing_rules pr
    LEFT JOIN pricing_rule_templates prt ON pr."TemplateId" = prt."Id";

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN

    CREATE VIEW v_event_summary AS
    SELECT e."Id", e."Title", e."Slug", e."Status", e."Category",
      e."StartDate", e."EndDate", e."ImagePath", e."IsFeatured",
      v."Name" AS "VenueName",
      a."City" AS "VenueCity",
      CONCAT(u."FirstName", ' ', u."LastName") AS "OrganizerName",
      COUNT(DISTINCT tt."Id") AS "TicketTypeCount",
      COALESCE(SUM(tt."QuantityTotal"), 0) AS "TotalCapacity",
      COALESCE(SUM(tt."QuantitySold"), 0) AS "TotalSold"
    FROM events e
    JOIN venues v ON e."VenueId" = v."Id"
    LEFT JOIN addresses a ON v."AddressId" = a."Id"
    JOIN users u ON e."OrganizerId" = u."Id"
    LEFT JOIN ticket_types tt ON tt."EventId" = e."Id"
    GROUP BY e."Id", e."Title", e."Slug", e."Status", e."Category",
      e."StartDate", e."EndDate", e."ImagePath", e."IsFeatured",
      v."Name", a."City", u."FirstName", u."LastName";

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260329223803_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260329223803_InitialCreate', '10.0.5');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    DROP VIEW IF EXISTS v_event_summary;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    DROP VIEW IF EXISTS v_pricing_rules;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    DROP VIEW IF EXISTS v_tables;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    DROP VIEW IF EXISTS v_ticket_types;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    DROP VIEW IF EXISTS v_events;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE booking_items DROP CONSTRAINT "FK_booking_items_seats_SeatId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE booking_items DROP CONSTRAINT "FK_booking_items_ticket_types_TicketTypeId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE bookings DROP CONSTRAINT "FK_bookings_events_EventId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE bookings DROP CONSTRAINT "FK_bookings_users_UserId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE events DROP CONSTRAINT "FK_events_event_templates_EventTemplateId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE events DROP CONSTRAINT "FK_events_users_OrganizerId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE events DROP CONSTRAINT "FK_events_venue_layouts_VenueLayoutId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE events DROP CONSTRAINT "FK_events_venues_VenueId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE payments DROP CONSTRAINT "FK_payments_bookings_BookingId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE pricing_rules DROP CONSTRAINT "FK_pricing_rules_pricing_rule_templates_TemplateId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE pricing_rules DROP CONSTRAINT "FK_pricing_rules_table_types_TableTypeId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE seat_holds DROP CONSTRAINT "FK_seat_holds_ticket_types_TicketTypeId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE seat_holds DROP CONSTRAINT "FK_seat_holds_users_UserId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE table_types DROP CONSTRAINT "FK_table_types_venues_VenueId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE tables DROP CONSTRAINT "FK_tables_events_EventId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE tables DROP CONSTRAINT "FK_tables_table_types_TableTypeId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE tables DROP CONSTRAINT "FK_tables_venue_layout_tables_VenueLayoutTableId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE tables DROP CONSTRAINT "FK_tables_venues_VenueId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE ticket_types DROP CONSTRAINT "FK_ticket_types_ticket_type_templates_TemplateId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE users DROP CONSTRAINT "FK_users_addresses_AddressId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venue_layout_tables DROP CONSTRAINT "FK_venue_layout_tables_table_types_TableTypeId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venue_layouts DROP CONSTRAINT "FK_venue_layouts_venues_VenueId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venues DROP CONSTRAINT "FK_venues_addresses_AddressId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    DROP INDEX "IX_venue_layouts_VenueId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    DROP INDEX "IX_venue_layout_tables_VenueLayoutId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    DROP INDEX "IX_seats_TableId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    DROP INDEX "IX_seat_holds_SeatId_EventId_IsActive";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    DROP INDEX "IX_bookings_EventId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venues ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venues ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venues ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venue_layouts ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venue_layouts ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venue_layouts ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venue_layout_tables ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venue_layout_tables ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venue_layout_tables ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE users ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE users ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE users ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE ticket_types ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE ticket_types ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE ticket_types ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE ticket_type_templates ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE ticket_type_templates ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE ticket_type_templates ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE tables ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE tables ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE tables ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE table_types ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE table_types ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE table_types ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE system_logs ALTER COLUMN "Timestamp" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE system_logs ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE seats ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE seats ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE seats ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE seat_holds ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE seat_holds ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE seat_holds ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE pricing_rules ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE pricing_rules ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE pricing_rules ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE pricing_rule_templates ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE pricing_rule_templates ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE pricing_rule_templates ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE payments ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE payments ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE payments ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE magic_link_tokens ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE magic_link_tokens ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE magic_link_tokens ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE events ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE events ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE events ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE event_templates ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE event_templates ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE event_templates ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE email_logs ALTER COLUMN "Timestamp" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE email_logs ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE developer_logs ALTER COLUMN "Timestamp" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE developer_logs ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE bookings ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE bookings ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE bookings ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE booking_items ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE booking_items ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE booking_items ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE app_settings ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE app_settings ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE app_settings ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE admin_logs ALTER COLUMN "Timestamp" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE admin_logs ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE addresses ALTER COLUMN "UpdatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE addresses ALTER COLUMN "CreatedAt" SET DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE addresses ALTER COLUMN "Id" SET DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    CREATE UNIQUE INDEX "IX_venue_layouts_VenueId_Name" ON venue_layouts ("VenueId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venue_layouts ADD CONSTRAINT "CK_venue_layouts_EditorMode" CHECK ("EditorMode" IS NULL OR "EditorMode" IN ('Grid'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venue_layouts ADD CONSTRAINT "CK_venue_layouts_GridDimensions" CHECK (("GridRows" IS NULL OR "GridRows" > 0) AND ("GridCols" IS NULL OR "GridCols" > 0));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venue_layouts ADD CONSTRAINT "CK_venue_layouts_LayoutMode" CHECK ("LayoutMode" IN ('None','Grid','CapacityOnly'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    CREATE UNIQUE INDEX "IX_venue_layout_tables_VenueLayoutId_Label" ON venue_layout_tables ("VenueLayoutId", "Label");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venue_layout_tables ADD CONSTRAINT "CK_venue_layout_tables_PriceCents" CHECK ("PriceCents" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venue_layout_tables ADD CONSTRAINT "CK_venue_layout_tables_PriceType" CHECK ("PriceType" IN ('PerTable','PerSeat'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE users ADD CONSTRAINT "CK_users_Role" CHECK ("Role" IN ('User','Staff','Admin','Developer'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE ticket_types ADD CONSTRAINT "CK_ticket_types_PriceCents" CHECK ("PriceCents" IS NULL OR "PriceCents" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE ticket_types ADD CONSTRAINT "CK_ticket_types_QuantitySold" CHECK ("QuantitySold" >= 0 AND "QuantitySold" <= "QuantityTotal");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE ticket_types ADD CONSTRAINT "CK_ticket_types_QuantityTotal" CHECK ("QuantityTotal" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE ticket_type_templates ADD CONSTRAINT "CK_ticket_type_templates_DefaultPlatformFeeCents" CHECK ("DefaultPlatformFeeCents" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE ticket_type_templates ADD CONSTRAINT "CK_ticket_type_templates_DefaultPriceCents" CHECK ("DefaultPriceCents" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    CREATE UNIQUE INDEX "IX_tables_EventId_Label" ON tables ("EventId", "Label") WHERE "EventId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE tables ADD CONSTRAINT "CK_tables_Capacity" CHECK ("Capacity" IS NULL OR "Capacity" > 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE tables ADD CONSTRAINT "CK_tables_PriceCents" CHECK ("PriceCents" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE tables ADD CONSTRAINT "CK_tables_PriceOverrideCents" CHECK ("PriceOverrideCents" IS NULL OR "PriceOverrideCents" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE tables ADD CONSTRAINT "CK_tables_PriceType" CHECK ("PriceType" IN ('PerTable','PerSeat'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE tables ADD CONSTRAINT "CK_tables_Shape" CHECK ("Shape" IS NULL OR "Shape" IN ('Round','Rectangle','Square','Cocktail'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE table_types ADD CONSTRAINT "CK_table_types_DefaultCapacity" CHECK ("DefaultCapacity" > 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE table_types ADD CONSTRAINT "CK_table_types_DefaultPriceCents" CHECK ("DefaultPriceCents" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE table_types ADD CONSTRAINT "CK_table_types_DefaultShape" CHECK ("DefaultShape" IN ('Round','Rectangle','Square','Cocktail'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE table_types ADD CONSTRAINT "CK_table_types_PlatformFeeCents" CHECK ("PlatformFeeCents" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE system_logs ADD CONSTRAINT "CK_system_logs_Category" CHECK ("Category" IN ('EntityChange','BackgroundWorker','Cache','MockService','Migration'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE system_logs ADD CONSTRAINT "CK_system_logs_DurationMs" CHECK ("DurationMs" IS NULL OR "DurationMs" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    CREATE UNIQUE INDEX "IX_seats_TableId_SeatNumber" ON seats ("TableId", "SeatNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE seats ADD CONSTRAINT "CK_seats_SeatNumber" CHECK ("SeatNumber" > 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    CREATE INDEX "IX_seat_holds_IsActive_ExpiresAt" ON seat_holds ("IsActive", "ExpiresAt") WHERE "IsActive" = true;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    CREATE UNIQUE INDEX "IX_seat_holds_SeatId_EventId" ON seat_holds ("SeatId", "EventId") WHERE "IsActive" = true;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE pricing_rules ADD CONSTRAINT "CK_pricing_rules_DateRange" CHECK ("ValidFrom" IS NULL OR "ValidUntil" IS NULL OR "ValidUntil" > "ValidFrom");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE pricing_rules ADD CONSTRAINT "CK_pricing_rules_FeeFlatCents" CHECK ("FeeFlatCents" IS NULL OR "FeeFlatCents" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE pricing_rules ADD CONSTRAINT "CK_pricing_rules_FeePercent" CHECK ("FeePercent" IS NULL OR ("FeePercent" >= 0 AND "FeePercent" <= 100));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE pricing_rules ADD CONSTRAINT "CK_pricing_rules_MaxCount" CHECK ("MaxCount" IS NULL OR "MaxCount" > 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE pricing_rules ADD CONSTRAINT "CK_pricing_rules_PriceCents" CHECK ("PriceCents" IS NULL OR "PriceCents" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE pricing_rules ADD CONSTRAINT "CK_pricing_rules_Type" CHECK ("Type" IS NULL OR "Type" IN ('Standard','EarlyBird','FirstN'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE pricing_rules ADD CONSTRAINT "CK_pricing_rules_UsedCount" CHECK ("UsedCount" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE pricing_rule_templates ADD CONSTRAINT "CK_pricing_rule_templates_DefaultPriceCents" CHECK ("DefaultPriceCents" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE pricing_rule_templates ADD CONSTRAINT "CK_pricing_rule_templates_Type" CHECK ("Type" IN ('Standard','EarlyBird','FirstN'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    CREATE INDEX "IX_payments_Status_PaidAt" ON payments ("Status", "PaidAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE payments ADD CONSTRAINT "CK_payments_AmountCents" CHECK ("AmountCents" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE payments ADD CONSTRAINT "CK_payments_Currency" CHECK ("Currency" IN ('usd'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE payments ADD CONSTRAINT "CK_payments_Status" CHECK ("Status" IN ('RequiresConfirmation','Succeeded','Failed','Refunded'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    CREATE INDEX "IX_events_Status_StartDate" ON events ("Status", "StartDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE events ADD CONSTRAINT "CK_events_Category" CHECK ("Category" IS NULL OR "Category" IN ('Music','Business','Social','Dining','Tech','Arts','Family','Sports'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE events ADD CONSTRAINT "CK_events_DateRange" CHECK ("EndDate" > "StartDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE events ADD CONSTRAINT "CK_events_EditorMode" CHECK ("EditorMode" IS NULL OR "EditorMode" IN ('Grid'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE events ADD CONSTRAINT "CK_events_GridDimensions" CHECK (("GridRows" IS NULL OR "GridRows" > 0) AND ("GridCols" IS NULL OR "GridCols" > 0));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE events ADD CONSTRAINT "CK_events_LayoutMode" CHECK ("LayoutMode" IS NULL OR "LayoutMode" IN ('None','Grid','CapacityOnly'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE events ADD CONSTRAINT "CK_events_MaxCapacity" CHECK ("MaxCapacity" IS NULL OR "MaxCapacity" > 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE events ADD CONSTRAINT "CK_events_PlatformFeePercent" CHECK ("PlatformFeePercent" IS NULL OR ("PlatformFeePercent" >= 0 AND "PlatformFeePercent" <= 100));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE events ADD CONSTRAINT "CK_events_Status" CHECK ("Status" IN ('Draft','Published','Completed','Cancelled'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE event_templates ADD CONSTRAINT "CK_event_templates_Category" CHECK ("Category" IS NULL OR "Category" IN ('Music','Business','Social','Dining','Tech','Arts','Family','Sports'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE event_templates ADD CONSTRAINT "CK_event_templates_DefaultMaxCapacity" CHECK ("DefaultMaxCapacity" IS NULL OR "DefaultMaxCapacity" > 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE event_templates ADD CONSTRAINT "CK_event_templates_DefaultPlatformFeePercent" CHECK ("DefaultPlatformFeePercent" IS NULL OR ("DefaultPlatformFeePercent" >= 0 AND "DefaultPlatformFeePercent" <= 100));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE event_templates ADD CONSTRAINT "CK_event_templates_LayoutMode" CHECK ("LayoutMode" IS NULL OR "LayoutMode" IN ('None','Grid','CapacityOnly'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE developer_logs ADD CONSTRAINT "CK_developer_logs_Severity" CHECK ("Severity" IN ('Warning','Error','Critical'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    CREATE INDEX "IX_bookings_EventId_Status" ON bookings ("EventId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    CREATE INDEX "IX_bookings_UserId_CreatedAt" ON bookings ("UserId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE bookings ADD CONSTRAINT "CK_bookings_FeeCents" CHECK ("FeeCents" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE bookings ADD CONSTRAINT "CK_bookings_Status" CHECK ("Status" IN ('Pending','Paid','CheckedIn','Cancelled','Refunded'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE bookings ADD CONSTRAINT "CK_bookings_SubtotalCents" CHECK ("SubtotalCents" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE bookings ADD CONSTRAINT "CK_bookings_TotalCents" CHECK ("TotalCents" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE bookings ADD CONSTRAINT "CK_bookings_TotalFormula" CHECK ("TotalCents" = "SubtotalCents" + "FeeCents");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE booking_items ADD CONSTRAINT "CK_booking_items_PriceCents" CHECK ("PriceCents" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE booking_items ADD CONSTRAINT "FK_booking_items_seats_SeatId" FOREIGN KEY ("SeatId") REFERENCES seats ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE booking_items ADD CONSTRAINT "FK_booking_items_ticket_types_TicketTypeId" FOREIGN KEY ("TicketTypeId") REFERENCES ticket_types ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE bookings ADD CONSTRAINT "FK_bookings_events_EventId" FOREIGN KEY ("EventId") REFERENCES events ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE bookings ADD CONSTRAINT "FK_bookings_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE events ADD CONSTRAINT "FK_events_event_templates_EventTemplateId" FOREIGN KEY ("EventTemplateId") REFERENCES event_templates ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE events ADD CONSTRAINT "FK_events_users_OrganizerId" FOREIGN KEY ("OrganizerId") REFERENCES users ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE events ADD CONSTRAINT "FK_events_venue_layouts_VenueLayoutId" FOREIGN KEY ("VenueLayoutId") REFERENCES venue_layouts ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE events ADD CONSTRAINT "FK_events_venues_VenueId" FOREIGN KEY ("VenueId") REFERENCES venues ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE payments ADD CONSTRAINT "FK_payments_bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES bookings ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE pricing_rules ADD CONSTRAINT "FK_pricing_rules_pricing_rule_templates_TemplateId" FOREIGN KEY ("TemplateId") REFERENCES pricing_rule_templates ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE pricing_rules ADD CONSTRAINT "FK_pricing_rules_table_types_TableTypeId" FOREIGN KEY ("TableTypeId") REFERENCES table_types ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE seat_holds ADD CONSTRAINT "FK_seat_holds_ticket_types_TicketTypeId" FOREIGN KEY ("TicketTypeId") REFERENCES ticket_types ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE seat_holds ADD CONSTRAINT "FK_seat_holds_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE table_types ADD CONSTRAINT "FK_table_types_venues_VenueId" FOREIGN KEY ("VenueId") REFERENCES venues ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE tables ADD CONSTRAINT "FK_tables_events_EventId" FOREIGN KEY ("EventId") REFERENCES events ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE tables ADD CONSTRAINT "FK_tables_table_types_TableTypeId" FOREIGN KEY ("TableTypeId") REFERENCES table_types ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE tables ADD CONSTRAINT "FK_tables_venue_layout_tables_VenueLayoutTableId" FOREIGN KEY ("VenueLayoutTableId") REFERENCES venue_layout_tables ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE tables ADD CONSTRAINT "FK_tables_venues_VenueId" FOREIGN KEY ("VenueId") REFERENCES venues ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE ticket_types ADD CONSTRAINT "FK_ticket_types_ticket_type_templates_TemplateId" FOREIGN KEY ("TemplateId") REFERENCES ticket_type_templates ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE users ADD CONSTRAINT "FK_users_addresses_AddressId" FOREIGN KEY ("AddressId") REFERENCES addresses ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venue_layout_tables ADD CONSTRAINT "FK_venue_layout_tables_table_types_TableTypeId" FOREIGN KEY ("TableTypeId") REFERENCES table_types ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venue_layouts ADD CONSTRAINT "FK_venue_layouts_venues_VenueId" FOREIGN KEY ("VenueId") REFERENCES venues ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    ALTER TABLE venues ADD CONSTRAINT "FK_venues_addresses_AddressId" FOREIGN KEY ("AddressId") REFERENCES addresses ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE OR REPLACE FUNCTION trigger_set_updated_at()
    RETURNS TRIGGER AS $$
    BEGIN
        NEW."UpdatedAt" = now();
        RETURN NEW;
    END;
    $$ LANGUAGE plpgsql;

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON addresses
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON app_settings
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON magic_link_tokens
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON venues
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON table_types
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON ticket_type_templates
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON venue_layouts
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON venue_layout_tables
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON pricing_rule_templates
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON event_templates
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON events
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON ticket_types
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON tables
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON seats
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON seat_holds
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON bookings
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON booking_items
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON payments
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE TRIGGER set_updated_at BEFORE UPDATE ON pricing_rules
    FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE VIEW v_events AS
    SELECT e."Id", e."Title", e."Slug", e."Description", e."Status",
      COALESCE(e."Category", et."Category") AS "Category",
      e."StartDate", e."EndDate", e."ImagePath", e."IsFeatured",
      COALESCE(e."LayoutMode", vl."LayoutMode", 'None') AS "LayoutMode",
      COALESCE(e."EditorMode", vl."EditorMode") AS "EditorMode",
      COALESCE(e."GridRows", vl."GridRows") AS "GridRows",
      COALESCE(e."GridCols", vl."GridCols") AS "GridCols",
      COALESCE(e."MaxCapacity", et."DefaultMaxCapacity") AS "MaxCapacity",
      COALESCE(e."PlatformFeePercent", et."DefaultPlatformFeePercent") AS "PlatformFeePercent",
      e."PublishedAt", e."ScheduledPublishAt",
      e."VenueId", e."OrganizerId", e."SearchVector", e."CreatedAt", e."UpdatedAt",
      v."Name" AS "VenueName",
      a."Line1" AS "VenueAddress",
      a."City" AS "VenueCity",
      a."State" AS "VenueState",
      a."ZipCode" AS "VenueZipCode"
    FROM events e
    JOIN venues v ON e."VenueId" = v."Id"
    LEFT JOIN addresses a ON v."AddressId" = a."Id"
    LEFT JOIN venue_layouts vl ON e."VenueLayoutId" = vl."Id"
    LEFT JOIN event_templates et ON e."EventTemplateId" = et."Id";

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE VIEW v_ticket_types AS
    SELECT tt."Id", tt."EventId",
      COALESCE(tt."Name", tpl."Name") AS "Name",
      COALESCE(tt."Description", tpl."Description") AS "Description",
      COALESCE(tt."PriceCents", tpl."DefaultPriceCents", 0) AS "PriceCents",
      COALESCE(tt."PlatformFeeCents", tpl."DefaultPlatformFeeCents", 0) AS "PlatformFeeCents",
      tt."QuantityTotal", tt."QuantitySold", tt."SortOrder",
      tt."TemplateId", tt."CreatedAt", tt."UpdatedAt"
    FROM ticket_types tt
    LEFT JOIN ticket_type_templates tpl ON tt."TemplateId" = tpl."Id";

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE VIEW v_tables AS
    SELECT
        t."Id", t."EventId", t."VenueId", t."TableTypeId",
        t."Label",
        COALESCE(t."Capacity", ttype."DefaultCapacity", 0) AS "Capacity",
        COALESCE(t."Shape", ttype."DefaultShape", 'Round') AS "Shape",
        COALESCE(t."Color", ttype."DefaultColor") AS "Color",
        t."Section", t."PriceType",
        COALESCE(t."PriceOverrideCents", t."PriceCents", ttype."DefaultPriceCents", 0) AS "EffectivePriceCents",
        COALESCE(ttype."PlatformFeeCents", 0) AS "PlatformFeeCents",
        t."IsActive",
        t."GridRow", t."GridCol", t."SortOrder",
        t."CreatedAt", t."UpdatedAt"
    FROM tables t
    LEFT JOIN table_types ttype ON t."TableTypeId" = ttype."Id";

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE VIEW v_pricing_rules AS
    SELECT pr."Id", pr."EventId", pr."TableTypeId",
      COALESCE(pr."Name", prt."Name") AS "Name",
      COALESCE(pr."Type", prt."Type") AS "Type",
      COALESCE(pr."PriceCents", prt."DefaultPriceCents", 0) AS "PriceCents",
      pr."ValidFrom", pr."ValidUntil", pr."MaxCount", pr."UsedCount", pr."IsActive", pr."SortOrder",
      COALESCE(pr."FeePercent", prt."DefaultFeePercent") AS "FeePercent",
      COALESCE(pr."FeeFlatCents", prt."DefaultFeeFlatCents") AS "FeeFlatCents",
      COALESCE(pr."Description", prt."Description") AS "Description",
      pr."TemplateId", pr."CreatedAt", pr."UpdatedAt"
    FROM pricing_rules pr
    LEFT JOIN pricing_rule_templates prt ON pr."TemplateId" = prt."Id";

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN

    CREATE VIEW v_event_summary AS
    SELECT e."Id", e."Title", e."Slug", e."Status", e."Category",
      e."StartDate", e."EndDate", e."ImagePath", e."IsFeatured",
      v."Name" AS "VenueName",
      a."City" AS "VenueCity",
      CONCAT(u."FirstName", ' ', u."LastName") AS "OrganizerName",
      COUNT(DISTINCT tt."Id") AS "TicketTypeCount",
      COALESCE(SUM(tt."QuantityTotal"), 0) AS "TotalCapacity",
      COALESCE(SUM(tt."QuantitySold"), 0) AS "TotalSold"
    FROM events e
    JOIN venues v ON e."VenueId" = v."Id"
    LEFT JOIN addresses a ON v."AddressId" = a."Id"
    JOIN users u ON e."OrganizerId" = u."Id"
    LEFT JOIN ticket_types tt ON tt."EventId" = e."Id"
    GROUP BY e."Id", e."Title", e."Slug", e."Status", e."Category",
      e."StartDate", e."EndDate", e."ImagePath", e."IsFeatured",
      v."Name", a."City", u."FirstName", u."LastName";

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    COMMENT ON TABLE users IS 'Platform users with role-based access (User, Staff, Admin, Developer)';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    COMMENT ON TABLE events IS 'Published, draft, or completed events with venue and organizer references';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    COMMENT ON TABLE venues IS 'Physical locations where events are held';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    COMMENT ON TABLE bookings IS 'Customer ticket/table reservations with payment tracking';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    COMMENT ON TABLE payments IS 'Stripe payment records linked 1:1 to bookings';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    COMMENT ON TABLE tables IS 'Placed table instances on an event floor plan';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    COMMENT ON TABLE seats IS 'Individual seats at tables, independently bookable';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    COMMENT ON TABLE seat_holds IS 'Temporary seat reservations during checkout (TTL-based)';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    COMMENT ON TABLE ticket_types IS 'Ticket tiers/price levels for an event';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    COMMENT ON TABLE pricing_rules IS 'Pricing rules (standard, early bird, first-N) per event';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    COMMENT ON TABLE booking_items IS 'Individual line items within a booking (one per ticket/seat)';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    COMMENT ON TABLE developer_logs IS 'Application error and exception tracking';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    COMMENT ON TABLE admin_logs IS 'Admin action audit trail';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    COMMENT ON TABLE system_logs IS 'Entity change audit trail with before/after JSON diffs';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    COMMENT ON TABLE email_logs IS 'Email delivery tracking';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331222836_ProductionConstraints') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260331222836_ProductionConstraints', '10.0.5');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331225240_LifecycleConstraints') THEN
    CREATE UNIQUE INDEX "IX_venue_layouts_OneDefaultPerVenue" ON venue_layouts ("VenueId") WHERE "IsDefault" = true;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331225240_LifecycleConstraints') THEN
    ALTER TABLE seat_holds ADD CONSTRAINT "CK_seat_holds_ExpiresAfterCreate" CHECK ("ExpiresAt" > "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331225240_LifecycleConstraints') THEN
    ALTER TABLE payments ADD CONSTRAINT "CK_payments_PaidLifecycle" CHECK ("Status" NOT IN ('Succeeded','Refunded') OR "PaidAt" IS NOT NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331225240_LifecycleConstraints') THEN
    ALTER TABLE payments ADD CONSTRAINT "CK_payments_RefundLifecycle" CHECK ("Status" <> 'Refunded' OR "RefundedAt" IS NOT NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331225240_LifecycleConstraints') THEN
    ALTER TABLE magic_link_tokens ADD CONSTRAINT "CK_magic_link_tokens_Usage" CHECK (("IsUsed" = false AND "UsedAt" IS NULL) OR ("IsUsed" = true AND "UsedAt" IS NOT NULL));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331225240_LifecycleConstraints') THEN
    ALTER TABLE events ADD CONSTRAINT "CK_events_PublishLifecycle" CHECK ("Status" <> 'Published' OR "PublishedAt" IS NOT NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331225240_LifecycleConstraints') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260331225240_LifecycleConstraints', '10.0.5');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331234248_FinalHardening') THEN
    ALTER TABLE payments ADD CONSTRAINT "CK_payments_NotRefundedNoRefundDate" CHECK ("Status" = 'Refunded' OR "RefundedAt" IS NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331234248_FinalHardening') THEN
    ALTER TABLE payments ADD CONSTRAINT "CK_payments_PendingNoPaidDate" CHECK ("Status" NOT IN ('RequiresConfirmation','Failed') OR "PaidAt" IS NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331234248_FinalHardening') THEN
    ALTER TABLE events ADD CONSTRAINT "CK_events_CompletedRequiresPublish" CHECK ("Status" <> 'Completed' OR "PublishedAt" IS NOT NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331234248_FinalHardening') THEN
    ALTER TABLE events ADD CONSTRAINT "CK_events_DraftNoPublishDate" CHECK ("Status" <> 'Draft' OR "PublishedAt" IS NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331234248_FinalHardening') THEN

    DO $$
    BEGIN
        IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'ep_app') THEN
            EXECUTE 'REVOKE INSERT, UPDATE, DELETE ON v_events, v_ticket_types, v_tables, v_pricing_rules, v_event_summary FROM ep_app';
        END IF;
        IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'ep_readonly') THEN
            EXECUTE 'REVOKE INSERT, UPDATE, DELETE ON v_events, v_ticket_types, v_tables, v_pricing_rules, v_event_summary FROM ep_readonly';
        END IF;
    END
    $$;

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331234248_FinalHardening') THEN
    COMMENT ON TABLE payments IS 'Stripe payment records linked 1:1 to bookings. One payment intent per booking by design — Stripe handles retries internally within the same intent. If multiple payment attempts or partial refunds are needed, redesign as payment-history table.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331234248_FinalHardening') THEN
    COMMENT ON COLUMN bookings."QrToken" IS 'Raw bearer token embedded in QR code images for check-in scanning. Stored unhashed because the token must be displayable to users and scannable by staff. 192-bit random value. Check-in requires staff auth as defense-in-depth.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331234248_FinalHardening') THEN
    COMMENT ON COLUMN booking_items."QrToken" IS 'Per-seat/ticket QR token for individual check-in. Stored unhashed — see bookings.QrToken comment for rationale.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331234248_FinalHardening') THEN
    COMMENT ON COLUMN booking_items."InvitationToken" IS 'Shareable invitation link token for guest access (no login required). Stored unhashed because guests use the raw token in URL paths for anonymous access. 256-bit random value.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260331234248_FinalHardening') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260331234248_FinalHardening', '10.0.5');
    END IF;
END $EF$;
COMMIT;

