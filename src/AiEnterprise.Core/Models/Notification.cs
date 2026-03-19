using AiEnterprise.Core.Enums;

namespace AiEnterprise.Core.Models;

public class AlertRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnterpriseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TriggerCondition { get; set; } = string.Empty; // JSON rule (e.g., severity >= High)
    public List<NotificationChannel> Channels { get; set; } = new();
    public List<Guid> RecipientUserIds { get; set; } = new();
    public List<string> WebhookUrls { get; set; } = new();
    public string EscalationPolicy { get; set; } = string.Empty; // JSON escalation config
    public bool IsActive { get; set; } = true;
    public int CooldownMinutes { get; set; } = 60;              // Prevent alert storms
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Alert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnterpriseId { get; set; }
    public Guid? AlertRuleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ViolationSeverity Severity { get; set; }
    public string TriggerSource { get; set; } = string.Empty;    // Which service triggered this
    public string TriggerResourceId { get; set; } = string.Empty;
    public string TriggerResourceType { get; set; } = string.Empty;
    public bool IsAcknowledged { get; set; } = false;
    public Guid? AcknowledgedByUserId { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public bool IsResolved { get; set; } = false;
    public string? ResolutionNotes { get; set; }
    public int DeliveryAttempts { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}
