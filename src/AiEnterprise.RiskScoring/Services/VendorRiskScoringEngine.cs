using AiEnterprise.Core.DTOs;
using AiEnterprise.Core.Enums;
using AiEnterprise.Core.Interfaces.Services;
using AiEnterprise.Core.Models;
using AiEnterprise.Infrastructure.Configuration;
using AiEnterprise.Shared.Constants;
using Dapper;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AiEnterprise.RiskScoring.Services;

/// <summary>
/// Vendor Risk Scoring Engine - solves a critical enterprise blind spot.
///
/// 60% of enterprise data breaches originate from third-party vendors (Ponemon Institute, 2024).
/// Most enterprises only assess vendor risk annually, if at all.
/// This engine provides continuous, objective risk scoring across 7 dimensions.
/// </summary>
public class VendorRiskScoringEngine : IRiskScoringService
{
    private readonly DapperContext _db;
    private readonly ICacheService _cache;
    private readonly ILogger<VendorRiskScoringEngine> _logger;

    // High-risk countries for data sovereignty/jurisdiction risk
    private static readonly HashSet<string> HighRiskJurisdictions = new(StringComparer.OrdinalIgnoreCase)
    {
        "China", "Russia", "North Korea", "Iran", "Belarus", "Venezuela"
    };

    // Known high-risk data types that trigger enhanced scrutiny
    private static readonly HashSet<string> SensitiveDataTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "PII", "PHI", "Financial", "Payment", "Credentials", "Biometric", "Children"
    };

    public VendorRiskScoringEngine(DapperContext db, ICacheService cache, ILogger<VendorRiskScoringEngine> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<VendorRiskSummary> AssessVendorRiskAsync(VendorAssessmentRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Assessing vendor risk for {VendorName} (Enterprise {EnterpriseId})",
            request.VendorName, request.EnterpriseId);

        var breakdown = ComputeScoreBreakdown(request);
        var compositeScore = ComputeCompositeScore(breakdown);
        var riskLevel = ScoreToRiskLevel(compositeScore);
        var topRisks = IdentifyTopRisks(request, breakdown);

        var profile = new VendorRiskProfile
        {
            EnterpriseId = request.EnterpriseId,
            VendorName = request.VendorName,
            VendorDomain = request.VendorDomain,
            ServiceCategory = request.ServiceCategory,
            Country = request.Country,
            CurrentRiskLevel = riskLevel,
            CompositeRiskScore = compositeScore,
            ScoreBreakdown = breakdown,
            DataTypesShared = request.DataTypesShared,
            HasSignedDPA = request.HasSignedDPA,
            HasSOC2 = request.HasSOC2,
            HasISO27001 = request.HasISO27001,
            LastAssessmentDate = DateTime.UtcNow,
            NextAssessmentDueDate = DateTime.UtcNow.AddDays(riskLevel >= RiskLevel.High ? 90 : 180)
        };

        await UpsertVendorProfileAsync(profile);

        // Invalidate cache
        await _cache.RemoveAsync(AppConstants.CacheKeys.VendorRisk(request.EnterpriseId), ct);

        return new VendorRiskSummary(
            profile.Id,
            request.VendorName,
            riskLevel,
            compositeScore,
            request.ServiceCategory,
            DateTime.UtcNow,
            topRisks
        );
    }

    public async Task<IReadOnlyList<VendorRiskSummary>> GetVendorRiskProfilesAsync(Guid enterpriseId, CancellationToken ct = default)
    {
        var cacheKey = AppConstants.CacheKeys.VendorRisk(enterpriseId);
        var cached = await _cache.GetAsync<List<VendorRiskSummary>>(cacheKey, ct);
        if (cached is not null) return cached;

        using var connection = _db.CreateConnection();
        const string sql = """
            SELECT Id, VendorName, CurrentRiskLevel, CompositeRiskScore, ServiceCategory, LastAssessmentDate, DataTypesShared
            FROM VendorRiskProfiles
            WHERE EnterpriseId = @EnterpriseId
            ORDER BY CompositeRiskScore DESC
            """;

        var rows = await connection.QueryAsync(sql, new { EnterpriseId = enterpriseId });
        var result = rows.Select(r =>
        {
            var dataTypes = JsonSerializer.Deserialize<List<string>>(r.DataTypesShared) ?? new();
            return new VendorRiskSummary(
                r.Id, r.VendorName, (RiskLevel)r.CurrentRiskLevel,
                r.CompositeRiskScore, r.ServiceCategory, r.LastAssessmentDate,
                dataTypes.Take(3).ToList()
            );
        }).ToList();

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30), ct);
        return result;
    }

    public async Task<BehavioralRiskEvent> RecordBehavioralAnomalyAsync(BehavioralAnomalyRequest request, CancellationToken ct = default)
    {
        var riskLevel = ClassifyBehavioralRisk(request.EventType, request.Description);
        var anomalyScore = CalculateAnomalyScore(request.EventType);

        var evt = new BehavioralRiskEvent
        {
            EnterpriseId = request.EnterpriseId,
            EntityId = request.EntityId,
            EntityType = request.EntityType,
            EventType = request.EventType,
            Description = request.Description,
            RiskLevel = riskLevel,
            AnomalyScore = anomalyScore,
            Metadata = request.Metadata
        };

        using var connection = _db.CreateConnection();
        const string sql = """
            INSERT INTO BehavioralRiskEvents
            (Id, EnterpriseId, EntityId, EntityType, EventType, Description, RiskLevel, AnomalyScore, Metadata, OccurredAt)
            VALUES
            (@Id, @EnterpriseId, @EntityId, @EntityType, @EventType, @Description, @RiskLevel, @AnomalyScore, @Metadata, @OccurredAt)
            """;

        await connection.ExecuteAsync(sql, new
        {
            evt.Id, evt.EnterpriseId, evt.EntityId, evt.EntityType,
            evt.EventType, evt.Description,
            RiskLevel = (int)evt.RiskLevel,
            evt.AnomalyScore, evt.Metadata, evt.OccurredAt
        });

        _logger.LogWarning("Behavioral anomaly recorded: {EventType} for {EntityId} (Score: {Score})",
            request.EventType, request.EntityId, anomalyScore);

        return evt;
    }

    public async Task<IReadOnlyList<BehavioralRiskEvent>> GetRecentAnomaliesAsync(Guid enterpriseId, int limit, CancellationToken ct = default)
    {
        using var connection = _db.CreateConnection();
        const string sql = """
            SELECT TOP (@Limit) *
            FROM BehavioralRiskEvents
            WHERE EnterpriseId = @EnterpriseId
            ORDER BY AnomalyScore DESC, OccurredAt DESC
            """;

        var rows = await connection.QueryAsync<BehavioralRiskEvent>(sql, new { EnterpriseId = enterpriseId, Limit = limit });
        return rows.ToList();
    }

    // --- Private scoring logic ---

    private VendorRiskScoreBreakdown ComputeScoreBreakdown(VendorAssessmentRequest req)
    {
        // Data Security Score (lower = better, inverted for display: high score = high risk)
        double dataSecurityRisk = 0;
        if (!req.HasSignedDPA) dataSecurityRisk += 40;
        if (req.DataTypesShared.Any(d => SensitiveDataTypes.Contains(d)))
        {
            dataSecurityRisk += 20;
            if (!req.HasSOC2) dataSecurityRisk += 20;
        }

        // Compliance Certifications Score (lack of certs = higher risk)
        double certRisk = 100;
        if (req.HasSOC2) certRisk -= 35;
        if (req.HasISO27001) certRisk -= 35;
        if (req.HasSignedDPA) certRisk -= 20;
        certRisk = Math.Max(0, certRisk);

        // Geographic Risk
        double geoRisk = HighRiskJurisdictions.Contains(req.Country) ? 90 : 15;

        // Access Level Risk (based on data sensitivity)
        double accessRisk = req.DataTypesShared.Count(d => SensitiveDataTypes.Contains(d)) * 15.0;
        accessRisk = Math.Min(100, accessRisk);

        // Contractual Protection
        double contractRisk = req.HasSignedDPA ? 20 : 80;

        return new VendorRiskScoreBreakdown
        {
            DataSecurityScore = Math.Min(100, dataSecurityRisk),
            ComplianceCertificationScore = certRisk,
            IncidentHistoryScore = 30,          // Default; would integrate with breach databases in production
            ContractualProtectionScore = contractRisk,
            GeographicRiskScore = geoRisk,
            FinancialStabilityScore = 25,       // Default; would integrate with financial APIs
            AccessPrivilegeScore = accessRisk
        };
    }

    private static double ComputeCompositeScore(VendorRiskScoreBreakdown b)
    {
        // Weighted composite score (weights sum to 1.0)
        return Math.Round(
            b.DataSecurityScore * 0.25 +
            b.ComplianceCertificationScore * 0.20 +
            b.IncidentHistoryScore * 0.15 +
            b.ContractualProtectionScore * 0.15 +
            b.GeographicRiskScore * 0.10 +
            b.FinancialStabilityScore * 0.05 +
            b.AccessPrivilegeScore * 0.10,
            1);
    }

    private static RiskLevel ScoreToRiskLevel(double score) => score switch
    {
        >= 80 => RiskLevel.Critical,
        >= 60 => RiskLevel.High,
        >= 40 => RiskLevel.Medium,
        >= 20 => RiskLevel.Low,
        _ => RiskLevel.Negligible
    };

    private static List<string> IdentifyTopRisks(VendorAssessmentRequest req, VendorRiskScoreBreakdown breakdown)
    {
        var risks = new List<(double Score, string Risk)>();

        if (!req.HasSignedDPA) risks.Add((breakdown.ContractualProtectionScore, "No Data Processing Agreement signed"));
        if (!req.HasSOC2) risks.Add((breakdown.ComplianceCertificationScore * 0.5, "No SOC 2 certification"));
        if (!req.HasISO27001) risks.Add((breakdown.ComplianceCertificationScore * 0.5, "No ISO 27001 certification"));
        if (HighRiskJurisdictions.Contains(req.Country)) risks.Add((breakdown.GeographicRiskScore, $"High-risk jurisdiction: {req.Country}"));
        if (req.DataTypesShared.Any(d => SensitiveDataTypes.Contains(d)))
            risks.Add((breakdown.AccessPrivilegeScore, $"Access to sensitive data: {string.Join(", ", req.DataTypesShared.Where(d => SensitiveDataTypes.Contains(d)))}"));

        return risks.OrderByDescending(r => r.Score).Take(3).Select(r => r.Risk).ToList();
    }

    private static RiskLevel ClassifyBehavioralRisk(string eventType, string description)
    {
        var upperEvent = eventType.ToUpperInvariant();
        return upperEvent switch
        {
            var e when e.Contains("EXFILTRAT") || e.Contains("MASS_DOWNLOAD") => RiskLevel.Critical,
            var e when e.Contains("OFFHOURS") && description.Contains("admin") => RiskLevel.High,
            var e when e.Contains("PRIVILEGE_ESCALAT") => RiskLevel.High,
            var e when e.Contains("FAILED_AUTH") => RiskLevel.Medium,
            var e when e.Contains("OFFHOURS") => RiskLevel.Medium,
            _ => RiskLevel.Low
        };
    }

    private static double CalculateAnomalyScore(string eventType)
    {
        var upperEvent = eventType.ToUpperInvariant();
        return upperEvent switch
        {
            var e when e.Contains("EXFILTRAT") => 95,
            var e when e.Contains("MASS_DOWNLOAD") => 90,
            var e when e.Contains("PRIVILEGE_ESCALAT") => 85,
            var e when e.Contains("OFFHOURS") && e.Contains("ADMIN") => 75,
            var e when e.Contains("FAILED_AUTH") => 55,
            var e when e.Contains("OFFHOURS") => 45,
            _ => 25
        };
    }

    private async Task UpsertVendorProfileAsync(VendorRiskProfile profile)
    {
        using var connection = _db.CreateConnection();
        const string sql = """
            MERGE VendorRiskProfiles AS target
            USING (SELECT @EnterpriseId AS EnterpriseId, @VendorName AS VendorName) AS source
                ON target.EnterpriseId = source.EnterpriseId AND target.VendorName = source.VendorName
            WHEN MATCHED THEN
                UPDATE SET
                    CurrentRiskLevel = @CurrentRiskLevel,
                    CompositeRiskScore = @CompositeRiskScore,
                    ScoreBreakdown = @ScoreBreakdown,
                    DataTypesShared = @DataTypesShared,
                    HasSignedDPA = @HasSignedDPA,
                    HasSOC2 = @HasSOC2,
                    HasISO27001 = @HasISO27001,
                    LastAssessmentDate = @LastAssessmentDate,
                    NextAssessmentDueDate = @NextAssessmentDueDate,
                    UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (Id, EnterpriseId, VendorName, VendorDomain, ServiceCategory, Country, CurrentRiskLevel, CompositeRiskScore,
                        ScoreBreakdown, DataTypesShared, HasSignedDPA, HasSOC2, HasISO27001, LastAssessmentDate, NextAssessmentDueDate)
                VALUES (@Id, @EnterpriseId, @VendorName, @VendorDomain, @ServiceCategory, @Country, @CurrentRiskLevel, @CompositeRiskScore,
                        @ScoreBreakdown, @DataTypesShared, @HasSignedDPA, @HasSOC2, @HasISO27001, @LastAssessmentDate, @NextAssessmentDueDate);
            """;

        await connection.ExecuteAsync(sql, new
        {
            profile.Id, profile.EnterpriseId, profile.VendorName, profile.VendorDomain,
            profile.ServiceCategory, profile.Country,
            CurrentRiskLevel = (int)profile.CurrentRiskLevel,
            profile.CompositeRiskScore,
            ScoreBreakdown = JsonSerializer.Serialize(profile.ScoreBreakdown),
            DataTypesShared = JsonSerializer.Serialize(profile.DataTypesShared),
            profile.HasSignedDPA, profile.HasSOC2, profile.HasISO27001,
            profile.LastAssessmentDate, profile.NextAssessmentDueDate
        });
    }
}
