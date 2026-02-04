using System;

namespace SaaSBase.Domain;

public class UserNotificationPreferences : BaseEntity, ITenantEntity
{
	public Guid UserId { get; set; }
	public bool EmailNotifications { get; set; } = true;
	public bool SmsNotifications { get; set; } = false;
	public bool PushNotifications { get; set; } = true;
	public bool InventoryAlerts { get; set; } = true;
	public bool OrderNotifications { get; set; } = true;
	public bool SystemNotifications { get; set; } = true;
	public bool MarketingEmails { get; set; } = false;
	public bool SecurityAlerts { get; set; } = true;
	public bool SystemUpdates { get; set; } = true;
	public bool WeeklyReports { get; set; } = true;
	public bool MonthlyReports { get; set; } = true;
	public string NotificationFrequency { get; set; } = "IMMEDIATE"; // IMMEDIATE, DAILY, WEEKLY
	public string PreferredNotificationTime { get; set; } = "09:00"; // HH:MM format

	// Navigation properties
	public User User { get; set; } = null!;
}
