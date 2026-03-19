using AiEnterprise.Infrastructure.Configuration;
using Dapper;
using Microsoft.Extensions.Logging;

namespace AiEnterprise.Infrastructure.Database;

/// <summary>
/// Initializes the database schema on startup. In production, use a migration tool like Flyway or EF Migrations.
/// </summary>
public class DatabaseInitializer
{
    private readonly DapperContext _context;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(DapperContext context, ILogger<DatabaseInitializer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        using var connection = _context.CreateConnection();
        try
        {
            await connection.ExecuteAsync(CreateTablesScript);
            _logger.LogInformation("Database schema initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database schema.");
            throw;
        }
    }

    private const string CreateTablesScript = """
        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Enterprises' AND xtype='U')
        CREATE TABLE Enterprises (
            Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
            Name NVARCHAR(200) NOT NULL,
            Domain NVARCHAR(200) NOT NULL,
            SubscriptionTier INT NOT NULL DEFAULT 1,
            ActiveFrameworks NVARCHAR(MAX) NOT NULL DEFAULT '[]',
            ContactEmail NVARCHAR(200) NOT NULL DEFAULT '',
            Industry NVARCHAR(100) NOT NULL DEFAULT '',
            EmployeeCount INT NOT NULL DEFAULT 0,
            Country NVARCHAR(100) NOT NULL DEFAULT '',
            IsActive BIT NOT NULL DEFAULT 1,
            CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
            UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
        );

        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
        CREATE TABLE Users (
            Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
            EnterpriseId UNIQUEIDENTIFIER NOT NULL,
            Email NVARCHAR(200) NOT NULL,
            PasswordHash NVARCHAR(500) NOT NULL,
            Role NVARCHAR(50) NOT NULL DEFAULT 'Viewer',
            FirstName NVARCHAR(100) NOT NULL DEFAULT '',
            LastName NVARCHAR(100) NOT NULL DEFAULT '',
            IsActive BIT NOT NULL DEFAULT 1,
            CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
            LastLoginAt DATETIME2 NULL,
            RefreshToken NVARCHAR(500) NULL,
            RefreshTokenExpiresAt DATETIME2 NULL,
            FOREIGN KEY (EnterpriseId) REFERENCES Enterprises(Id)
        );

        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ComplianceRules' AND xtype='U')
        CREATE TABLE ComplianceRules (
            Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
            RuleCode NVARCHAR(50) NOT NULL UNIQUE,
            Name NVARCHAR(200) NOT NULL,
            Description NVARCHAR(MAX) NOT NULL DEFAULT '',
            Framework INT NOT NULL,
            DefaultSeverity INT NOT NULL,
            Category NVARCHAR(100) NOT NULL DEFAULT '',
            EvaluationLogic NVARCHAR(MAX) NOT NULL DEFAULT '{}',
            RemediationGuidance NVARCHAR(MAX) NOT NULL DEFAULT '',
            RegulatoryReference NVARCHAR(500) NOT NULL DEFAULT '',
            IsActive BIT NOT NULL DEFAULT 1,
            CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
        );

        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ComplianceViolations' AND xtype='U')
        CREATE TABLE ComplianceViolations (
            Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
            EnterpriseId UNIQUEIDENTIFIER NOT NULL,
            RuleId UNIQUEIDENTIFIER NOT NULL,
            RuleCode NVARCHAR(50) NOT NULL,
            Framework INT NOT NULL,
            Severity INT NOT NULL,
            Status INT NOT NULL DEFAULT 1,
            Title NVARCHAR(300) NOT NULL,
            Description NVARCHAR(MAX) NOT NULL DEFAULT '',
            AffectedResource NVARCHAR(500) NOT NULL DEFAULT '',
            Evidence NVARCHAR(MAX) NOT NULL DEFAULT '{}',
            RemediationSteps NVARCHAR(MAX) NOT NULL DEFAULT '',
            AssignedToUserId UNIQUEIDENTIFIER NULL,
            DetectedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
            ResolvedAt DATETIME2 NULL,
            ResolutionNotes NVARCHAR(MAX) NULL,
            IsAiDetected BIT NOT NULL DEFAULT 0,
            AiConfidenceScore FLOAT NOT NULL DEFAULT 0,
            FOREIGN KEY (EnterpriseId) REFERENCES Enterprises(Id)
        );

        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Documents' AND xtype='U')
        CREATE TABLE Documents (
            Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
            EnterpriseId UNIQUEIDENTIFIER NOT NULL,
            FileName NVARCHAR(500) NOT NULL,
            Type INT NOT NULL,
            MimeType NVARCHAR(100) NOT NULL DEFAULT '',
            FileSizeBytes BIGINT NOT NULL DEFAULT 0,
            StoragePath NVARCHAR(1000) NOT NULL DEFAULT '',
            ContentHash NVARCHAR(64) NOT NULL DEFAULT '',
            IsAnalyzed BIT NOT NULL DEFAULT 0,
            UploadedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
            UploadedByUserId UNIQUEIDENTIFIER NOT NULL,
            AnalyzedAt DATETIME2 NULL,
            Tags NVARCHAR(MAX) NULL,
            FOREIGN KEY (EnterpriseId) REFERENCES Enterprises(Id)
        );

        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DocumentAnalysisResults' AND xtype='U')
        CREATE TABLE DocumentAnalysisResults (
            Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
            DocumentId UNIQUEIDENTIFIER NOT NULL UNIQUE,
            OverallRiskLevel INT NOT NULL,
            RiskScore FLOAT NOT NULL DEFAULT 0,
            ExecutiveSummary NVARCHAR(MAX) NOT NULL DEFAULT '',
            Findings NVARCHAR(MAX) NOT NULL DEFAULT '[]',
            KeyClauses NVARCHAR(MAX) NOT NULL DEFAULT '[]',
            ComplianceConcerns NVARCHAR(MAX) NOT NULL DEFAULT '[]',
            Recommendations NVARCHAR(MAX) NOT NULL DEFAULT '[]',
            ModelUsed NVARCHAR(100) NOT NULL DEFAULT 'claude-sonnet-4-6',
            TokensUsed INT NOT NULL DEFAULT 0,
            AnalyzedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
            FOREIGN KEY (DocumentId) REFERENCES Documents(Id)
        );

        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='VendorRiskProfiles' AND xtype='U')
        CREATE TABLE VendorRiskProfiles (
            Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
            EnterpriseId UNIQUEIDENTIFIER NOT NULL,
            VendorName NVARCHAR(200) NOT NULL,
            VendorDomain NVARCHAR(200) NOT NULL DEFAULT '',
            ServiceCategory NVARCHAR(100) NOT NULL DEFAULT '',
            Country NVARCHAR(100) NOT NULL DEFAULT '',
            CurrentRiskLevel INT NOT NULL DEFAULT 1,
            CompositeRiskScore FLOAT NOT NULL DEFAULT 0,
            ScoreBreakdown NVARCHAR(MAX) NOT NULL DEFAULT '{}',
            DataTypesShared NVARCHAR(MAX) NOT NULL DEFAULT '[]',
            HasSignedDPA BIT NOT NULL DEFAULT 0,
            HasSOC2 BIT NOT NULL DEFAULT 0,
            HasISO27001 BIT NOT NULL DEFAULT 0,
            LastAssessmentDate DATETIME2 NULL,
            NextAssessmentDueDate DATETIME2 NULL,
            CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
            UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
            FOREIGN KEY (EnterpriseId) REFERENCES Enterprises(Id)
        );

        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='BehavioralRiskEvents' AND xtype='U')
        CREATE TABLE BehavioralRiskEvents (
            Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
            EnterpriseId UNIQUEIDENTIFIER NOT NULL,
            UserId UNIQUEIDENTIFIER NULL,
            EntityId NVARCHAR(200) NOT NULL,
            EntityType NVARCHAR(50) NOT NULL,
            EventType NVARCHAR(100) NOT NULL,
            Description NVARCHAR(MAX) NOT NULL DEFAULT '',
            RiskLevel INT NOT NULL,
            AnomalyScore FLOAT NOT NULL DEFAULT 0,
            Metadata NVARCHAR(MAX) NOT NULL DEFAULT '{}',
            IsInvestigated BIT NOT NULL DEFAULT 0,
            OccurredAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
            FOREIGN KEY (EnterpriseId) REFERENCES Enterprises(Id)
        );

        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AuditLog' AND xtype='U')
        CREATE TABLE AuditLog (
            Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
            EnterpriseId UNIQUEIDENTIFIER NOT NULL,
            UserId UNIQUEIDENTIFIER NULL,
            UserEmail NVARCHAR(200) NOT NULL DEFAULT '',
            Action INT NOT NULL,
            ResourceType NVARCHAR(100) NOT NULL DEFAULT '',
            ResourceId NVARCHAR(200) NOT NULL DEFAULT '',
            ResourceName NVARCHAR(500) NOT NULL DEFAULT '',
            Description NVARCHAR(MAX) NOT NULL DEFAULT '',
            OldValue NVARCHAR(MAX) NOT NULL DEFAULT '',
            NewValue NVARCHAR(MAX) NOT NULL DEFAULT '',
            IpAddress NVARCHAR(50) NOT NULL DEFAULT '',
            UserAgent NVARCHAR(500) NOT NULL DEFAULT '',
            ServiceName NVARCHAR(100) NOT NULL DEFAULT '',
            CorrelationId NVARCHAR(100) NOT NULL DEFAULT '',
            IntegrityHash NVARCHAR(64) NOT NULL DEFAULT '',
            OccurredAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
        );

        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Alerts' AND xtype='U')
        CREATE TABLE Alerts (
            Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
            EnterpriseId UNIQUEIDENTIFIER NOT NULL,
            AlertRuleId UNIQUEIDENTIFIER NULL,
            Title NVARCHAR(300) NOT NULL,
            Message NVARCHAR(MAX) NOT NULL DEFAULT '',
            Severity INT NOT NULL,
            TriggerSource NVARCHAR(100) NOT NULL DEFAULT '',
            TriggerResourceId NVARCHAR(200) NOT NULL DEFAULT '',
            TriggerResourceType NVARCHAR(100) NOT NULL DEFAULT '',
            IsAcknowledged BIT NOT NULL DEFAULT 0,
            AcknowledgedByUserId UNIQUEIDENTIFIER NULL,
            AcknowledgedAt DATETIME2 NULL,
            IsResolved BIT NOT NULL DEFAULT 0,
            ResolutionNotes NVARCHAR(MAX) NULL,
            DeliveryAttempts INT NOT NULL DEFAULT 0,
            CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
            SentAt DATETIME2 NULL,
            FOREIGN KEY (EnterpriseId) REFERENCES Enterprises(Id)
        );

        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ComplianceReports' AND xtype='U')
        CREATE TABLE ComplianceReports (
            Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
            EnterpriseId UNIQUEIDENTIFIER NOT NULL,
            Title NVARCHAR(300) NOT NULL,
            Framework INT NOT NULL,
            PeriodStart DATETIME2 NOT NULL,
            PeriodEnd DATETIME2 NOT NULL,
            TotalViolations INT NOT NULL DEFAULT 0,
            CriticalViolations INT NOT NULL DEFAULT 0,
            HighViolations INT NOT NULL DEFAULT 0,
            MediumViolations INT NOT NULL DEFAULT 0,
            LowViolations INT NOT NULL DEFAULT 0,
            ResolvedViolations INT NOT NULL DEFAULT 0,
            ComplianceScore FLOAT NOT NULL DEFAULT 0,
            ExecutiveSummary NVARCHAR(MAX) NOT NULL DEFAULT '',
            DetailedFindings NVARCHAR(MAX) NOT NULL DEFAULT '{}',
            Recommendations NVARCHAR(MAX) NOT NULL DEFAULT '',
            GeneratedByUserId UNIQUEIDENTIFIER NOT NULL,
            GeneratedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
            FOREIGN KEY (EnterpriseId) REFERENCES Enterprises(Id)
        );
        """;
}
