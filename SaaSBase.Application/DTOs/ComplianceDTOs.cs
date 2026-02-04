using System.ComponentModel.DataAnnotations;

namespace SaaSBase.Application.DTOs;

// Data Subject Request DTOs
public class DataSubjectRequestDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? RequestedData { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ResponseData { get; set; }
    public string? RejectionReason { get; set; }
    public string? AssignedTo { get; set; }
    public string? Notes { get; set; }
    public string? VerificationMethod { get; set; }
    public DateTimeOffset? VerificationDate { get; set; }
    public string? LegalBasis { get; set; }
    public string? ProcessingPurpose { get; set; }
}

public class CreateDataSubjectRequestDto
{
    [Required]
    public Guid UserId { get; set; }
    [Required]
    public string RequestType { get; set; } = string.Empty;
    [Required]
    public string Description { get; set; } = string.Empty;
    public string? RequestedData { get; set; }
    public string? LegalBasis { get; set; }
    public string? ProcessingPurpose { get; set; }
}

public class UpdateDataSubjectRequestDto
{
    public string Status { get; set; } = string.Empty;
    public string? ResponseData { get; set; }
    public string? RejectionReason { get; set; }
    public string? AssignedTo { get; set; }
    public string? Notes { get; set; }
    public string? VerificationMethod { get; set; }
    public DateTimeOffset? VerificationDate { get; set; }
}

// Privacy Settings DTOs
public class PrivacySettingsDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public bool MarketingConsent { get; set; }
    public bool AnalyticsConsent { get; set; }
    public bool FunctionalConsent { get; set; }
    public bool NecessaryConsent { get; set; }
    public bool ThirdPartyConsent { get; set; }
    public bool DataProcessingConsent { get; set; }
    public bool EmailNotifications { get; set; }
    public bool SmsNotifications { get; set; }
    public bool PhoneCalls { get; set; }
    public bool DataSharing { get; set; }
    public bool Profiling { get; set; }
    public bool AutomatedDecisionMaking { get; set; }
    public string? DataProcessingPurpose { get; set; }
    public string? LegalBasis { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
    public string? LastUpdatedBy { get; set; }
}

public class UpdatePrivacySettingsDto
{
    public bool MarketingConsent { get; set; }
    public bool AnalyticsConsent { get; set; }
    public bool FunctionalConsent { get; set; }
    public bool NecessaryConsent { get; set; }
    public bool ThirdPartyConsent { get; set; }
    public bool DataProcessingConsent { get; set; }
    public bool EmailNotifications { get; set; }
    public bool SmsNotifications { get; set; }
    public bool PhoneCalls { get; set; }
    public bool DataSharing { get; set; }
    public bool Profiling { get; set; }
    public bool AutomatedDecisionMaking { get; set; }
    public string? DataProcessingPurpose { get; set; }
    public string? LegalBasis { get; set; }
}

// Data Breach Incident DTOs
public class DataBreachIncidentDto
{
    public Guid Id { get; set; }
    public string IncidentNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset DetectedAt { get; set; }
    public DateTimeOffset? ContainedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? AffectedDataCategories { get; set; }
    public int? AffectedRecordsCount { get; set; }
    public string? AffectedUsers { get; set; }
    public string? RootCause { get; set; }
    public string? Impact { get; set; }
    public string? MitigationActions { get; set; }
    public string NotificationStatus { get; set; } = string.Empty;
    public DateTimeOffset? NotificationSentAt { get; set; }
    public string RegulatoryNotificationStatus { get; set; } = string.Empty;
    public DateTimeOffset? RegulatoryNotificationSentAt { get; set; }
    public string? AssignedTo { get; set; }
    public string? Notes { get; set; }
    public string? LessonsLearned { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
}

public class CreateDataBreachIncidentDto
{
    [Required]
    public string Title { get; set; } = string.Empty;
    [Required]
    public string Description { get; set; } = string.Empty;
    [Required]
    public string Severity { get; set; } = string.Empty;
    public string? AffectedDataCategories { get; set; }
    public int? AffectedRecordsCount { get; set; }
    public string? AffectedUsers { get; set; }
    public string? RootCause { get; set; }
    public string? Impact { get; set; }
    public string? MitigationActions { get; set; }
    public string? AssignedTo { get; set; }
    public string? Notes { get; set; }
}

public class UpdateDataBreachIncidentDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AffectedDataCategories { get; set; }
    public int? AffectedRecordsCount { get; set; }
    public string? AffectedUsers { get; set; }
    public string? RootCause { get; set; }
    public string? Impact { get; set; }
    public string? MitigationActions { get; set; }
    public string? AssignedTo { get; set; }
    public string? Notes { get; set; }
    public string? LessonsLearned { get; set; }
}

// Privacy Impact Assessment DTOs
public class PrivacyImpactAssessmentDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProcessingActivity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
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
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
}

public class CreatePrivacyImpactAssessmentDto
{
    [Required]
    public string Title { get; set; } = string.Empty;
    [Required]
    public string Description { get; set; } = string.Empty;
    [Required]
    public string ProcessingActivity { get; set; } = string.Empty;
    [Required]
    public string RiskLevel { get; set; } = string.Empty;
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
    public string? Notes { get; set; }
}

public class UpdatePrivacyImpactAssessmentDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProcessingActivity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
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
    public string? Notes { get; set; }
}

// Data Processing Record DTOs
public class DataProcessingRecordDto
{
    public Guid Id { get; set; }
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
    public bool IsActive { get; set; }
    public DateTimeOffset LastReviewedAt { get; set; }
    public string? LastReviewedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
}

