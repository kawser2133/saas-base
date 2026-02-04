using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaaSBase.Application.Services;
using SaaSBase.Domain;
using BCrypt.Net;

namespace SaaSBase.Application.Implementations;

public class DemoDataService : IDemoDataService
{
	private readonly IUnitOfWork _unitOfWork;
	private readonly ICurrentTenantService _tenantService;
	private readonly ILogger<DemoDataService> _logger;

	public DemoDataService(IUnitOfWork unitOfWork, ICurrentTenantService tenantService, ILogger<DemoDataService> logger)
	{
		_unitOfWork = unitOfWork;
		_tenantService = tenantService;
		_logger = logger;
	}

	public async Task<bool> SeedDemoDataForOrganizationAsync(Guid organizationId)
	{
		try
		{
			// Set tenant context
			_tenantService.SetBackgroundContext(organizationId, Guid.Empty);

			// Seed Departments (must be first to get IDs for users)
			var departments = await SeedDepartmentsAsync(organizationId);

			// Seed Positions (must be second to get IDs for users)
			var positions = await SeedPositionsAsync(organizationId);

			// Seed Locations (must be third to get IDs for users)
			var locations = await SeedLocationsAsync(organizationId);

			// Seed Additional Roles (must be fourth to get IDs for users)
			var roles = await SeedAdditionalRolesAsync(organizationId);

			// Seed Organization Settings
			await SeedCurrenciesAsync(organizationId);
			await SeedTaxRatesAsync(organizationId);
			await SeedBusinessSettingsAsync(organizationId);
			await SeedNotificationTemplatesAsync(organizationId);
			await SeedIntegrationSettingsAsync(organizationId);

			// Save changes to get IDs
			await _unitOfWork.SaveChangesAsync();

			// Assign permissions to Administrator role (must be done before demo roles)
			await AssignPermissionsToAdministratorRoleAsync(organizationId);

			// Assign permissions to demo roles
			await AssignPermissionsToDemoRolesAsync(organizationId, roles);

			// Save permission assignments
			await _unitOfWork.SaveChangesAsync();

			// Seed Demo Users (after all other entities are saved)
			await SeedDemoUsersAsync(organizationId, departments, positions, locations, roles);

			// Save all changes
			await _unitOfWork.SaveChangesAsync();

			return true;
		}
		catch (Exception ex)
		{
			// Log error but don't fail registration
			_logger.LogError(ex, "Error seeding demo data for organization {OrganizationId}", organizationId);
			return false;
		}
	}

