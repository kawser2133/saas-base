using System;
using SaaSBase.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SaaSBase.Infrastructure.Persistence;

public class PasswordPolicyConfig : IEntityTypeConfiguration<PasswordPolicy>
{
	public void Configure(EntityTypeBuilder<PasswordPolicy> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Id).ValueGeneratedOnAdd();
		builder.Property(x => x.OrganizationId).IsRequired();
		builder.Property(x => x.MinLength).IsRequired();
		builder.Property(x => x.MaxLength).IsRequired();
		builder.Property(x => x.RequireUppercase).IsRequired();
		builder.Property(x => x.RequireLowercase).IsRequired();
		builder.Property(x => x.RequireNumbers).IsRequired();
		builder.Property(x => x.RequireSpecialCharacters).IsRequired();
		builder.Property(x => x.MinSpecialCharacters).IsRequired();
		builder.Property(x => x.PreventCommonPasswords).IsRequired();
		builder.Property(x => x.PreventUserInfoInPassword).IsRequired();
		builder.Property(x => x.PasswordHistoryCount).IsRequired();
		builder.Property(x => x.MaxFailedAttempts).IsRequired();
		builder.Property(x => x.LockoutDurationMinutes).IsRequired();
		builder.Property(x => x.PasswordExpiryDays).IsRequired();
		builder.Property(x => x.RequirePasswordChangeOnFirstLogin).IsRequired();
		builder.Property(x => x.AllowedSpecialCharacters).HasMaxLength(200);
		builder.Property(x => x.DisallowedPasswords).HasMaxLength(1000);

		builder.HasIndex(x => x.OrganizationId)
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
	}
}

public class UserSessionConfig : IEntityTypeConfiguration<UserSession>
{
	public void Configure(EntityTypeBuilder<UserSession> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Id).ValueGeneratedOnAdd();
		builder.Property(x => x.OrganizationId).IsRequired();
		builder.Property(x => x.UserId).IsRequired();
		builder.Property(x => x.SessionId).IsRequired().HasMaxLength(100);
		builder.Property(x => x.DeviceId).IsRequired().HasMaxLength(100);
		builder.Property(x => x.DeviceName).IsRequired().HasMaxLength(200);
		builder.Property(x => x.DeviceType).IsRequired().HasMaxLength(50);
		builder.Property(x => x.BrowserName).IsRequired().HasMaxLength(100);
		builder.Property(x => x.BrowserVersion).IsRequired().HasMaxLength(50);
		builder.Property(x => x.OperatingSystem).IsRequired().HasMaxLength(100);
		builder.Property(x => x.IpAddress).IsRequired().HasMaxLength(45);
		builder.Property(x => x.UserAgent).IsRequired().HasMaxLength(500);
		builder.Property(x => x.Location).HasMaxLength(200);
		builder.Property(x => x.LastActivityAt).IsRequired();
		builder.Property(x => x.ExpiresAt).IsRequired();
		builder.Property(x => x.IsActive).IsRequired();
		builder.Property(x => x.Notes).HasMaxLength(1000);

		builder.HasOne(x => x.User)
			.WithMany(x => x.UserSessions)
			.HasForeignKey(x => x.UserId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => x.OrganizationId);
		builder.HasIndex(x => x.UserId);
		builder.HasIndex(x => x.SessionId);
		builder.HasIndex(x => x.DeviceId);
		builder.HasIndex(x => x.IsActive);
		builder.HasIndex(x => x.ExpiresAt);
	}
}

public class MfaSettingsConfig : IEntityTypeConfiguration<MfaSettings>
{
	public void Configure(EntityTypeBuilder<MfaSettings> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Id).ValueGeneratedOnAdd();
		builder.Property(x => x.OrganizationId).IsRequired();
		builder.Property(x => x.UserId).IsRequired();
		builder.Property(x => x.IsEnabled).IsRequired();
		builder.Property(x => x.MfaType).IsRequired().HasMaxLength(20);
		builder.Property(x => x.SecretKey).HasMaxLength(500);
		builder.Property(x => x.BackupCodes).HasMaxLength(2000);
		builder.Property(x => x.PhoneNumber).HasMaxLength(20);
		builder.Property(x => x.EmailAddress).HasMaxLength(200);
		builder.Property(x => x.FailedAttempts).IsRequired();
		builder.Property(x => x.LockedUntil);

