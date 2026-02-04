using System;
using System.Collections.Generic;

namespace SaaSBase.Domain;

public class Organization : BaseEntity, ITenantEntity, IAggregateRoot
{
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
	public bool IsActive { get; set; } = true;

	public ICollection<Location> Locations { get; set; } = new List<Location>();
	public ICollection<BusinessSetting> BusinessSettings { get; set; } = new List<BusinessSetting>();
	public ICollection<Currency> Currencies { get; set; } = new List<Currency>();
	public ICollection<TaxRate> TaxRates { get; set; } = new List<TaxRate>();
	public ICollection<NotificationTemplate> NotificationTemplates { get; set; } = new List<NotificationTemplate>();
	public ICollection<IntegrationSetting> IntegrationSettings { get; set; } = new List<IntegrationSetting>();
}

public class Location : BaseEntity, ITenantEntity
{
	public Organization Organization { get; set; } = null!;
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
	public bool IsActive { get; set; } = true;
	public bool IsWarehouse { get; set; } = false;
	public bool IsRetail { get; set; } = false;
	public bool IsOffice { get; set; } = false;
	public decimal? Latitude { get; set; }
	public decimal? Longitude { get; set; }
	
	// Location hierarchy
	public Guid? ParentLocationId { get; set; }
	public Guid? ParentId { get; set; }
	public Location? ParentLocation { get; set; }
	public ICollection<Location> ChildLocations { get; set; } = new List<Location>();
	public int Level { get; set; } = 0;
	public string? LocationCode { get; set; }
	public string LocationType { get; set; } = "BRANCH"; // HEADQUARTERS, BRANCH, WAREHOUSE, RETAIL, OFFICE
	public int SortOrder { get; set; } = 0;
	
	// Location-specific settings
	public string? TimeZone { get; set; }
	public string? Currency { get; set; }
	public string? Language { get; set; }
	public bool IsDefault { get; set; } = false;
}

public class BusinessSetting : BaseEntity, ITenantEntity
{
	public Organization Organization { get; set; } = null!;
	public string Key { get; set; } = string.Empty;
	public string Value { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string DataType { get; set; } = "String"; // String, Number, Boolean, JSON
	public string? SettingKey { get; set; }
	public string? SettingValue { get; set; }
	public string? SettingType { get; set; }
	public bool IsActive { get; set; } = true;
}

public class TaxRate : BaseEntity, ITenantEntity
{
	public Organization Organization { get; set; } = null!;
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public decimal Rate { get; set; } // Percentage (e.g., 8.5 for 8.5%)
	public string? TaxType { get; set; } // Sales, VAT, GST, etc.
	public bool IsActive { get; set; } = true;
	public bool IsDefault { get; set; } = false;
	public DateTimeOffset? EffectiveFrom { get; set; }
	public DateTimeOffset? EffectiveTo { get; set; }
}

public class NotificationTemplate : BaseEntity, ITenantEntity
{
	public Organization Organization { get; set; } = null!;
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string TemplateType { get; set; } = string.Empty; // EMAIL, SMS, PUSH, IN_APP
	public string Subject { get; set; } = string.Empty;
	public string Body { get; set; } = string.Empty;
	public string? Variables { get; set; } // JSON array of available variables
	public bool IsActive { get; set; } = true;
	public bool IsSystemTemplate { get; set; } = false;
	public string? Category { get; set; } // ORDER, INVENTORY, USER, SYSTEM
}

public class IntegrationSetting : BaseEntity, ITenantEntity
{
	public Organization Organization { get; set; } = null!;
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string IntegrationType { get; set; } = string.Empty; // ERP, CRM, WMS, EMAIL, SMS, API
	public string Provider { get; set; } = string.Empty; // SAP, ORACLE, SALESFORCE, etc.
	public string? Configuration { get; set; } // JSON configuration
	public string? Credentials { get; set; } // Encrypted credentials
	public bool IsActive { get; set; } = true;
	public bool IsEnabled { get; set; } = false;
	public DateTimeOffset? LastSyncAt { get; set; }
	public string? LastSyncStatus { get; set; }
	public string? ErrorMessage { get; set; }
}

public class Currency : BaseEntity, ITenantEntity
{
	public Organization Organization { get; set; } = null!;
	public string Code { get; set; } = string.Empty; // USD, EUR, GBP, etc.
	public string Name { get; set; } = string.Empty; // US Dollar, Euro, etc.
	public string Symbol { get; set; } = string.Empty; // $, €, £, etc.
	public int DecimalPlaces { get; set; } = 2;
	public bool IsActive { get; set; } = true;
	public bool IsDefault { get; set; } = false;
	public decimal ExchangeRate { get; set; } = 1.0m; // Exchange rate relative to base currency
	public DateTimeOffset? LastUpdated { get; set; }
}