START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM qa_expanded_scope."__EFMigrationsHistory" WHERE "MigrationId" = '20261017132203_AddCampaignAudienceAndOrderAmendments') THEN
    CREATE TABLE qa_expanded_scope."CampaignAudiences" (
        "Id" text NOT NULL,
        "WorkspaceId" text NOT NULL,
        "CampaignId" text NOT NULL,
        "MerchantId" text NOT NULL,
        CONSTRAINT "PK_CampaignAudiences" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM qa_expanded_scope."__EFMigrationsHistory" WHERE "MigrationId" = '20261017132203_AddCampaignAudienceAndOrderAmendments') THEN
    CREATE TABLE qa_expanded_scope."OrderAmendments" (
        "Id" text NOT NULL,
        "WorkspaceId" text NOT NULL,
        "OrderId" text NOT NULL,
        "MerchantId" text NOT NULL,
        "Version" integer NOT NULL,
        "BaseOrderVersion" integer NOT NULL,
        "Status" text NOT NULL,
        "Reason" text NOT NULL,
        "ProposedBy" text NOT NULL,
        "ProposerName" text NOT NULL,
        "ProposedAt" timestamp with time zone NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "IdempotencyKey" text NOT NULL,
        "RequestHash" text NOT NULL,
        "LinesJson" text NOT NULL,
        "BeforeJson" text NOT NULL,
        "AfterJson" text NOT NULL,
        "TermsHash" text NOT NULL,
        "DecidedBy" text NOT NULL,
        "DecisionName" text NOT NULL,
        "DecidedAt" timestamp with time zone,
        "DecisionReason" text NOT NULL,
        "DecisionKey" text NOT NULL,
        "DecisionRequestHash" text NOT NULL,
        "AcceptedOrderVersion" integer,
        CONSTRAINT "PK_OrderAmendments" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM qa_expanded_scope."__EFMigrationsHistory" WHERE "MigrationId" = '20261017132203_AddCampaignAudienceAndOrderAmendments') THEN
    CREATE UNIQUE INDEX "IX_CampaignAudiences_WorkspaceId_CampaignId_MerchantId" ON qa_expanded_scope."CampaignAudiences" ("WorkspaceId", "CampaignId", "MerchantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM qa_expanded_scope."__EFMigrationsHistory" WHERE "MigrationId" = '20261017132203_AddCampaignAudienceAndOrderAmendments') THEN
    CREATE INDEX "IX_OrderAmendments_WorkspaceId_MerchantId" ON qa_expanded_scope."OrderAmendments" ("WorkspaceId", "MerchantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM qa_expanded_scope."__EFMigrationsHistory" WHERE "MigrationId" = '20261017132203_AddCampaignAudienceAndOrderAmendments') THEN
    CREATE UNIQUE INDEX "IX_OrderAmendments_WorkspaceId_OrderId" ON qa_expanded_scope."OrderAmendments" ("WorkspaceId", "OrderId") WHERE "Status" = 'PENDING';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM qa_expanded_scope."__EFMigrationsHistory" WHERE "MigrationId" = '20261017132203_AddCampaignAudienceAndOrderAmendments') THEN
    CREATE UNIQUE INDEX "IX_OrderAmendments_WorkspaceId_OrderId_IdempotencyKey" ON qa_expanded_scope."OrderAmendments" ("WorkspaceId", "OrderId", "IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM qa_expanded_scope."__EFMigrationsHistory" WHERE "MigrationId" = '20261017132203_AddCampaignAudienceAndOrderAmendments') THEN
    INSERT INTO qa_expanded_scope."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261017132203_AddCampaignAudienceAndOrderAmendments', '10.0.9');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM qa_expanded_scope."__EFMigrationsHistory" WHERE "MigrationId" = '20261017133002_AddLotRecallContainment') THEN
    CREATE TABLE qa_expanded_scope."RecallCases" (
        "Id" text NOT NULL,
        "WorkspaceId" text NOT NULL,
        "ProductId" text NOT NULL,
        "LotId" text NOT NULL,
        "IdentityKey" text NOT NULL,
        "LotNumber" text NOT NULL,
        "ExpiresAt" timestamp with time zone,
        "Reason" text NOT NULL,
        "SafetyInstruction" text NOT NULL,
        "Status" text NOT NULL,
        "ContainmentActive" boolean NOT NULL,
        "UnknownUntrackedOrderCount" integer NOT NULL,
        "UnknownUntrackedQuantity" integer NOT NULL,
        "Version" integer NOT NULL,
        "CreatedBy" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ClosedBy" text NOT NULL,
        "ClosedAt" timestamp with time zone,
        "ClosureNote" text NOT NULL,
        "UnknownScopeReason" text NOT NULL,
        CONSTRAINT "PK_RecallCases" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM qa_expanded_scope."__EFMigrationsHistory" WHERE "MigrationId" = '20261017133002_AddLotRecallContainment') THEN
    CREATE TABLE qa_expanded_scope."RecallEvents" (
        "Id" text NOT NULL,
        "WorkspaceId" text NOT NULL,
        "RecallId" text NOT NULL,
        "PositionId" text NOT NULL,
        "WarehouseLotId" text NOT NULL,
        "ActorId" text NOT NULL,
        "Kind" text NOT NULL,
        "Quantity" integer NOT NULL,
        "Destination" text NOT NULL,
        "ProofReference" text NOT NULL,
        "Details" text NOT NULL,
        "At" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_RecallEvents" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM qa_expanded_scope."__EFMigrationsHistory" WHERE "MigrationId" = '20261017133002_AddLotRecallContainment') THEN
    CREATE TABLE qa_expanded_scope."RecallNotices" (
        "Id" text NOT NULL,
        "WorkspaceId" text NOT NULL,
        "RecallId" text NOT NULL,
        "RecipientId" text NOT NULL,
        "RecipientRole" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ReceivedBy" text NOT NULL,
        "ReceivedAt" timestamp with time zone,
        "Version" integer NOT NULL,
        CONSTRAINT "PK_RecallNotices" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM qa_expanded_scope."__EFMigrationsHistory" WHERE "MigrationId" = '20261017133002_AddLotRecallContainment') THEN
    CREATE TABLE qa_expanded_scope."RecallOperations" (
        "Id" text NOT NULL,
        "WorkspaceId" text NOT NULL,
        "ActorId" text NOT NULL,
        "IdempotencyKey" text NOT NULL,
        "RequestHash" text NOT NULL,
        "RecallId" text NOT NULL,
        "At" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_RecallOperations" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM qa_expanded_scope."__EFMigrationsHistory" WHERE "MigrationId" = '20261017133002_AddLotRecallContainment') THEN
    CREATE TABLE qa_expanded_scope."RecallPositions" (
        "Id" text NOT NULL,
        "WorkspaceId" text NOT NULL,
        "RecallId" text NOT NULL,
        "PositionKey" text NOT NULL,
        "Kind" text NOT NULL,
        "LotId" text NOT NULL,
        "AllocationId" text NOT NULL,
        "OrderId" text NOT NULL,
        "ShopId" text NOT NULL,
        "DriverId" text NOT NULL,
        "SnapshotOrderStatus" text NOT NULL,
        "Quantity" integer NOT NULL,
        "RecoveredQuantity" integer NOT NULL,
        "ExceptionQuantity" integer NOT NULL,
        "RequiresRecovery" boolean NOT NULL,
        "ExceptionReason" text NOT NULL,
        "ExceptionReference" text NOT NULL,
        "ExceptionBy" text NOT NULL,
        "ExceptionAt" timestamp with time zone,
        "Version" integer NOT NULL,
        CONSTRAINT "PK_RecallPositions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM qa_expanded_scope."__EFMigrationsHistory" WHERE "MigrationId" = '20261017133002_AddLotRecallContainment') THEN
    CREATE UNIQUE INDEX "IX_RecallCases_WorkspaceId_IdentityKey" ON qa_expanded_scope."RecallCases" ("WorkspaceId", "IdentityKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM qa_expanded_scope."__EFMigrationsHistory" WHERE "MigrationId" = '20261017133002_AddLotRecallContainment') THEN
    CREATE INDEX "IX_RecallEvents_WorkspaceId_RecallId" ON qa_expanded_scope."RecallEvents" ("WorkspaceId", "RecallId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM qa_expanded_scope."__EFMigrationsHistory" WHERE "MigrationId" = '20261017133002_AddLotRecallContainment') THEN
    CREATE UNIQUE INDEX "IX_RecallNotices_RecallId_RecipientRole_RecipientId" ON qa_expanded_scope."RecallNotices" ("RecallId", "RecipientRole", "RecipientId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM qa_expanded_scope."__EFMigrationsHistory" WHERE "MigrationId" = '20261017133002_AddLotRecallContainment') THEN
    CREATE UNIQUE INDEX "IX_RecallOperations_WorkspaceId_ActorId_IdempotencyKey" ON qa_expanded_scope."RecallOperations" ("WorkspaceId", "ActorId", "IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM qa_expanded_scope."__EFMigrationsHistory" WHERE "MigrationId" = '20261017133002_AddLotRecallContainment') THEN
    CREATE UNIQUE INDEX "IX_RecallPositions_RecallId_PositionKey" ON qa_expanded_scope."RecallPositions" ("RecallId", "PositionKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM qa_expanded_scope."__EFMigrationsHistory" WHERE "MigrationId" = '20261017133002_AddLotRecallContainment') THEN
    INSERT INTO qa_expanded_scope."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261017133002_AddLotRecallContainment', '10.0.9');
    END IF;
END $EF$;
COMMIT;

