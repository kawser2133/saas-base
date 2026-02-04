using System;

namespace SaaSBase.Domain;

// GDPR Data Subject Request
public class DataSubjectRequest : BaseEntity, ITenantEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string RequestType { get; set; } = string.Empty; // ACCESS, RECTIFICATION, ERASURE, PORTABILITY, RESTRICTION, OBJECTION
    public string Status { get; set; } = "PENDING"; // PENDING, IN_PROGRESS, COMPLETED, REJECTED, CANCELLED
    public string Description { get; set; } = string.Empty;
    public string? RequestedData { get; set; } // What specific data is being requested
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ResponseData { get; set; } // Response or exported data
    public string? RejectionReason { get; set; }
    public string? AssignedTo { get; set; } // Who is handling the request
    public string? Notes { get; set; }
    public string? VerificationMethod { get; set; } // How identity was verified
    public DateTimeOffset? VerificationDate { get; set; }
    public string? LegalBasis { get; set; } // Legal basis for processing
    public string? ProcessingPurpose { get; set; } // Purpose of data processing
}

// Privacy Settings
public class PrivacySettings : BaseEntity, ITenantEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public bool MarketingConsent { get; set; } = false;
    public bool AnalyticsConsent { get; set; } = false;
    public bool FunctionalConsent { get; set; } = true;
    public bool NecessaryConsent { get; set; } = true;
    public bool ThirdPartyConsent { get; set; } = false;
    public bool DataProcessingConsent { get; set; } = false;
    public bool EmailNotifications { get; set; } = true;
    public bool SmsNotifications { get; set; } = false;
    public bool PhoneCalls { get; set; } = false;
    public bool DataSharing { get; set; } = false;
    public bool Profiling { get; set; } = false;
    public bool AutomatedDecisionMaking { get; set; } = false;
    public string? DataProcessingPurpose { get; set; }
    public string? LegalBasis { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
    public string? LastUpdatedBy { get; set; }
}

// Data Breach Incident
public class DataBreachIncident : BaseEntity, ITenantEntity
{
    public string IncidentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "MEDIUM"; // LOW, MEDIUM, HIGH, CRITICAL
    public string Status { get; set; } = "DETECTED"; // DETECTED, INVESTIGATING, CONTAINED, RESOLVED, CLOSED
    public DateTimeOffset DetectedAt { get; set; }
    public DateTimeOffset? ContainedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? AffectedDataCategories { get; set; }
    public int? AffectedRecordsCount { get; set; }
    public string? AffectedUsers { get; set; }
    public string? RootCause { get; set; }
    public string? Impact { get; set; }
    public string? MitigationActions { get; set; }
    public string? NotificationStatus { get; set; } = "PENDING"; // PENDING, SENT, ACKNOWLEDGED
    public DateTimeOffset? NotificationSentAt { get; set; }
    public string? RegulatoryNotificationStatus { get; set; } = "PENDING"; // PENDING, SENT, ACKNOWLEDGED
    public DateTimeOffset? RegulatoryNotificationSentAt { get; set; }
    public string? AssignedTo { get; set; }
    public string? Notes { get; set; }
    public string? LessonsLearned { get; set; }
}

// Data Processing Record
public class DataProcessingRecord : BaseEntity, ITenantEntity
{
    public string ProcessingActivity { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string LegalBasis { get; set; } = string.Empty;
    public string DataCategories { get; set; } = string.Empty;
    public string DataSubjects { get; set; } = string.Empty;
    public string? Recipients { get; set; }
    public string? ThirdCountryTransfers { get; set; }
    public string? RetentionPeriod { get; set; }
    public string? SecurityMeasures { get; set; }
    public string? DataProtectionOfficer { get; set; }
    public string? ContactDetails { get; set; }
    public string? SupervisoryAuthority { get; set; }
    public string? RiskAssessment { get; set; }
    public string? DataProtectionImpactAssessment { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset LastReviewedAt { get; set; }
    public string? LastReviewedBy { get; set; }
}

// Privacy Impact Assessment
public class PrivacyImpactAssessment : BaseEntity, ITenantEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProcessingActivity { get; set; } = string.Empty;
    public string Status { get; set; } = "DRAFT"; // DRAFT, IN_REVIEW, APPROVED, REJECTED
    public string RiskLevel { get; set; } = "MEDIUM"; // LOW, MEDIUM, HIGH, CRITICAL
    public string DataCategories { get; set; } = string.Empty;
    public string ProcessingPurposes { get; set; } = string.Empty;
    public string LegalBasis { get; set; } = string.Empty;
    public string? Necessity { get; set; }
    public string? Proportionality { get; set; }
    public string? DataMinimization { get; set; }
    public string? SecurityMeasures { get; set; }
    public string? RiskMitigation { get; set; }
    public string? StakeholderConsultation { get; set; }
    public string? SupervisoryAuthorityConsultation { get; set; }
    public string? Recommendations { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? Notes { get; set; }
}

// Data Transfer Record
public class DataTransferRecord : BaseEntity, ITenantEntity
{
    public string TransferType { get; set; } = string.Empty; // INTERNATIONAL, THIRD_PARTY, SUBCONTRACTOR
    public string Destination { get; set; } = string.Empty;
    public string DataCategories { get; set; } = string.Empty;
    public string LegalBasis { get; set; } = string.Empty;
    public string? AdequacyDecision { get; set; }
    public string? AppropriateSafeguards { get; set; }
    public string? StandardContractualClauses { get; set; }
    public string? BindingCorporateRules { get; set; }
    public string? Certification { get; set; }
    public string? CodeOfConduct { get; set; }
    public string? Derogation { get; set; }
    public string? RecipientDetails { get; set; }
    public string? Purpose { get; set; }
    public DateTimeOffset TransferDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}
