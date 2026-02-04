using SaaSBase.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SaaSBase.Infrastructure.Persistence;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
	public void Configure(EntityTypeBuilder<Organization> builder)
	{
		builder.ToTable("organizations");
		builder.HasIndex(x => new { x.Name, x.OrganizationId })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
		builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
		builder.Property(x => x.Email).HasMaxLength(256);
		builder.Property(x => x.Phone).HasMaxLength(50);
		builder.Property(x => x.TaxId).HasMaxLength(100);
		builder.Property(x => x.RegistrationNumber).HasMaxLength(100);
		builder.Property(x => x.RowVersion).IsRowVersion();
	}
}

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
	public void Configure(EntityTypeBuilder<Location> builder)
	{
		builder.ToTable("locations");
		builder.HasIndex(x => new { x.Name, x.OrganizationId })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
		builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
		builder.Property(x => x.Email).HasMaxLength(256);
		builder.Property(x => x.Phone).HasMaxLength(50);
		builder.Property(x => x.ManagerName).HasMaxLength(256);
		builder.Property(x => x.LocationCode).HasMaxLength(50);
		builder.Property(x => x.LocationType).HasMaxLength(50);
		builder.Property(x => x.TimeZone).HasMaxLength(100);
		builder.Property(x => x.Currency).HasMaxLength(10);
		builder.Property(x => x.Language).HasMaxLength(10);
		builder.HasOne(x => x.Organization).WithMany(x => x.Locations).HasForeignKey(x => x.OrganizationId);
		builder.HasOne(x => x.ParentLocation).WithMany(x => x.ChildLocations).HasForeignKey(x => x.ParentLocationId);
		builder.Property(x => x.RowVersion).IsRowVersion();
	}
}

public class BusinessSettingConfiguration : IEntityTypeConfiguration<BusinessSetting>
{
	public void Configure(EntityTypeBuilder<BusinessSetting> builder)
	{
		builder.ToTable("business_settings");
		builder.HasIndex(x => new { x.Key, x.OrganizationId })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
		builder.Property(x => x.Key).IsRequired().HasMaxLength(256);
		builder.Property(x => x.Value).IsRequired().HasMaxLength(4000);
		builder.Property(x => x.DataType).IsRequired().HasMaxLength(50);
		builder.HasOne(x => x.Organization).WithMany(x => x.BusinessSettings).HasForeignKey(x => x.OrganizationId);
		builder.Property(x => x.RowVersion).IsRowVersion();
	}
}

public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
	public void Configure(EntityTypeBuilder<Currency> builder)
	{
		builder.ToTable("currencies");
		builder.HasIndex(x => new { x.Code, x.OrganizationId })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
		builder.Property(x => x.Code).IsRequired().HasMaxLength(3);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
		builder.Property(x => x.Symbol).IsRequired().HasMaxLength(10);
		builder.HasOne(x => x.Organization).WithMany(x => x.Currencies).HasForeignKey(x => x.OrganizationId);
		builder.Property(x => x.RowVersion).IsRowVersion();
	}
}

public class TaxRateConfiguration : IEntityTypeConfiguration<TaxRate>
{
	public void Configure(EntityTypeBuilder<TaxRate> builder)
	{
		builder.ToTable("tax_rates");
		builder.HasIndex(x => new { x.Name, x.OrganizationId })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
		builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
		builder.Property(x => x.TaxType).HasMaxLength(50);
		builder.Property(x => x.Rate).HasPrecision(18, 4);
		builder.HasOne(x => x.Organization).WithMany(x => x.TaxRates).HasForeignKey(x => x.OrganizationId);
		builder.Property(x => x.RowVersion).IsRowVersion();
	}
}

public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
	public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
	{
		builder.ToTable("notification_templates");
		builder.HasIndex(x => new { x.Name, x.OrganizationId })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
		builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
		builder.Property(x => x.TemplateType).IsRequired().HasMaxLength(50);
		builder.Property(x => x.Subject).IsRequired().HasMaxLength(500);
		builder.Property(x => x.Body).IsRequired().HasMaxLength(4000);
		builder.Property(x => x.Category).HasMaxLength(50);
		builder.HasOne(x => x.Organization).WithMany(x => x.NotificationTemplates).HasForeignKey(x => x.OrganizationId);
		builder.Property(x => x.RowVersion).IsRowVersion();
	}
}

public class IntegrationSettingConfiguration : IEntityTypeConfiguration<IntegrationSetting>
{
	public void Configure(EntityTypeBuilder<IntegrationSetting> builder)
	{
		builder.ToTable("integration_settings");
		builder.HasIndex(x => new { x.Name, x.OrganizationId })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
		builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
		builder.Property(x => x.IntegrationType).IsRequired().HasMaxLength(50);
		builder.Property(x => x.Provider).IsRequired().HasMaxLength(100);
		builder.Property(x => x.Configuration).HasMaxLength(4000);
		builder.Property(x => x.Credentials).HasMaxLength(4000);
		builder.Property(x => x.LastSyncStatus).HasMaxLength(50);
		builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
		builder.HasOne(x => x.Organization).WithMany(x => x.IntegrationSettings).HasForeignKey(x => x.OrganizationId);
		builder.Property(x => x.RowVersion).IsRowVersion();
	}
}
