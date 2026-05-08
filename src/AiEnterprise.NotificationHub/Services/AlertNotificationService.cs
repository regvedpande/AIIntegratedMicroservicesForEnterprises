using AiEnterprise.Core.Enums;
using AiEnterprise.Core.Interfaces.Services;
using AiEnterprise.Core.Models;
using AiEnterprise.Infrastructure.Configuration;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

namespace AiEnterprise.NotificationHub.Services;

/// <summary>
/// Intelligent Alert Notification Service.
///
/// Enterprise problem: Alert fatigue - security teams receive thousands of low-quality alerts
/// and begin ignoring them. Studies show 70% of enterprise alerts are false positives or noise.
///
/// This service applies intelligent filtering, deduplication, and priority scoring
/// to ensure only actionable, high-quality alerts reach the right people, via the right channel.
/// </summary>
public class AlertNotificationService : INotificationService
{
    private readonly DapperContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICacheService _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AlertNotificationService> _logger;

    public AlertNotificationService(
        DapperContext db,
        IHttpClientFactory httpClientFactory,
        ICacheService cache,
        IConfiguration configuration,
        ILogger<AlertNotificationService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAlertAsync(Alert alert, CancellationToken ct = default)
    {
        // Deduplication: Check if a similar alert was sent recently (cooldown window)
        var dedupeKey = $"alert:dedup:{alert.EnterpriseId}:{alert.TriggerSource}:{alert.TriggerResourceType}";
        if (await _cache.ExistsAsync(dedupeKey, ct))
        {
            _logger.LogDebug("Alert suppressed by deduplication: {Title}", alert.Title);
            return;
        }

        // Save alert to database
        await PersistAlertAsync(alert);

        // Route to appropriate channels based on severity
        var deliveryTasks = new List<Task>();

        if (alert.Severity >= ViolationSeverity.High)
        {
            // High/Critical: send via all channels - email + webhook
            deliveryTasks.Add(SendWebhookAsync(alert, ct));
            deliveryTasks.Add(SendEmailNotificationAsync(alert, ct));
        }
        else
        {
            // Medium/Low: in-app only (avoid overwhelming teams)
            _logger.LogInformation("Alert {AlertId} queued for in-app delivery (severity: {Severity})",
                alert.Id, alert.Severity);
        }

        await Task.WhenAll(deliveryTasks);

        // Mark as sent and set deduplication window
        await MarkAlertSentAsync(alert.Id);

        var cooldownMinutes = alert.Severity switch
        {
            ViolationSeverity.Critical => 15,
            ViolationSeverity.High => 30,
            _ => 60
        };
        await _cache.SetAsync(dedupeKey, true, TimeSpan.FromMinutes(cooldownMinutes), ct);

        _logger.LogInformation("Alert sent: {Title} (Severity: {Severity})", alert.Title, alert.Severity);
    }

    public async Task<IReadOnlyList<Alert>> GetActiveAlertsAsync(Guid enterpriseId, CancellationToken ct = default)
    {
        var cacheKey = $"alerts:active:{enterpriseId}";
        var cached = await _cache.GetAsync<List<Alert>>(cacheKey, ct);
        if (cached is not null) return cached;

        using var connection = _db.CreateConnection();
        const string sql = """
            SELECT TOP 100 *
            FROM Alerts
            WHERE EnterpriseId = @EnterpriseId
              AND IsResolved = 0
            ORDER BY
              CASE Severity WHEN 4 THEN 1 WHEN 3 THEN 2 WHEN 2 THEN 3 ELSE 4 END,
              CreatedAt DESC
            """;

        var alerts = (await connection.QueryAsync<Alert>(sql, new { EnterpriseId = enterpriseId })).ToList();
        await _cache.SetAsync(cacheKey, alerts, TimeSpan.FromMinutes(2), ct);
        return alerts;
    }

    public async Task<bool> AcknowledgeAlertAsync(Guid alertId, Guid userId, CancellationToken ct = default)
    {
        using var connection = _db.CreateConnection();
        const string sql = """
            UPDATE Alerts
            SET IsAcknowledged = 1, AcknowledgedByUserId = @UserId, AcknowledgedAt = @AcknowledgedAt
            WHERE Id = @AlertId AND IsAcknowledged = 0
            """;

        var rows = await connection.ExecuteAsync(sql, new
        {
            AlertId = alertId,
            UserId = userId,
            AcknowledgedAt = DateTime.UtcNow
        });

        if (rows > 0)
        {
            // Invalidate active alerts cache for this enterprise
            var alert = await connection.QuerySingleOrDefaultAsync<Alert>(
                "SELECT EnterpriseId FROM Alerts WHERE Id = @Id", new { Id = alertId });
            if (alert is not null)
                await _cache.RemoveAsync($"alerts:active:{alert.EnterpriseId}", ct);
        }

        return rows > 0;
    }

    private async Task SendWebhookAsync(Alert alert, CancellationToken ct)
    {
        var webhookUrl = _configuration["Notifications:WebhookUrl"];
        if (string.IsNullOrWhiteSpace(webhookUrl)) return;

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var payload = new
            {
                alert.Id,
                alert.Title,
                alert.Message,
                Severity = alert.Severity.ToString(),
                alert.TriggerSource,
                alert.CreatedAt,
                Platform = "AiEnterprise ECRI"
            };

            using var content = JsonContent.Create(payload);
            var response = await client.PostAsync(webhookUrl, content, ct);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Webhook delivered for alert {AlertId}", alert.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deliver webhook for alert {AlertId}", alert.Id);
        }
    }

    private async Task SendEmailNotificationAsync(Alert alert, CancellationToken ct)
    {
        var emailRecipients = _configuration["Notifications:AlertEmailRecipients"];
        if (string.IsNullOrWhiteSpace(emailRecipients))
        {
            _logger.LogDebug("Email notification skipped — no recipients configured.");
            return;
        }

        var smtpHost = _configuration["Notifications:Smtp:Host"];
        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            _logger.LogWarning("Email notification skipped — Notifications:Smtp:Host is not configured.");
            return;
        }

        var port = int.TryParse(_configuration["Notifications:Smtp:Port"], out var p) ? p : 587;
        var fromAddress = _configuration["Notifications:Smtp:FromAddress"] ?? "noreply@aienterpriseecri.com";
        var fromName = _configuration["Notifications:Smtp:FromName"] ?? "AiEnterprise ECRI";
        var smtpUser = _configuration["Notifications:Smtp:Username"];
        var smtpPass = _configuration["Notifications:Smtp:Password"];
        var enableSsl = !string.Equals(_configuration["Notifications:Smtp:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase);

        try
        {
            using var smtp = new SmtpClient(smtpHost, port)
            {
                EnableSsl = enableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrWhiteSpace(smtpUser) && !string.IsNullOrWhiteSpace(smtpPass))
                smtp.Credentials = new NetworkCredential(smtpUser, smtpPass);

            using var message = new MailMessage
            {
                From = new MailAddress(fromAddress, fromName),
                Subject = $"[{alert.Severity.ToString().ToUpperInvariant()}] {alert.Title}",
                Body = BuildEmailBody(alert),
                IsBodyHtml = false
            };

            foreach (var recipient in emailRecipients.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                message.To.Add(recipient);

            await smtp.SendMailAsync(message, ct);

            _logger.LogInformation("Email alert sent for {AlertId} to {Recipients}", alert.Id, emailRecipients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email alert for {AlertId}", alert.Id);
        }
    }

    private static string BuildEmailBody(Alert alert)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"ENTERPRISE COMPLIANCE ALERT - {alert.Severity.ToString().ToUpper()}");
        sb.AppendLine(new string('=', 60));
        sb.AppendLine($"Title: {alert.Title}");
        sb.AppendLine($"Severity: {alert.Severity}");
        sb.AppendLine($"Service: {alert.TriggerSource}");
        sb.AppendLine($"Resource: {alert.TriggerResourceType} / {alert.TriggerResourceId}");
        sb.AppendLine($"Detected At: {alert.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("DETAILS:");
        sb.AppendLine(alert.Message);
        sb.AppendLine();
        sb.AppendLine("Please log in to the AiEnterprise Compliance Portal to review and acknowledge this alert.");
        return sb.ToString();
    }

    private async Task PersistAlertAsync(Alert alert)
    {
        using var connection = _db.CreateConnection();
        const string sql = """
            INSERT INTO Alerts
            (Id, EnterpriseId, AlertRuleId, Title, Message, Severity, TriggerSource, TriggerResourceId, TriggerResourceType, CreatedAt)
            VALUES
            (@Id, @EnterpriseId, @AlertRuleId, @Title, @Message, @Severity, @TriggerSource, @TriggerResourceId, @TriggerResourceType, @CreatedAt)
            """;

        await connection.ExecuteAsync(sql, new
        {
            alert.Id, alert.EnterpriseId, alert.AlertRuleId,
            alert.Title, alert.Message,
            Severity = (int)alert.Severity,
            alert.TriggerSource, alert.TriggerResourceId, alert.TriggerResourceType,
            alert.CreatedAt
        });
    }

    private async Task MarkAlertSentAsync(Guid alertId)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE Alerts SET SentAt = @SentAt, DeliveryAttempts = DeliveryAttempts + 1 WHERE Id = @Id",
            new { Id = alertId, SentAt = DateTime.UtcNow });
    }
}