	private async Task<List<Department>> SeedDepartmentsAsync(Guid organizationId)
	{
		var deptRepo = _unitOfWork.Repository<Department>();
		
		// Check if departments already exist
		var existingDepts = await deptRepo.GetQueryable()
			.Where(d => d.OrganizationId == organizationId)
			.ToListAsync();
		
		if (existingDepts.Any())
			return existingDepts;

		var departments = new[]
		{
			new Department
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = "Information Technology",
				Description = "IT department responsible for technology infrastructure and support",
				Code = "IT",
				IsActive = true,
				SortOrder = 1,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			},
			new Department
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = "Operations",
				Description = "Operations department managing day-to-day business operations",
				Code = "OPS",
				IsActive = true,
				SortOrder = 2,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			},
			new Department
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = "Sales & Marketing",
				Description = "Sales and marketing department",
				Code = "SALES",
				IsActive = true,
				SortOrder = 3,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			},
			new Department
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = "Human Resources",
				Description = "HR department for employee management",
				Code = "HR",
				IsActive = true,
				SortOrder = 4,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			}
		};

		await deptRepo.AddRangeAsync(departments);
		return departments.ToList();
	}

	private async Task<List<Position>> SeedPositionsAsync(Guid organizationId)
	{
		var positionRepo = _unitOfWork.Repository<Position>();
		
		var existingPositions = await positionRepo.GetQueryable()
			.Where(p => p.OrganizationId == organizationId)
			.ToListAsync();
		
		if (existingPositions.Any())
			return existingPositions;

		var positions = new[]
		{
			new Position
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = "Software Engineer",
				Description = "Software development position",
				Code = "SE",
				IsActive = true,
				SortOrder = 1,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			},
			new Position
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = "Product Manager",
				Description = "Product management position",
				Code = "PM",
				IsActive = true,
				SortOrder = 2,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			},
			new Position
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = "Sales Representative",
				Description = "Sales position",
				Code = "SR",
				IsActive = true,
				SortOrder = 3,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			},
			new Position
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = "HR Manager",
				Description = "Human resources management position",
				Code = "HRM",
				IsActive = true,
				SortOrder = 4,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			}
		};

		await positionRepo.AddRangeAsync(positions);
		return positions.ToList();
	}

	private async Task<List<Location>> SeedLocationsAsync(Guid organizationId)
	{
		var locationRepo = _unitOfWork.Repository<Location>();
		
		var existingLocations = await locationRepo.GetQueryable()
			.Where(l => l.OrganizationId == organizationId)
			.ToListAsync();
		
		if (existingLocations.Any())
			return existingLocations;

		var locations = new[]
		{
			new Location
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = "Headquarters",
				Description = "Main office location",
				Address = "123 Business Street",
				City = "New York",
				State = "NY",
				Country = "USA",
				PostalCode = "10001",
				IsActive = true,
				IsOffice = true,
				IsDefault = true,
				LocationCode = "HQ",
				LocationType = "HEADQUARTERS",
				SortOrder = 1,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			},
			new Location
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = "Branch Office",
				Description = "Secondary office location",
				Address = "456 Commerce Avenue",
				City = "Los Angeles",
				State = "CA",
				Country = "USA",
				PostalCode = "90001",
				IsActive = true,
				IsOffice = true,
				IsDefault = false,
				LocationCode = "BR01",
				LocationType = "OFFICE",
				SortOrder = 2,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			}
		};

		await locationRepo.AddRangeAsync(locations);
		return locations.ToList();
	}

	private async Task<List<Role>> SeedAdditionalRolesAsync(Guid organizationId)
	{
		var roleRepo = _unitOfWork.Repository<Role>();
		
		// Get existing roles (including Administrator)
		var existingRoles = await roleRepo.GetQueryable()
			.Where(r => r.OrganizationId == organizationId)
			.ToListAsync();
		
		// If we already have more than Administrator, return existing custom roles
		if (existingRoles.Count > 1)
			return existingRoles.Where(r => r.Name != "Administrator").ToList();

		var roles = new[]
		{
			new Role
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = "Manager",
				Description = "Manager role with elevated permissions",
				RoleType = "CUSTOM",
				IsSystemRole = false,
				IsActive = true,
				SortOrder = 2,
				Color = "#0d6efd",
				Icon = "person-badge",
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			},
			new Role
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = "User",
				Description = "Standard user role",
				RoleType = "CUSTOM",
				IsSystemRole = false,
				IsActive = true,
				SortOrder = 3,
				Color = "#6c757d",
				Icon = "person",
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			}
		};

		await roleRepo.AddRangeAsync(roles);
		return roles.ToList();
	}

	private async Task SeedDemoUsersAsync(
		Guid organizationId,
		List<Department> departments,
		List<Position> positions,
		List<Location> locations,
		List<Role> roles)
	{
		var userRepo = _unitOfWork.Repository<User>();
		var userRoleRepo = _unitOfWork.Repository<UserRole>();
		var roleRepo = _unitOfWork.Repository<Role>();

		// Check if demo users already exist (excluding admin user)
		if (await userRepo.GetQueryable()
			.CountAsync(u => u.OrganizationId == organizationId && u.CreatedBy == "System" && u.Email != null && u.Email.Contains("demo")) > 0)
			return;

		// Get Administrator role for one user
		var adminRole = await roleRepo.GetQueryable()
			.FirstOrDefaultAsync(r => r.OrganizationId == organizationId && r.Name == "Administrator");
		
		// Get Manager and User roles
		var managerRole = roles.FirstOrDefault(r => r.Name == "Manager");
		var userRole = roles.FirstOrDefault(r => r.Name == "User");

		// Get department, position, and location references
		var itDept = departments.FirstOrDefault(d => d.Code == "IT");
		var salesDept = departments.FirstOrDefault(d => d.Code == "SALES");
		var hrDept = departments.FirstOrDefault(d => d.Code == "HR");
		var opsDept = departments.FirstOrDefault(d => d.Code == "OPS");

		var sePosition = positions.FirstOrDefault(p => p.Code == "SE");
		var pmPosition = positions.FirstOrDefault(p => p.Code == "PM");
		var srPosition = positions.FirstOrDefault(p => p.Code == "SR");
		var hrmPosition = positions.FirstOrDefault(p => p.Code == "HRM");

		var hqLocation = locations.FirstOrDefault(l => l.LocationCode == "HQ");
		var branchLocation = locations.FirstOrDefault(l => l.LocationCode == "BR01");

		// Default password for all demo users: Demo123!
		var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("Demo123!");

		var demoUsers = new[]
		{
			new User
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Email = "demo.manager@example.com",
				PasswordHash = defaultPasswordHash,
				FullName = "John Manager",
				FirstName = "John",
				LastName = "Manager",
				PhoneNumber = "+1-555-0101",
				JobTitle = pmPosition?.Name ?? "Product Manager",
				Department = itDept?.Name ?? "Information Technology",
				Location = hqLocation?.Name ?? "Headquarters",
				EmployeeId = "EMP001",
				IsActive = true,
				IsEmailVerified = true,
				TimeZone = "America/New_York",
				Language = "en",
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			},
			new User
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Email = "demo.engineer@example.com",
				PasswordHash = defaultPasswordHash,
				FullName = "Sarah Engineer",
				FirstName = "Sarah",
				LastName = "Engineer",
				PhoneNumber = "+1-555-0102",
				JobTitle = sePosition?.Name ?? "Software Engineer",
				Department = itDept?.Name ?? "Information Technology",
				Location = hqLocation?.Name ?? "Headquarters",
				EmployeeId = "EMP002",
				IsActive = true,
				IsEmailVerified = true,
				TimeZone = "America/New_York",
				Language = "en",
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			},
			new User
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Email = "demo.sales@example.com",
				PasswordHash = defaultPasswordHash,
				FullName = "Mike Sales",
				FirstName = "Mike",
				LastName = "Sales",
				PhoneNumber = "+1-555-0103",
				JobTitle = srPosition?.Name ?? "Sales Representative",
				Department = salesDept?.Name ?? "Sales & Marketing",
				Location = branchLocation?.Name ?? "Branch Office",
				EmployeeId = "EMP003",
				IsActive = true,
				IsEmailVerified = true,
				TimeZone = "America/Los_Angeles",
				Language = "en",
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			},
			new User
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Email = "demo.hr@example.com",
				PasswordHash = defaultPasswordHash,
				FullName = "Lisa HR",
				FirstName = "Lisa",
				LastName = "HR",
				PhoneNumber = "+1-555-0104",
				JobTitle = hrmPosition?.Name ?? "HR Manager",
				Department = hrDept?.Name ?? "Human Resources",
				Location = hqLocation?.Name ?? "Headquarters",
				EmployeeId = "EMP004",
				IsActive = true,
				IsEmailVerified = true,
				TimeZone = "America/New_York",
				Language = "en",
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			}
		};

		await userRepo.AddRangeAsync(demoUsers);
		await _unitOfWork.SaveChangesAsync(); // Save to get user IDs

		// Assign roles to users
		var userRoles = new List<UserRole>();

		// Manager role to first user
		if (managerRole != null && demoUsers.Length > 0)
		{
			userRoles.Add(new UserRole
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				UserId = demoUsers[0].Id,
				RoleId = managerRole.Id,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			});
		}

		// User role to remaining users
		if (userRole != null)
		{
			for (int i = 1; i < demoUsers.Length; i++)
			{
				userRoles.Add(new UserRole
				{
					Id = Guid.NewGuid(),
					OrganizationId = organizationId,
					UserId = demoUsers[i].Id,
					RoleId = userRole.Id,
					CreatedAtUtc = DateTimeOffset.UtcNow,
					CreatedBy = "System"
				});
			}
		}

		if (userRoles.Any())
		{
			await userRoleRepo.AddRangeAsync(userRoles);
		}
	}

	private async Task AssignPermissionsToAdministratorRoleAsync(Guid organizationId)
	{
		var roleRepo = _unitOfWork.Repository<Role>();
		var permissionRepo = _unitOfWork.Repository<Permission>();
		var rolePermissionRepo = _unitOfWork.Repository<RolePermission>();

		// Get Administrator role
		var adminRole = await roleRepo.GetQueryable()
			.FirstOrDefaultAsync(r => r.OrganizationId == organizationId && r.Name == "Administrator");

		if (adminRole == null)
			return; // Administrator role doesn't exist

		// Get all active permissions (permissions are system-wide)
		var systemOrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var allPermissions = await permissionRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(p => p.OrganizationId == systemOrganizationId && p.IsActive)
			.ToListAsync();

		// Company Admin permission codes (same as seed data)
		var adminPermissionCodes = new[]
		{
			// Dashboard
			"Dashboard.Read",
			// Users - Full access
			"Users.Read",
			"Users.Create",
			"Users.Update",
			"Users.Delete",
			"Users.Import",
			"Users.Export",
			// Roles - Full access
			"Roles.Read",
			"Roles.Create",
			"Roles.Update",
			"Roles.Delete",
			"Roles.Import",
			"Roles.Export",
			// Permissions - Read access for assigning permissions to roles
			"Permissions.Read",
			"Permissions.Update",
			// Departments - Full access
			"Departments.Read",
			"Departments.Create",
			"Departments.Update",
			"Departments.Delete",
			"Departments.Import",
			"Departments.Export",
			// Positions - Full access
			"Positions.Read",
			"Positions.Create",
			"Positions.Update",
			"Positions.Delete",
			"Positions.Import",
			"Positions.Export",
			// Sessions - Read and Delete
			"Sessions.Read",
			"Sessions.Delete",
			// MFA - Read and Update
			"Mfa.Read",
			"Mfa.Update",
			// Organizations - Read and Update
			"Organizations.Read",
			"Organizations.Update",
			// Organization Settings - Full access
			"Organizations.Locations.Read",
			"Organizations.Locations.Create",
			"Organizations.Locations.Update",
			"Organizations.Locations.Delete",
			"Organizations.Locations.Import",
			"Organizations.Locations.Export",
			"Organizations.BusinessSettings.Read",
			"Organizations.BusinessSettings.Create",
			"Organizations.BusinessSettings.Update",
			"Organizations.BusinessSettings.Delete",
			"Organizations.Currencies.Read",
			"Organizations.Currencies.Create",
			"Organizations.Currencies.Update",
			"Organizations.Currencies.Delete",
			"Organizations.Currencies.Import",
			"Organizations.Currencies.Export",
			"Organizations.TaxRates.Read",
			"Organizations.TaxRates.Create",
			"Organizations.TaxRates.Update",
			"Organizations.TaxRates.Delete",
			"Organizations.TaxRates.Import",
			"Organizations.TaxRates.Export",
			"Organizations.IntegrationSettings.Read",
			"Organizations.IntegrationSettings.Create",
			"Organizations.IntegrationSettings.Update",
			"Organizations.IntegrationSettings.Delete",
			"Organizations.IntegrationSettings.Import",
			"Organizations.IntegrationSettings.Export",
			"Organizations.NotificationTemplates.Read",
			"Organizations.NotificationTemplates.Create",
			"Organizations.NotificationTemplates.Update",
			"Organizations.NotificationTemplates.Delete",
			"Organizations.NotificationTemplates.Import",
			"Organizations.NotificationTemplates.Export",
			// Password Policy - Read access for self account settings
			"PasswordPolicy.Read"
		};

		// Assign permissions to Administrator role (only non-SystemAdminOnly permissions)
		var adminPermissions = allPermissions
			.Where(p => !p.IsSystemAdminOnly && adminPermissionCodes.Contains(p.Code))
			.Select(permission => new RolePermission
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				RoleId = adminRole.Id,
				PermissionId = permission.Id,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			}).ToList();

		if (adminPermissions.Any())
		{
			await rolePermissionRepo.AddRangeAsync(adminPermissions);
		}
	}

	private async Task AssignPermissionsToDemoRolesAsync(Guid organizationId, List<Role> roles)
	{
		var permissionRepo = _unitOfWork.Repository<Permission>();
		var rolePermissionRepo = _unitOfWork.Repository<RolePermission>();

		// Check if permissions already assigned
		var managerRole = roles.FirstOrDefault(r => r.Name == "Manager");
		var userRole = roles.FirstOrDefault(r => r.Name == "User");

		if (managerRole == null && userRole == null)
			return;

		// Check if permissions are already assigned
		if (managerRole != null)
		{
			var hasManagerPermissions = await rolePermissionRepo.GetQueryable()
				.AnyAsync(rp => rp.RoleId == managerRole.Id && rp.OrganizationId == organizationId);
			if (hasManagerPermissions)
				return; // Already assigned
		}

        // Get all active permissions (permissions are system-wide)
        // Filter out System Admin only permissions (only Company Admin permissions should be assigned)
        var systemOrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var allPermissions = await permissionRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(p => p.OrganizationId == systemOrganizationId && p.IsActive && !p.IsSystemAdminOnly)
            .ToListAsync();

		// If no permissions exist, skip assignment (permissions should be seeded separately)
		if (!allPermissions.Any())
			return;

		var rolePermissions = new List<RolePermission>();

		// Assign permissions to Manager role (more comprehensive access)
		if (managerRole != null)
		{
			// Manager gets read access to most things including organization settings
			var managerPermissionCodes = new[]
			{
				"Dashboard.Read",
				"Users.Read",
				"Roles.Read",
				"Permissions.Read",
				"Menus.Read",
				"Departments.Read",
				"Positions.Read",
				"Organizations.Read",
				// Organization Settings - Read access
				"Organizations.Locations.Read",
				"Organizations.BusinessSettings.Read",
				"Organizations.Currencies.Read",
				"Organizations.TaxRates.Read",
				"Organizations.IntegrationSettings.Read",
				"Organizations.NotificationTemplates.Read",
				// Password Policy - Read access for self account settings
				"PasswordPolicy.Read"
			};

			foreach (var code in managerPermissionCodes)
			{
				var permission = allPermissions.FirstOrDefault(p => p.Code == code);
				if (permission != null)
				{
					rolePermissions.Add(new RolePermission
					{
						Id = Guid.NewGuid(),
						OrganizationId = organizationId,
						RoleId = managerRole.Id,
						PermissionId = permission.Id,
						CreatedAtUtc = DateTimeOffset.UtcNow,
						CreatedBy = "System"
					});
				}
			}
		}

		// Assign permissions to User role (basic read access)
		if (userRole != null)
		{
			// User gets basic read access
			var userPermissionCodes = new[]
			{
				"Dashboard.Read",
				"Users.Read",
				"Departments.Read",
				"Positions.Read",
				"Locations.Read"
			};

			foreach (var code in userPermissionCodes)
			{
				var permission = allPermissions.FirstOrDefault(p => p.Code == code);
				if (permission != null)
				{
					rolePermissions.Add(new RolePermission
					{
						Id = Guid.NewGuid(),
						OrganizationId = organizationId,
						RoleId = userRole.Id,
						PermissionId = permission.Id,
						CreatedAtUtc = DateTimeOffset.UtcNow,
						CreatedBy = "System"
					});
				}
			}
		}

		if (rolePermissions.Any())
		{
			await rolePermissionRepo.AddRangeAsync(rolePermissions);
		}
	}

	private async Task SeedCurrenciesAsync(Guid organizationId)
	{
		var currencyRepo = _unitOfWork.Repository<Currency>();

		var existingCurrencies = await currencyRepo.GetQueryable()
			.Where(c => c.OrganizationId == organizationId)
			.ToListAsync();

		if (existingCurrencies.Any())
			return;

		var currencies = new[]
		{
			new Currency
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Code = "USD",
				Name = "US Dollar",
				Symbol = "$",
				DecimalPlaces = 2,
				IsActive = true,
				IsDefault = true,
				ExchangeRate = 1.0m,
				LastUpdated = DateTimeOffset.UtcNow,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			},
			new Currency
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Code = "EUR",
				Name = "Euro",
				Symbol = "€",
				DecimalPlaces = 2,
				IsActive = true,
				IsDefault = false,
				ExchangeRate = 0.85m,
				LastUpdated = DateTimeOffset.UtcNow,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			}
		};

		await currencyRepo.AddRangeAsync(currencies);
	}

	private async Task SeedTaxRatesAsync(Guid organizationId)
	{
		var taxRateRepo = _unitOfWork.Repository<TaxRate>();

		var existingTaxRates = await taxRateRepo.GetQueryable()
			.Where(t => t.OrganizationId == organizationId)
			.ToListAsync();

		if (existingTaxRates.Any())
			return;

		var taxRates = new[]
		{
			new TaxRate
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = "Standard Tax",
				Description = "Standard tax rate",
				Rate = 8.5m,
				TaxType = "Sales",
				IsActive = true,
				IsDefault = true,
				EffectiveFrom = DateTimeOffset.UtcNow,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			},
			new TaxRate
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = "Reduced Tax",
				Description = "Reduced tax rate for specific items",
				Rate = 5.0m,
				TaxType = "Sales",
				IsActive = true,
				IsDefault = false,
				EffectiveFrom = DateTimeOffset.UtcNow,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			}
		};

		await taxRateRepo.AddRangeAsync(taxRates);
	}

	private async Task SeedBusinessSettingsAsync(Guid organizationId)
	{
		var businessSettingRepo = _unitOfWork.Repository<BusinessSetting>();

		var existingSettings = await businessSettingRepo.GetQueryable()
			.Where(b => b.OrganizationId == organizationId)
			.ToListAsync();

		if (existingSettings.Any())
			return;

		var settings = new[]
		{
			new BusinessSetting
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Key = "CompanyName",
				Value = "Demo Company",
				Description = "Company display name",
				DataType = "String",
				IsActive = true,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			},
			new BusinessSetting
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Key = "DefaultCurrency",
				Value = "USD",
				Description = "Default currency for the organization",
				DataType = "String",
				IsActive = true,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			},
			new BusinessSetting
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Key = "TimeZone",
				Value = "America/New_York",
				Description = "Default timezone for the organization",
				DataType = "String",
				IsActive = true,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			}
		};

		await businessSettingRepo.AddRangeAsync(settings);
	}

	private async Task SeedNotificationTemplatesAsync(Guid organizationId)
	{
		var templateRepo = _unitOfWork.Repository<NotificationTemplate>();

		var existingTemplates = await templateRepo.GetQueryable()
			.Where(n => n.OrganizationId == organizationId)
			.ToListAsync();

		if (existingTemplates.Any())
			return;

		var templates = new[]
		{
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Welcome Email",
                Description = "Welcome email template for new users",
                TemplateType = "EMAIL",
                Subject = "Welcome to {{OrganizationName}}!",
                Body = "<p style='margin:0 0 12px;color:#374151'>Dear {{UserName}},</p><p style='margin:0 0 12px;color:#374151'>Welcome to {{OrganizationName}}! Your account has been created successfully.</p><p style='margin:16px 0'><a href='{{LoginUrl}}' style='display:inline-block;padding:12px 18px;background:#2563eb;color:#fff;border-radius:8px;text-decoration:none;font-weight:600'>Login to Your Account</a></p><p style='margin:12px 0;color:#6b7280'>If you have any questions, please contact us at {{OrganizationEmail}} or visit {{OrganizationWebsite}}.</p><p style='margin:12px 0;color:#374151'>Best regards,<br>{{OrganizationName}} Team</p>",
                Variables = "[\"UserName\", \"UserEmail\", \"LoginUrl\", \"OrganizationName\", \"OrganizationEmail\", \"OrganizationWebsite\"]",
                IsActive = true,
                IsSystemTemplate = true,
                Category = "USER",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Password Reset",
                Description = "Password reset email template",
                TemplateType = "EMAIL",
                Subject = "Password Reset Request - {{OrganizationName}}",
                Body = "<p style='margin:0 0 12px;color:#374151'>Dear {{UserName}},</p><p style='margin:0 0 12px;color:#374151'>You have requested to reset your password for your {{OrganizationName}} account.</p><p style='margin:16px 0'><a href='{{ResetUrl}}' style='display:inline-block;padding:12px 18px;background:#2563eb;color:#fff;border-radius:8px;text-decoration:none;font-weight:600'>Reset Password</a></p><p style='margin:12px 0;color:#6b7280'>If the button doesn't work, copy and paste this link into your browser:</p><p style='word-break:break-all;color:#2563eb;margin:12px 0'>{{ResetUrl}}</p><p style='margin:12px 0;color:#6b7280;font-size:12px'>This link will expire in {{ExpiryTime}}.</p><p style='margin:12px 0;color:#dc2626;font-size:12px'>If you did not request this password reset, please ignore this email or contact support at {{OrganizationEmail}}.</p><p style='margin:12px 0;color:#374151'>Best regards,<br>{{OrganizationName}} Team</p>",
                Variables = "[\"UserName\", \"UserEmail\", \"ResetUrl\", \"ExpiryTime\", \"OrganizationName\", \"OrganizationEmail\"]",
                IsActive = true,
                IsSystemTemplate = true,
                Category = "USER",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Email Verification",
                Description = "Email verification template for new user accounts",
                TemplateType = "EMAIL",
                Subject = "Verify Your Email - {{OrganizationName}}",
                Body = "<p style='margin:0 0 12px;color:#374151'>Dear {{UserName}},</p><p style='margin:0 0 12px;color:#374151'>Please verify your email address to activate your {{OrganizationName}} account.</p><p style='margin:16px 0'><a href='{{VerificationUrl}}' style='display:inline-block;padding:12px 18px;background:#2563eb;color:#fff;border-radius:8px;text-decoration:none;font-weight:600'>Verify Email</a></p><p style='margin:12px 0;color:#6b7280'>If the button doesn't work, copy and paste this link into your browser:</p><p style='word-break:break-all;color:#2563eb;margin:12px 0'>{{VerificationUrl}}</p><p style='margin:12px 0;color:#6b7280;font-size:12px'>This link will expire in 3 days.</p><p style='margin:12px 0;color:#374151'>Best regards,<br>{{OrganizationName}} Team</p>",
                Variables = "[\"UserName\", \"UserEmail\", \"VerificationUrl\", \"OrganizationName\", \"OrganizationEmail\"]",
                IsActive = true,
                IsSystemTemplate = true,
                Category = "USER",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "MFA Code",
                Description = "Multi-factor authentication code email template",
                TemplateType = "EMAIL",
                Subject = "Your MFA Verification Code - {{OrganizationName}}",
                Body = "<p style='margin:0 0 12px;color:#374151'>Dear {{UserName}},</p><p style='margin:0 0 12px;color:#374151'>Your verification code for {{OrganizationName}} is:</p><p style='margin:16px 0'><strong style='font-size:24px;color:#111827;letter-spacing:4px;padding:12px 24px;background:#f3f4f6;border-radius:8px;display:inline-block'>{{MfaCode}}</strong></p><p style='margin:12px 0;color:#6b7280'>This code will expire in 10 minutes.</p><p style='margin:12px 0;color:#dc2626;font-size:12px'>If you did not request this code, please contact support immediately at {{OrganizationEmail}}.</p><p style='margin:12px 0;color:#374151'>Best regards,<br>{{OrganizationName}} Team</p>",
                Variables = "[\"UserName\", \"UserEmail\", \"MfaCode\", \"OrganizationName\", \"OrganizationEmail\"]",
                IsActive = true,
                IsSystemTemplate = true,
                Category = "USER",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Account Password",
                Description = "Temporary account password email template",
                TemplateType = "EMAIL",
                Subject = "Your Account Password - {{OrganizationName}}",
                Body = "<p style='margin:0 0 12px;color:#374151'>Dear {{UserName}},</p><p style='margin:0 0 12px;color:#374151'>Your {{OrganizationName}} account has been activated. Here is your temporary password:</p><p style='margin:16px 0'><strong style='font-size:16px;color:#111827;padding:12px 24px;background:#f3f4f6;border-radius:8px;display:inline-block'>{{TemporaryPassword}}</strong></p><p style='margin:12px 0;color:#6b7280'>For security, please change your password after logging in.</p><p style='margin:16px 0'><a href='{{LoginUrl}}' style='display:inline-block;padding:12px 18px;background:#2563eb;color:#fff;border-radius:8px;text-decoration:none;font-weight:600'>Login to Your Account</a></p><p style='margin:12px 0;color:#374151'>Best regards,<br>{{OrganizationName}} Team</p>",
                Variables = "[\"UserName\", \"UserEmail\", \"TemporaryPassword\", \"LoginUrl\", \"OrganizationName\", \"OrganizationEmail\"]",
                IsActive = true,
                IsSystemTemplate = true,
                Category = "USER",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Invitation Email",
                Description = "User invitation email template",
                TemplateType = "EMAIL",
                Subject = "You're Invited to Join {{OrganizationName}}",
                Body = "<p style='margin:0 0 12px;color:#374151'>Dear {{UserName}},</p><p style='margin:0 0 12px;color:#374151'>You've been invited to join {{OrganizationName}}!</p><p style='margin:0 0 12px;color:#374151'>Click the button below to set up your account and get started.</p><p style='margin:16px 0'><a href='{{InvitationUrl}}' style='display:inline-block;padding:12px 18px;background:#2563eb;color:#fff;border-radius:8px;text-decoration:none;font-weight:600'>Setup Account</a></p><p style='margin:12px 0;color:#6b7280'>If the button doesn't work, copy and paste this link into your browser:</p><p style='word-break:break-all;color:#2563eb;margin:12px 0'>{{InvitationUrl}}</p><p style='margin:12px 0;color:#374151'>Best regards,<br>{{OrganizationName}} Team</p>",
                Variables = "[\"UserName\", \"UserEmail\", \"InvitationUrl\", \"OrganizationName\", \"OrganizationEmail\", \"OrganizationWebsite\"]",
                IsActive = true,
                IsSystemTemplate = true,
                Category = "USER",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Account Activation",
                Description = "Account activation notification email template",
                TemplateType = "EMAIL",
                Subject = "Your Account Has Been Activated - {{OrganizationName}}",
                Body = "<p style='margin:0 0 12px;color:#374151'>Dear {{UserName}},</p><p style='margin:0 0 12px;color:#374151'>Great news! Your {{OrganizationName}} account has been activated.</p><p style='margin:0 0 12px;color:#374151'>You can now log in and start using all the features available to you.</p><p style='margin:16px 0'><a href='{{LoginUrl}}' style='display:inline-block;padding:12px 18px;background:#2563eb;color:#fff;border-radius:8px;text-decoration:none;font-weight:600'>Login to Your Account</a></p><p style='margin:12px 0;color:#374151'>Best regards,<br>{{OrganizationName}} Team</p>",
                Variables = "[\"UserName\", \"UserEmail\", \"LoginUrl\", \"OrganizationName\", \"OrganizationEmail\"]",
                IsActive = true,
                IsSystemTemplate = true,
                Category = "USER",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Password Changed",
                Description = "Password changed confirmation email template",
                TemplateType = "EMAIL",
                Subject = "Your Password Has Been Changed - {{OrganizationName}}",
                Body = "<p style='margin:0 0 12px;color:#374151'>Dear {{UserName}},</p><p style='margin:0 0 12px;color:#374151'>This is to confirm that your password for your {{OrganizationName}} account has been successfully changed.</p><p style='margin:12px 0;color:#6b7280'>If you did not make this change, please contact support immediately at {{OrganizationEmail}}.</p><p style='margin:12px 0;color:#374151'>Best regards,<br>{{OrganizationName}} Team</p>",
                Variables = "[\"UserName\", \"UserEmail\", \"ChangedAt\", \"OrganizationName\", \"OrganizationEmail\"]",
                IsActive = true,
                IsSystemTemplate = true,
                Category = "USER",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Account Locked",
                Description = "Account locked notification email template",
                TemplateType = "EMAIL",
                Subject = "Your Account Has Been Locked - {{OrganizationName}}",
                Body = "<p style='margin:0 0 12px;color:#374151'>Dear {{UserName}},</p><p style='margin:0 0 12px;color:#dc2626'>Your {{OrganizationName}} account has been temporarily locked due to multiple failed login attempts.</p><p style='margin:0 0 12px;color:#374151'>For security reasons, your account will remain locked for {{LockDuration}}.</p><p style='margin:12px 0;color:#6b7280'>If you believe this is an error or need immediate assistance, please contact support at {{OrganizationEmail}}.</p><p style='margin:12px 0;color:#374151'>Best regards,<br>{{OrganizationName}} Team</p>",
                Variables = "[\"UserName\", \"UserEmail\", \"LockDuration\", \"LockedAt\", \"OrganizationName\", \"OrganizationEmail\"]",
                IsActive = true,
                IsSystemTemplate = true,
                Category = "USER",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Account Unlocked",
                Description = "Account unlocked notification email template",
                TemplateType = "EMAIL",
                Subject = "Your Account Has Been Unlocked - {{OrganizationName}}",
                Body = "<p style='margin:0 0 12px;color:#374151'>Dear {{UserName}},</p><p style='margin:0 0 12px;color:#059669'>Good news! Your {{OrganizationName}} account has been unlocked.</p><p style='margin:0 0 12px;color:#374151'>You can now log in to your account again.</p><p style='margin:16px 0'><a href='{{LoginUrl}}' style='display:inline-block;padding:12px 18px;background:#2563eb;color:#fff;border-radius:8px;text-decoration:none;font-weight:600'>Login to Your Account</a></p><p style='margin:12px 0;color:#374151'>Best regards,<br>{{OrganizationName}} Team</p>",
                Variables = "[\"UserName\", \"UserEmail\", \"LoginUrl\", \"UnlockedAt\", \"OrganizationName\", \"OrganizationEmail\"]",
                IsActive = true,
                IsSystemTemplate = true,
                Category = "USER",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Account Deactivated",
                Description = "Account deactivation notification email template",
                TemplateType = "EMAIL",
                Subject = "Your Account Has Been Deactivated - {{OrganizationName}}",
                Body = "<p style='margin:0 0 12px;color:#374151'>Dear {{UserName}},</p><p style='margin:0 0 12px;color:#dc2626'>Your {{OrganizationName}} account has been deactivated.</p><p style='margin:0 0 12px;color:#374151'>You will no longer be able to access the system with this account.</p><p style='margin:12px 0;color:#6b7280'>If you believe this is an error or need assistance, please contact support at {{OrganizationEmail}}.</p><p style='margin:12px 0;color:#374151'>Best regards,<br>{{OrganizationName}} Team</p>",
                Variables = "[\"UserName\", \"UserEmail\", \"DeactivatedAt\", \"OrganizationName\", \"OrganizationEmail\"]",
                IsActive = true,
                IsSystemTemplate = true,
                Category = "USER",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            }
		};

		await templateRepo.AddRangeAsync(templates);
	}

	private async Task SeedIntegrationSettingsAsync(Guid organizationId)
	{
		var integrationRepo = _unitOfWork.Repository<IntegrationSetting>();

		var existingIntegrations = await integrationRepo.GetQueryable()
			.Where(i => i.OrganizationId == organizationId)
			.ToListAsync();

		if (existingIntegrations.Any())
			return;

		var integrations = new[]
		{
			new IntegrationSetting
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = "SendGrid Email",
				Description = "Email service integration using SendGrid. IMPORTANT: Update Configuration JSON with your real SendGrid API key, FromEmail, and FromName values.",
				IntegrationType = "EMAIL",
				Provider = "SendGrid",
				Configuration = "{\"ApiKey\":\"YOUR_SENDGRID_API_KEY_HERE\",\"FromEmail\":\"noreply@demo.com\",\"FromName\":\"Demo Company\"}",
				Credentials = "encrypted_credentials_here",
				IsActive = true,
				IsEnabled = false, // Disabled by default - user must configure with real values first
				LastSyncAt = DateTimeOffset.UtcNow,
				LastSyncStatus = "Pending Configuration",
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			},
			new IntegrationSetting
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = "Twilio SMS",
				Description = "SMS service integration using Twilio",
				IntegrationType = "SMS",
				Provider = "Twilio",
				Configuration = "{\"AccountSid\":\"{{TWILIO_ACCOUNT_SID}}\",\"AuthToken\":\"{{TWILIO_AUTH_TOKEN}}\",\"FromNumber\":\"+1234567890\"}",
				Credentials = "encrypted_credentials_here",
				IsActive = true,
				IsEnabled = false, // Disabled by default
				LastSyncAt = DateTimeOffset.UtcNow,
				LastSyncStatus = "Pending Configuration",
				CreatedAtUtc = DateTimeOffset.UtcNow,
				CreatedBy = "System"
			}
		};

		await integrationRepo.AddRangeAsync(integrations);
	}
}
