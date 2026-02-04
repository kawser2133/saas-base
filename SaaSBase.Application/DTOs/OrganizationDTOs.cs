using System;

namespace SaaSBase.Application.DTOs;

public class OrganizationDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? LogoUrl { get; set; }
	public string? Website { get; set; }
	public string? Email { get; set; }
	public string? Phone { get; set; }
	public string? Address { get; set; }
	public string? City { get; set; }
	public string? State { get; set; }
	public string? Country { get; set; }
	public string? PostalCode { get; set; }
	public string? TaxId { get; set; }
	public string? RegistrationNumber { get; set; }
	public string? PrimaryColor { get; set; }
	public string? SecondaryColor { get; set; }
	public bool IsActive { get; set; }
	public int LocationCount { get; set; }
}

public class CreateOrganizationDto
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? Email { get; set; }
	public string? Phone { get; set; }
	public string? Address { get; set; }
	public string? City { get; set; }
	public string? State { get; set; }
	public string? Country { get; set; }
	public string? PostalCode { get; set; }
	public string? TaxId { get; set; }
	public string? RegistrationNumber { get; set; }
}

public class UpdateOrganizationDto
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? Website { get; set; }
	public string? Email { get; set; }
	public string? Phone { get; set; }
	public string? Address { get; set; }
	public string? City { get; set; }
	public string? State { get; set; }
	public string? Country { get; set; }
	public string? PostalCode { get; set; }
	public string? TaxId { get; set; }
	public string? RegistrationNumber { get; set; }
	public string? PrimaryColor { get; set; }
	public string? SecondaryColor { get; set; }
	public bool? IsActive { get; set; }
}

public class LocationDto
{
	public Guid Id { get; set; }
	public Guid OrganizationId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? Address { get; set; }
	public string? City { get; set; }
	public string? State { get; set; }
	public string? Country { get; set; }
	public string? PostalCode { get; set; }
	public string? Phone { get; set; }
	public string? Email { get; set; }
	public string? ManagerName { get; set; }
	public bool IsActive { get; set; }
	public bool IsWarehouse { get; set; }
	public bool IsRetail { get; set; }
	public bool IsOffice { get; set; }
	public decimal? Latitude { get; set; }
	public decimal? Longitude { get; set; }
	
	// Hierarchy
	public Guid? ParentLocationId { get; set; }
	public int Level { get; set; }
	public string? LocationCode { get; set; }
	public string LocationType { get; set; } = "BRANCH";
	public int SortOrder { get; set; }
	
	// Settings
	public string? TimeZone { get; set; }
	public string? Currency { get; set; }
	public string? Language { get; set; }
	public bool IsDefault { get; set; }
	
	// Children
	public List<LocationDto> ChildLocations { get; set; } = new();
	
	// Metadata
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset? ModifiedAtUtc { get; set; }
	public string? CreatedBy { get; set; } // Keep for backward compatibility
	public string? ModifiedBy { get; set; } // Keep for backward compatibility
	public Guid? CreatedById { get; set; }
	public string? CreatedByName { get; set; }
	public Guid? ModifiedById { get; set; }
	public string? ModifiedByName { get; set; }
}

public class CreateLocationDto
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? Address { get; set; }
	public string? City { get; set; }
	public string? State { get; set; }
	public string? Country { get; set; }
	public string? PostalCode { get; set; }
	public string Phone { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public string? ManagerName { get; set; }
	public bool IsActive { get; set; } = true;
	public bool IsWarehouse { get; set; }
	public bool IsRetail { get; set; }
	public bool IsOffice { get; set; }
	public decimal? Latitude { get; set; }
	public decimal? Longitude { get; set; }
	public Guid? ParentLocationId { get; set; }
	public string? LocationCode { get; set; }
	public string LocationType { get; set; } = "BRANCH";
	public string? TimeZone { get; set; }
	public string? Currency { get; set; }
	public string? Language { get; set; }
	public bool IsDefault { get; set; }
}

public class UpdateLocationDto
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? Address { get; set; }
	public string? City { get; set; }
	public string? State { get; set; }
	public string? Country { get; set; }
	public string? PostalCode { get; set; }
	public string Phone { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public string? ManagerName { get; set; }
	public bool IsActive { get; set; }
	public bool IsWarehouse { get; set; }
	public bool IsRetail { get; set; }
	public bool IsOffice { get; set; }
	public decimal? Latitude { get; set; }
	public decimal? Longitude { get; set; }
	public Guid? ParentLocationId { get; set; }
	public string? LocationCode { get; set; }
	public string LocationType { get; set; } = "BRANCH";
	public string? TimeZone { get; set; }
	public string? Currency { get; set; }
	public string? Language { get; set; }
	public bool IsDefault { get; set; }
}