		builder.HasOne(x => x.User)
			.WithMany(x => x.MfaSettings)
			.HasForeignKey(x => x.UserId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => x.OrganizationId);
		builder.HasIndex(x => x.UserId);
		builder.HasIndex(x => x.MfaType);
		builder.HasIndex(x => x.IsEnabled);
	}
}

public class MfaAttemptConfig : IEntityTypeConfiguration<MfaAttempt>
{
	public void Configure(EntityTypeBuilder<MfaAttempt> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Id).ValueGeneratedOnAdd();
		builder.Property(x => x.OrganizationId).IsRequired();
		builder.Property(x => x.UserId).IsRequired();
		builder.Property(x => x.MfaType).IsRequired().HasMaxLength(20);
		builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
		builder.Property(x => x.IsSuccessful).IsRequired();
		builder.Property(x => x.IpAddress).IsRequired().HasMaxLength(45);
		builder.Property(x => x.UserAgent).IsRequired().HasMaxLength(500);
		builder.Property(x => x.AttemptedAt).IsRequired();
		builder.Property(x => x.FailureReason).HasMaxLength(200);

		builder.HasOne(x => x.User)
			.WithMany()
			.HasForeignKey(x => x.UserId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => x.OrganizationId);
		builder.HasIndex(x => x.UserId);
		builder.HasIndex(x => x.MfaType);
		builder.HasIndex(x => x.IsSuccessful);
		builder.HasIndex(x => x.AttemptedAt);
	}
}

public class UserActivityLogConfig : IEntityTypeConfiguration<UserActivityLog>
{
	public void Configure(EntityTypeBuilder<UserActivityLog> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Id).ValueGeneratedOnAdd();
		builder.Property(x => x.OrganizationId).IsRequired();
		builder.Property(x => x.UserId).IsRequired();
		builder.Property(x => x.Action).IsRequired().HasMaxLength(100);
		builder.Property(x => x.Description).IsRequired().HasMaxLength(500);
		builder.Property(x => x.ResourceType).HasMaxLength(50);
		builder.Property(x => x.ResourceId);
		builder.Property(x => x.IpAddress).IsRequired().HasMaxLength(45);
		builder.Property(x => x.UserAgent).IsRequired().HasMaxLength(500);
		builder.Property(x => x.Location).HasMaxLength(200);
		builder.Property(x => x.Details).HasMaxLength(4000);
		builder.Property(x => x.Severity).IsRequired().HasMaxLength(20);
		builder.Property(x => x.Timestamp).IsRequired();

		builder.HasOne(x => x.User)
			.WithMany(x => x.ActivityLogs)
			.HasForeignKey(x => x.UserId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => x.OrganizationId);
		builder.HasIndex(x => x.UserId);
		builder.HasIndex(x => x.Action);
		builder.HasIndex(x => x.ResourceType);
		builder.HasIndex(x => x.Severity);
		builder.HasIndex(x => x.Timestamp);
	}
}

public class UserNotificationPreferencesConfig : IEntityTypeConfiguration<UserNotificationPreferences>
{
	public void Configure(EntityTypeBuilder<UserNotificationPreferences> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Id).ValueGeneratedOnAdd();
		builder.Property(x => x.OrganizationId).IsRequired();
		builder.Property(x => x.UserId).IsRequired();
		builder.Property(x => x.EmailNotifications).IsRequired();
		builder.Property(x => x.SmsNotifications).IsRequired();
		builder.Property(x => x.PushNotifications).IsRequired();
		builder.Property(x => x.InventoryAlerts).IsRequired();
		builder.Property(x => x.OrderNotifications).IsRequired();
		builder.Property(x => x.SystemNotifications).IsRequired();
		builder.Property(x => x.MarketingEmails).IsRequired();
		builder.Property(x => x.WeeklyReports).IsRequired();
		builder.Property(x => x.MonthlyReports).IsRequired();
		builder.Property(x => x.NotificationFrequency).IsRequired().HasMaxLength(20);
		builder.Property(x => x.PreferredNotificationTime).IsRequired().HasMaxLength(10);

		builder.HasOne(x => x.User)
			.WithOne(x => x.NotificationPreferences)
			.HasForeignKey<UserNotificationPreferences>(x => x.UserId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => x.OrganizationId);
		builder.HasIndex(x => x.UserId)
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
	}
}
