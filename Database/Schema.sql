IF OBJECT_ID(N'dbo.UserAccounts', N'U') IS NULL
BEGIN
	CREATE TABLE dbo.UserAccounts
	(
		Id                   uniqueidentifier  NOT NULL,
		Email                nvarchar(254)     NOT NULL,
		NormalizedEmail      nvarchar(254)     NOT NULL,
		Username             nvarchar(100)     NOT NULL,
		NormalizedUsername   nvarchar(100)     NOT NULL,
		PasswordHash         nvarchar(512)     NOT NULL,
		CreatedAt            datetimeoffset    NOT NULL,

		CONSTRAINT PK_UserAccounts PRIMARY KEY CLUSTERED (Id)
	);
END;
GO

IF OBJECT_ID(N'dbo.Wagers', N'U') IS NULL
BEGIN
	CREATE TABLE dbo.Wagers
	(
		WagerId              uniqueidentifier  NOT NULL,
		Theme                nvarchar(max)     NOT NULL,
		Provider             nvarchar(200)     NOT NULL,
		GameName             nvarchar(200)     NOT NULL,
		TransactionId        uniqueidentifier  NOT NULL,
		BrandId              uniqueidentifier  NOT NULL,
		AccountId            uniqueidentifier  NOT NULL,
		Username             nvarchar(100)     NOT NULL,
		ExternalReferenceId  uniqueidentifier  NOT NULL,
		TransactionTypeId    uniqueidentifier  NOT NULL,
		Amount               decimal(20, 6)    NOT NULL,
		CreatedDateTime      datetimeoffset    NOT NULL,
		NumberOfBets         int               NOT NULL,
		CountryCode          nvarchar(2)       NOT NULL,
		SessionData          nvarchar(max)     NULL,
		Duration             bigint            NOT NULL,

		CONSTRAINT PK_Wagers PRIMARY KEY CLUSTERED (WagerId),
		CONSTRAINT CK_Wagers_Amount_NonNegative CHECK (Amount >= 0),
		CONSTRAINT CK_Wagers_NumberOfBets_Positive CHECK (NumberOfBets >= 1),
		CONSTRAINT CK_Wagers_Duration_NonNegative CHECK (Duration >= 0)
	);
END;
GO

IF OBJECT_ID(N'dbo.Outbox', N'U') IS NULL
BEGIN
	CREATE TABLE dbo.Outbox
	(
		Id          uniqueidentifier  NOT NULL,
		EventType   nvarchar(max)     NOT NULL,
		Payload     nvarchar(max)     NOT NULL,
		CreatedAt   datetimeoffset    NOT NULL,
		PublishedAt datetimeoffset     NULL,
		Attempts    int               NOT NULL,

		CONSTRAINT PK_Outbox PRIMARY KEY CLUSTERED (Id)
	);
END;
GO

IF NOT EXISTS
(
	SELECT 1
	FROM sys.indexes
	WHERE name = N'IX_UserAccounts_NormalizedEmail'
	  AND object_id = OBJECT_ID(N'dbo.UserAccounts')
)
BEGIN
	CREATE UNIQUE INDEX IX_UserAccounts_NormalizedEmail
		ON dbo.UserAccounts (NormalizedEmail);
END;
GO

IF NOT EXISTS
(
	SELECT 1
	FROM sys.indexes
	WHERE name = N'IX_UserAccounts_NormalizedUsername'
	  AND object_id = OBJECT_ID(N'dbo.UserAccounts')
)
BEGIN
	CREATE UNIQUE INDEX IX_UserAccounts_NormalizedUsername
		ON dbo.UserAccounts (NormalizedUsername);
END;
GO

IF NOT EXISTS
(
	SELECT 1
	FROM sys.indexes
	WHERE name = N'IX_Wagers_AccountId_CreatedDateTime'
	  AND object_id = OBJECT_ID(N'dbo.Wagers')
)
BEGIN
	CREATE INDEX IX_Wagers_AccountId_CreatedDateTime
		ON dbo.Wagers (AccountId, CreatedDateTime);
END;
GO

IF NOT EXISTS
(
	SELECT 1
	FROM sys.indexes
	WHERE name = N'IX_Wagers_TransactionId'
	  AND object_id = OBJECT_ID(N'dbo.Wagers')
)
BEGIN
	CREATE UNIQUE INDEX IX_Wagers_TransactionId
		ON dbo.Wagers (TransactionId);
END;
GO

IF NOT EXISTS
(
	SELECT 1
	FROM sys.indexes
	WHERE name = N'IX_Outbox_PublishedAt_CreatedAt'
	  AND object_id = OBJECT_ID(N'dbo.Outbox')
)
BEGIN
	CREATE INDEX IX_Outbox_PublishedAt_CreatedAt
		ON dbo.Outbox (PublishedAt, CreatedAt);
END;
GO