public class BusinessSettingDto
{
	public Guid Id { get; set; }
	public Guid OrganizationId { get; set; }
	public string Key { get; set; } = string.Empty;
	public string Value { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string DataType { get; set; } = "String";
	public string? SettingKey { get; set; }
	public string? SettingValue { get; set; }
	public string? SettingType { get; set; }
	public bool IsActive { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset? ModifiedAtUtc { get; set; }
	public string? CreatedBy { get; set; } // Keep for backward compatibility
	public string? ModifiedBy { get; set; } // Keep for backward compatibility
	public Guid? CreatedById { get; set; }
	public string? CreatedByName { get; set; }
	public Guid? ModifiedById { get; set; }
	public string? ModifiedByName { get; set; }
}

public class CreateBusinessSettingDto
{
	public string Key { get; set; } = string.Empty;
	public string Value { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? SettingKey { get; set; }
	public string? SettingValue { get; set; }
	public string? SettingType { get; set; }
	public string DataType { get; set; } = "String";
	public bool IsActive { get; set; } = true;
}

public class CurrencyDto
{
	public Guid Id { get; set; }
	public Guid OrganizationId { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Symbol { get; set; } = string.Empty;
	public decimal ExchangeRate { get; set; } = 1.0m;
	public int DecimalPlaces { get; set; }
	public bool IsActive { get; set; }
	public bool IsDefault { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset? ModifiedAtUtc { get; set; }
	public string? CreatedBy { get; set; } // Keep for backward compatibility
	public string? ModifiedBy { get; set; } // Keep for backward compatibility
	public Guid? CreatedById { get; set; }
	public string? CreatedByName { get; set; }
	public Guid? ModifiedById { get; set; }
	public string? ModifiedByName { get; set; }
}

public class CreateCurrencyDto
{
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Symbol { get; set; } = string.Empty;
	public decimal ExchangeRate { get; set; } = 1.0m;
	public int DecimalPlaces { get; set; } = 2;
	public bool IsActive { get; set; } = true;
	public bool IsDefault { get; set; }
}

public class TaxRateDto
{
	public Guid Id { get; set; }
	public Guid OrganizationId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public decimal Rate { get; set; }
	public string? TaxType { get; set; }
	public bool IsActive { get; set; }
	public bool IsDefault { get; set; }
	public DateTimeOffset? EffectiveFrom { get; set; }
	public DateTimeOffset? EffectiveTo { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset? ModifiedAtUtc { get; set; }
	public string? CreatedBy { get; set; } // Keep for backward compatibility
	public string? ModifiedBy { get; set; } // Keep for backward compatibility
	public Guid? CreatedById { get; set; }
	public string? CreatedByName { get; set; }
	public Guid? ModifiedById { get; set; }
	public string? ModifiedByName { get; set; }
}

public class CreateTaxRateDto
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public decimal Rate { get; set; }
	public string? TaxType { get; set; }
	public bool IsActive { get; set; } = true;
	public bool IsDefault { get; set; } = false;
	public DateTimeOffset? EffectiveFrom { get; set; }
	public DateTimeOffset? EffectiveTo { get; set; }
}

public class NotificationTemplateDto
{
	public Guid Id { get; set; }
	public Guid OrganizationId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string TemplateType { get; set; } = string.Empty;
	public string Subject { get; set; } = string.Empty;
	public string Body { get; set; } = string.Empty;
	public string? Variables { get; set; }
	public bool IsActive { get; set; }
	public bool IsSystemTemplate { get; set; }
	public string? Category { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset? ModifiedAtUtc { get; set; }
	public string? CreatedBy { get; set; } // Keep for backward compatibility
	public string? ModifiedBy { get; set; } // Keep for backward compatibility
	public Guid? CreatedById { get; set; }
	public string? CreatedByName { get; set; }
	public Guid? ModifiedById { get; set; }
	public string? ModifiedByName { get; set; }
}

public class CreateNotificationTemplateDto
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string TemplateType { get; set; } = string.Empty;
	public string Subject { get; set; } = string.Empty;
	public string Body { get; set; } = string.Empty;
	public string? Variables { get; set; }
	public string? Category { get; set; }
	public bool IsActive { get; set; } = true;
}

public class IntegrationSettingDto
{
	public Guid Id { get; set; }
	public Guid OrganizationId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string IntegrationType { get; set; } = string.Empty;
	public string Provider { get; set; } = string.Empty;
	public string? Configuration { get; set; }
	public bool IsActive { get; set; }
	public bool IsEnabled { get; set; }
	public DateTimeOffset? LastSyncAt { get; set; }
	public string? LastSyncStatus { get; set; }
	public string? ErrorMessage { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset? ModifiedAtUtc { get; set; }
	public string? CreatedBy { get; set; } // Keep for backward compatibility
	public string? ModifiedBy { get; set; } // Keep for backward compatibility
	public Guid? CreatedById { get; set; }
	public string? CreatedByName { get; set; }
	public Guid? ModifiedById { get; set; }
	public string? ModifiedByName { get; set; }
}

public class CreateIntegrationSettingDto
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string IntegrationType { get; set; } = string.Empty;
	public string Provider { get; set; } = string.Empty;
	public string? Configuration { get; set; }
	public bool IsActive { get; set; } = true;
	public bool IsEnabled { get; set; } = false;
}

public class UploadLogoDto
{
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public byte[] FileData { get; set; } = Array.Empty<byte>();
	public long FileSize { get; set; }
}

public class LocationHierarchyDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? LocationCode { get; set; }
	public string LocationType { get; set; } = string.Empty;
	public int Level { get; set; }
	public int SortOrder { get; set; }
	public bool IsActive { get; set; }
	public List<LocationHierarchyDto> Children { get; set; } = new();
}

public class OrganizationSummaryDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? LogoUrl { get; set; }
	public int LocationCount { get; set; }
	public int ActiveLocationCount { get; set; }
	public int CurrencyCount { get; set; }
	public int TaxRateCount { get; set; }
	public int IntegrationCount { get; set; }
	public int ActiveIntegrationCount { get; set; }
}

public class SetLocationStatusDto
{
	public bool IsActive { get; set; }
}