public class CreateDataProcessingRecordDto
{
    [Required]
    public string ProcessingActivity { get; set; } = string.Empty;
    [Required]
    public string Purpose { get; set; } = string.Empty;
    [Required]
    public string LegalBasis { get; set; } = string.Empty;
    [Required]
    public string DataCategories { get; set; } = string.Empty;
    [Required]
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
}

public class UpdateDataProcessingRecordDto
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
    public bool IsActive { get; set; }
}

// Data Transfer Record DTOs
public class DataTransferRecordDto
{
    public Guid Id { get; set; }
    public string TransferType { get; set; } = string.Empty;
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
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
}

public class CreateDataTransferRecordDto
{
    [Required]
    public string TransferType { get; set; } = string.Empty;
    [Required]
    public string Destination { get; set; } = string.Empty;
    [Required]
    public string DataCategories { get; set; } = string.Empty;
    [Required]
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
}

public class UpdateDataTransferRecordDto
{
    public string TransferType { get; set; } = string.Empty;
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
    public bool IsActive { get; set; }
}

public class DataAccessReportDto
{
    public Guid UserId { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public IEnumerable<PersonalDataLocationDto> PersonalDataLocations { get; set; } = new List<PersonalDataLocationDto>();
    public IEnumerable<DataProcessingActivityDto> ProcessingActivities { get; set; } = new List<DataProcessingActivityDto>();
    public IEnumerable<DataTransferDto> DataTransfers { get; set; } = new List<DataTransferDto>();
}

public class DataCategory
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LegalBasis { get; set; } = string.Empty;
    public DateTimeOffset? RetentionPeriod { get; set; }
    public List<string> DataTypes { get; set; } = new List<string>();
}

public class ProcessingActivity
{
    public string Name { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string LegalBasis { get; set; } = string.Empty;
    public List<string> DataCategories { get; set; } = new List<string>();
    public List<string> Recipients { get; set; } = new List<string>();
    public DateTimeOffset? RetentionPeriod { get; set; }
}

public class DataSharing
{
    public string Recipient { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string LegalBasis { get; set; } = string.Empty;
    public List<string> DataCategories { get; set; } = new List<string>();
    public DateTimeOffset SharedAt { get; set; }
}

public class DataProcessingActivityDto
{
    public string Activity { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string LegalBasis { get; set; } = string.Empty;
    public string DataCategories { get; set; } = string.Empty;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
}

public class DataTransferDto
{
    public string Destination { get; set; } = string.Empty;
    public string DataCategories { get; set; } = string.Empty;
    public string LegalBasis { get; set; } = string.Empty;
    public DateTimeOffset TransferDate { get; set; }
    public string? Safeguards { get; set; }
}


public class ComplianceViolationDto
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public DateTimeOffset DetectedAt { get; set; }
    public string? Recommendation { get; set; }
    public bool IsFixed { get; set; }
    public DateTimeOffset? FixedAt { get; set; }
}

public class PrivacyComplianceReportDto
{
    public DateTimeOffset GeneratedAt { get; set; }
    public bool IsCompliant { get; set; }
    public int TotalViolations { get; set; }
    public int CriticalViolations { get; set; }
    public int HighViolations { get; set; }
    public int MediumViolations { get; set; }
    public int LowViolations { get; set; }
    public int ResolvedViolations { get; set; }
    public int PendingViolations { get; set; }
    public IEnumerable<ComplianceViolationDto> Violations { get; set; } = new List<ComplianceViolationDto>();
    public Dictionary<string, int> ViolationsByType { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, int> ViolationsBySeverity { get; set; } = new Dictionary<string, int>();
    public List<string> Recommendations { get; set; } = new List<string>();
}

public class DataMappingReportDto
{
    public DateTimeOffset GeneratedAt { get; set; }
    public int TotalDataCategories { get; set; }
    public int MappedCategories { get; set; }
    public int UnmappedCategories { get; set; }
    public int TotalDataLocations { get; set; }
    public int TotalProcessingActivities { get; set; }
    public int TotalDataTransfers { get; set; }
    public IEnumerable<DataMappingDto> Mappings { get; set; } = new List<DataMappingDto>();
    public IEnumerable<PersonalDataLocationDto> DataLocations { get; set; } = new List<PersonalDataLocationDto>();
    public IEnumerable<DataProcessingActivityDto> ProcessingActivities { get; set; } = new List<DataProcessingActivityDto>();
    public IEnumerable<DataTransferDto> DataTransfers { get; set; } = new List<DataTransferDto>();
}

public class PersonalDataLocationDto
{
    public string EntityType { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string? Value { get; set; }
    public DateTimeOffset? LastUpdated { get; set; }
    public string? Purpose { get; set; }
    public string? LegalBasis { get; set; }
    public string Location { get; set; } = string.Empty;
    public string DataCategories { get; set; } = string.Empty;
    public string? RetentionPeriod { get; set; }
    public string? AccessControls { get; set; }
    public string? EncryptionStatus { get; set; }
}

public class DataMappingDto
{
    public string DataCategory { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string? Purpose { get; set; }
    public string? LegalBasis { get; set; }
    public string? RetentionPeriod { get; set; }
    public bool IsMapped { get; set; }
}

public class UpdateDataMappingDto
{
    public Guid EntityId { get; set; }
    public string DataCategory { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string? Purpose { get; set; }
    public string? LegalBasis { get; set; }
    public string? RetentionPeriod { get; set; }
}
