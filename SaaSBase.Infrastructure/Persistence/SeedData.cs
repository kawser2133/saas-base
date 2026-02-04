using Microsoft.EntityFrameworkCore;
using SaaSBase.Domain;

namespace SaaSBase.Infrastructure.Persistence;

public static class SeedData
{
    public static async Task SeedAsync(AppDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        await SeedOrganizationsAsync(context);
        await SeedCurrenciesAsync(context);
        await SeedTaxRatesAsync(context);
        await SeedPasswordPoliciesAsync(context);
        await SeedRolesAsync(context);
        await SeedMenusAsync(context);
        await SeedPermissionsAsync(context);
        await SeedRolePermissionsAsync(context);
        await SeedUsersAsync(context);
        await SeedUserSessionsAsync(context);
        await SeedMfaSettingsAsync(context);
        await SeedLocationsAsync(context);
        await SeedDepartmentsAsync(context);
        await SeedPositionsAsync(context);
        await SeedBusinessSettingsAsync(context);
        await SeedNotificationTemplatesAsync(context);
        await SeedIntegrationSettingsAsync(context);

        await context.SaveChangesAsync();
    }

    private static async Task SeedOrganizationsAsync(AppDbContext context)
    {
        if (await context.Organizations.IgnoreQueryFilters().AnyAsync()) return;

        var org = new Organization
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "SaaS Base Corp",
            Description = "SaaS solutions provider",
            Website = "https://saasbase.com",
            Email = "info@saasbase.com",
            Phone = "+1-555-0123",
            Address = "123 Business Park Drive",
            City = "San Francisco",
            State = "CA",
            Country = "USA",
            PostalCode = "94105",
            TaxId = "12-3456789",
            RegistrationNumber = "ASC-2024-001",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        context.Organizations.Add(org);
    }

    private static async Task SeedCurrenciesAsync(AppDbContext context)
    {
        if (await context.Currencies.IgnoreQueryFilters().AnyAsync()) return;

        var currencies = new[]
        {
            new Currency
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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

        context.Currencies.AddRange(currencies);
    }

    private static async Task SeedTaxRatesAsync(AppDbContext context)
    {
        if (await context.TaxRates.IgnoreQueryFilters().AnyAsync()) return;

        var taxRates = new[]
        {
            new TaxRate
            {
                Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Standard Sales Tax",
                Description = "Standard sales tax rate for most products",
                Rate = 8.25m,
                TaxType = "Sales",
                IsActive = true,
                IsDefault = true,
                EffectiveFrom = DateTimeOffset.UtcNow.AddYears(-1),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            }
        };

        context.TaxRates.AddRange(taxRates);
    }

    private static async Task SeedPasswordPoliciesAsync(AppDbContext context)
    {
        if (await context.PasswordPolicies.IgnoreQueryFilters().AnyAsync()) return;

        var passwordPolicy = new PasswordPolicy
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            MinLength = 8,
            MaxLength = 128,
            RequireUppercase = true,
            RequireLowercase = true,
            RequireNumbers = true,
            RequireSpecialCharacters = true,
            MinSpecialCharacters = 1,
            PasswordHistoryCount = 5,
            PasswordExpiryDays = 90,
            MaxFailedAttempts = 5,
            LockoutDurationMinutes = 30,
            PreventCommonPasswords = true,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        context.PasswordPolicies.Add(passwordPolicy);
    }

    private static async Task SeedRolesAsync(AppDbContext context)
    {
        if (await context.Roles.IgnoreQueryFilters().AnyAsync()) return;

        var roles = new[]
        {
            new Role
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "System Administrator",
                Description = "Full system access with all permissions",
                RoleType = "SYSTEM",
                IsSystemRole = true,
                IsActive = true,
                SortOrder = 1,
                Color = "#dc3545",
                Icon = "shield-check",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Role
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Administrator",
                Description = "Organization administrator with full access",
                RoleType = "SYSTEM",
                IsSystemRole = true,
                IsActive = true,
                SortOrder = 2,
                Color = "#6f42c1",
                Icon = "person-gear",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Role
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Manager",
                Description = "Department manager with limited administrative access",
                RoleType = "CUSTOM",
                IsSystemRole = false,
                IsActive = true,
                SortOrder = 3,
                Color = "#0d6efd",
                Icon = "person-badge",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Role
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Product Manager",
                Description = "Product catalog and category management",
                RoleType = "CUSTOM",
                IsSystemRole = false,
                IsActive = true,
                SortOrder = 4,
                Color = "#198754",
                Icon = "box-seam",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Role
            {
                Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "User",
                Description = "Standard user with basic access",
                RoleType = "CUSTOM",
                IsSystemRole = false,
                IsActive = true,
                SortOrder = 5,
                Color = "#6c757d",
                Icon = "person",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            }
        };

        context.Roles.AddRange(roles);
    }

    private static async Task SeedMenusAsync(AppDbContext context)
    {
        var dashboardManagementMenu = new Menu
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111102"),
            OrganizationId = Guid.Empty,
            Label = "System Dashboard",
            Route = "/system/dashboard",
            Icon = "fas fa-chart-pie",
            Section = "DASHBOARD",
            ParentMenuId = Guid.Parse("11111111-1111-1111-1111-111111111100"), // Reference to overviewMenu
            SortOrder = 1,
            IsActive = true,
            IsSystemMenu = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        var dashboardMenu = new Menu
        {
            Id = Guid.Parse("11111111-1111-1111-1112-111111111100"),
            OrganizationId = Guid.Empty,
            Label = "Dashboard",
            Route = "/dashboard",
            Icon = "fas fa-chart-pie",
            Section = "DASHBOARD",
            ParentMenuId = Guid.Parse("11111111-1111-1111-1111-111111111100"),
            SortOrder = 1,
            IsActive = true,
            IsSystemMenu = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        var overviewMenu = new Menu
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111100"),
            OrganizationId = Guid.Empty,
            Label = "Overview",
            Route = "/dashboard",
            Icon = "fas fa-tachometer-alt",
            Section = "DASHBOARD",
            ParentMenuId = null,
            SortOrder = 1,
            IsActive = true,
            IsSystemMenu = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };
        
        var masterDataMenu = new Menu
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111180"),
            OrganizationId = Guid.Empty,
            Label = "Master Data",
            Route = "/organizations",
            Icon = "fas fa-database",
            Section = "SYSTEM ADMIN",
            ParentMenuId = null,
            SortOrder = 1,
            IsActive = true,
            IsSystemMenu = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        var usersMenu = new Menu
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111191"),
            OrganizationId = Guid.Empty,
            Label = "Users",
            Route = "/auth/users",
            Icon = "fas fa-users",
            Section = "SYSTEM ADMIN",
            ParentMenuId = Guid.Parse("11111111-1111-1111-1111-111111111190"),
            SortOrder = 1,
            IsActive = true,
            IsSystemMenu = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        var rolesMenu = new Menu
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111193"),
            OrganizationId = Guid.Empty,
            Label = "Roles",
            Route = "/auth/roles",
            Icon = "fas fa-user-tag",
            Section = "SYSTEM ADMIN",
            ParentMenuId = Guid.Parse("11111111-1111-1111-1111-111111111192"),
            SortOrder = 1,
            IsActive = true,
            IsSystemMenu = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        var userManagementMenu = new Menu
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111190"),
            OrganizationId = Guid.Empty,
            Label = "User Management",
            Route = "/auth/users",
            Icon = "fas fa-users-cog",
            Section = "SYSTEM ADMIN",
            ParentMenuId = null,
            SortOrder = 2,
            IsActive = true,
            IsSystemMenu = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        var permissionsMenu = new Menu
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111194"),
            OrganizationId = Guid.Empty,
            Label = "Permissions",
            Route = "/auth/permissions",
            Icon = "fas fa-lock",
            Section = "SYSTEM ADMIN",
            ParentMenuId = Guid.Parse("11111111-1111-1111-1111-111111111192"),
            SortOrder = 2,
            IsActive = true,
            IsSystemMenu = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        var menusMenu = new Menu
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111197"),
            OrganizationId = Guid.Empty,
            Label = "Menus",
            Route = "/auth/menus",
            Icon = "fas fa-bars",
            Section = "SYSTEM ADMIN",
            ParentMenuId = Guid.Parse("11111111-1111-1111-1111-111111111192"), // Roles & Permissions
            SortOrder = 3,
            IsActive = true,
            IsSystemMenu = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        var sessionsMenu = new Menu
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111195"),
            OrganizationId = Guid.Empty,
            Label = "Sessions",
            Route = "/auth/sessions",
            Icon = "fas fa-clock",
            Section = "SYSTEM ADMIN",
            ParentMenuId = Guid.Parse("11111111-1111-1111-1111-111111111190"),
            SortOrder = 2,
            IsActive = true,
            IsSystemMenu = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        var rolesPermissionsMenu = new Menu
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111192"),
            OrganizationId = Guid.Empty,
            Label = "Roles & Permissions",
            Route = "/auth/roles",
            Icon = "fas fa-user-shield",
            Section = "SYSTEM ADMIN",
            ParentMenuId = null,
            SortOrder = 3,
            IsActive = true,
            IsSystemMenu = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        var passwordPolicyMenu = new Menu
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111198"),
            OrganizationId = Guid.Empty,
            Label = "Password Policy",
            Route = "/auth/password-policy",
            Icon = "fas fa-key",
            Section = "SYSTEM ADMIN",
            ParentMenuId = Guid.Parse("11111111-1111-1111-1111-111111111190"),
            SortOrder = 4,
            IsActive = true,
            IsSystemMenu = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        var mfaMenu = new Menu
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111196"),
            OrganizationId = Guid.Empty,
            Label = "MFA Settings",
            Route = "/auth/mfa",
            Icon = "fas fa-shield-alt",
            Section = "SYSTEM ADMIN",
            ParentMenuId = Guid.Parse("11111111-1111-1111-1111-111111111190"),
            SortOrder = 3,
            IsActive = true,
            IsSystemMenu = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        var organizationsMenu = new Menu
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111184"),
            OrganizationId = Guid.Empty,
            Label = "Organization",
            Route = "/organizations",
            Icon = "fas fa-building",
            Section = "SYSTEM ADMIN",
            ParentMenuId = Guid.Parse("11111111-1111-1111-1111-111111111180"),
            SortOrder = 4,
            IsActive = true,
            IsSystemMenu = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        var departmentsMenu = new Menu
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111185"),
            OrganizationId = Guid.Empty,
            Label = "Departments",
            Route = "/master-data/departments",
            Icon = "fas fa-sitemap",
            Section = "SYSTEM ADMIN",
            ParentMenuId = Guid.Parse("11111111-1111-1111-1111-111111111180"),
            SortOrder = 5,
            IsActive = true,
            IsSystemMenu = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        var positionsMenu = new Menu
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111186"),
            OrganizationId = Guid.Empty,
            Label = "Positions",
            Route = "/master-data/positions",
            Icon = "fas fa-briefcase",
            Section = "SYSTEM ADMIN",
            ParentMenuId = Guid.Parse("11111111-1111-1111-1111-111111111180"),
            SortOrder = 6,
            IsActive = true,
            IsSystemMenu = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        // System Dashboard - for System Admin to monitor all organizations
        var systemDashboardMenu = new Menu
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111199"),
            OrganizationId = Guid.Empty,
            Label = "Organization Management",
            Route = "/system/organizations",
            Icon = "fas fa-globe",
            Section = "SYSTEM ADMIN",
            ParentMenuId = null,
            SortOrder = 0,
            IsActive = true,
            IsSystemMenu = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        // All Organizations - System Admin view of all organizations
        var allOrganizationsMenu = new Menu
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111200"),
            OrganizationId = Guid.Empty,
            Label = "All Organizations",
            Route = "/system/organizations",
            Icon = "fas fa-building",
            Section = "SYSTEM ADMIN",
            ParentMenuId = Guid.Parse("11111111-1111-1111-1111-111111111199"),
            SortOrder = 1,
            IsActive = true,
            IsSystemMenu = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        // Order menus so parent menus are added before their children
        var menus = new[]
        {
            // Parent menus (no ParentMenuId)
            overviewMenu,
            systemDashboardMenu, // System Dashboard - highest priority for System Admin
            dashboardMenu,
            masterDataMenu,
            userManagementMenu,
            rolesPermissionsMenu,
            // Child menus (with ParentMenuId)
            allOrganizationsMenu, // Parent: systemDashboardMenu
            dashboardManagementMenu, // Parent: overviewMenu
            organizationsMenu, // Parent: masterDataMenu
            departmentsMenu, // Parent: masterDataMenu
            positionsMenu, // Parent: masterDataMenu
            usersMenu, // Parent: userManagementMenu
            sessionsMenu, // Parent: userManagementMenu
            mfaMenu, // Parent: userManagementMenu
            passwordPolicyMenu, // Parent: userManagementMenu
            rolesMenu, // Parent: rolesPermissionsMenu
            permissionsMenu, // Parent: rolesPermissionsMenu
            menusMenu // Parent: rolesPermissionsMenu
        };

        // Add menus individually, checking if they already exist to avoid tracking conflicts
        foreach (var menu in menus)
        {
            // Check if menu exists using AsNoTracking to avoid tracking conflicts
            var exists = await context.Menus
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(m => m.Id == menu.Id);
            
            if (!exists)
            {
                context.Menus.Add(menu);
            }
            // If menu exists, skip it (seeding should be idempotent)
        }
    }

    private static async Task SeedPermissionsAsync(AppDbContext context)
    {
        if (await context.Permissions.IgnoreQueryFilters().AnyAsync()) return;

        // System organization ID for system-wide permissions
        var systemOrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var permissions = new[]
        {            
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000060"),
                OrganizationId = systemOrganizationId,
                Code = "Dashboard.Read",
                Name = "Read Dashboard",
                Description = "View overview dashboard",
                Module = "Dashboard",
                Action = "Read",
                Resource = "Dashboard",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 11,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1112-111111111100"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
                        
            new Permission
            {
                Id = Guid.Parse("6764d2aa-2841-484f-97f2-3a905d6362f6"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Mfa.Read",
                Name = "Read MFA Settings",
                Description = "Read MFA settings",
                Module = "MFA",
                Action = "Read",
                Resource = "MfaSettings",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 71,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111196"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000005a"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.BusinessSettings.Create",
                Name = "Create Business Settings",
                Description = "Create organization business settings",
                Module = "Organizations",
                Action = "Create",
                Resource = "BusinessSettings",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 19,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000005c"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.BusinessSettings.Delete",
                Name = "Delete Business Settings",
                Description = "Delete organization business settings",
                Module = "Organizations",
                Action = "Delete",
                Resource = "BusinessSettings",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 21,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000059"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.BusinessSettings.Read",
                Name = "Read Business Settings",
                Description = "View organization business settings",
                Module = "Organizations",
                Action = "Read",
                Resource = "BusinessSettings",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 18,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000005b"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.BusinessSettings.Update",
                Name = "Update Business Settings",
                Description = "Update organization business settings",
                Module = "Organizations",
                Action = "Update",
                Resource = "BusinessSettings",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 20,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000052"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.Create",
                Name = "Create Organizations",
                Description = "Create new organizations",
                Module = "Organizations",
                Action = "Create",
                Resource = "Organizations",
                IsSystemPermission = true,
                IsSystemAdminOnly = true, // Company Admin accessible
                IsActive = true,
                SortOrder = 9,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000005e"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.Currencies.Create",
                Name = "Create Currencies",
                Description = "Create organization currencies",
                Module = "Organizations",
                Action = "Create",
                Resource = "Currencies",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 23,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000200"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.Currencies.Delete",
                Name = "Delete Currencies",
                Description = "Delete organization currencies",
                Module = "Organizations",
                Action = "Delete",
                Resource = "Currencies",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 25,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000210"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.Currencies.Export",
                Name = "Export Currencies",
                Description = "Export organization currencies",
                Module = "Organizations",
                Action = "Export",
                Resource = "Currencies",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 27,
                Category = "Export",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000020f"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.Currencies.Import",
                Name = "Import Currencies",
                Description = "Import organization currencies",
                Module = "Organizations",
                Action = "Import",
                Resource = "Currencies",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 26,
                Category = "Import",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000005d"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.Currencies.Read",
                Name = "Read Currencies",
                Description = "View organization currencies",
                Module = "Organizations",
                Action = "Read",
                Resource = "Currencies",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 22,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000005f"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.Currencies.Update",
                Name = "Update Currencies",
                Description = "Update organization currencies",
                Module = "Organizations",
                Action = "Update",
                Resource = "Currencies",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 24,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000054"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.Delete",
                Name = "Delete Organizations",
                Description = "Delete organizations",
                Module = "Organizations",
                Action = "Delete",
                Resource = "Organizations",
                IsSystemPermission = true,
                IsSystemAdminOnly = true, // Company Admin accessible
                IsActive = true,
                SortOrder = 11,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000020a"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.IntegrationSettings.Create",
                Name = "Create Integration Settings",
                Description = "Create organization integration settings",
                Module = "Organizations",
                Action = "Create",
                Resource = "IntegrationSettings",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 41,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000020c"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.IntegrationSettings.Delete",
                Name = "Delete Integration Settings",
                Description = "Delete organization integration settings",
                Module = "Organizations",
                Action = "Delete",
                Resource = "IntegrationSettings",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 43,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000216"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.IntegrationSettings.Export",
                Name = "Export Integration Settings",
                Description = "Export organization integration settings",
                Module = "Organizations",
                Action = "Export",
                Resource = "IntegrationSettings",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 45,
                Category = "Export",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000215"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.IntegrationSettings.Import",
                Name = "Import Integration Settings",
                Description = "Import organization integration settings",
                Module = "Organizations",
                Action = "Import",
                Resource = "IntegrationSettings",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 44,
                Category = "Import",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000209"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.IntegrationSettings.Read",
                Name = "Read Integration Settings",
                Description = "View organization integration settings",
                Module = "Organizations",
                Action = "Read",
                Resource = "IntegrationSettings",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 40,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000020b"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.IntegrationSettings.Update",
                Name = "Update Integration Settings",
                Description = "Update organization integration settings",
                Module = "Organizations",
                Action = "Update",
                Resource = "IntegrationSettings",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 42,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000056"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.Locations.Create",
                Name = "Create Locations",
                Description = "Create new organization locations",
                Module = "Organizations",
                Action = "Create",
                Resource = "Locations",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 13,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000058"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.Locations.Delete",
                Name = "Delete Locations",
                Description = "Delete organization locations",
                Module = "Organizations",
                Action = "Delete",
                Resource = "Locations",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 15,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000020e"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.Locations.Export",
                Name = "Export Locations",
                Description = "Export organization locations",
                Module = "Organizations",
                Action = "Export",
                Resource = "Locations",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 17,
                Category = "Export",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000020d"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.Locations.Import",
                Name = "Import Locations",
                Description = "Import organization locations",
                Module = "Organizations",
                Action = "Import",
                Resource = "Locations",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 16,
                Category = "Import",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000055"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.Locations.Read",
                Name = "Read Locations",
                Description = "View organization locations",
                Module = "Organizations",
                Action = "Read",
                Resource = "Locations",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 12,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000057"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.Locations.Update",
                Name = "Update Locations",
                Description = "Update organization locations",
                Module = "Organizations",
                Action = "Update",
                Resource = "Locations",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 14,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000206"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.NotificationTemplates.Create",
                Name = "Create Notification Templates",
                Description = "Create organization notification templates",
                Module = "Organizations",
                Action = "Create",
                Resource = "NotificationTemplates",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 35,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000208"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.NotificationTemplates.Delete",
                Name = "Delete Notification Templates",
                Description = "Delete organization notification templates",
                Module = "Organizations",
                Action = "Delete",
                Resource = "NotificationTemplates",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 37,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000214"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.NotificationTemplates.Export",
                Name = "Export Notification Templates",
                Description = "Export organization notification templates",
                Module = "Organizations",
                Action = "Export",
                Resource = "NotificationTemplates",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 39,
                Category = "Export",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000213"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.NotificationTemplates.Import",
                Name = "Import Notification Templates",
                Description = "Import organization notification templates",
                Module = "Organizations",
                Action = "Import",
                Resource = "NotificationTemplates",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 38,
                Category = "Import",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000205"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.NotificationTemplates.Read",
                Name = "Read Notification Templates",
                Description = "View organization notification templates",
                Module = "Organizations",
                Action = "Read",
                Resource = "NotificationTemplates",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 34,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000207"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.NotificationTemplates.Update",
                Name = "Update Notification Templates",
                Description = "Update organization notification templates",
                Module = "Organizations",
                Action = "Update",
                Resource = "NotificationTemplates",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 36,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000023"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.Read",
                Name = "Read Organizations",
                Description = "View organization information",
                Module = "Organizations",
                Action = "Read",
                Resource = "Organizations",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 7,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000202"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.TaxRates.Create",
                Name = "Create Tax Rates",
                Description = "Create organization tax rates",
                Module = "Organizations",
                Action = "Create",
                Resource = "TaxRates",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 29,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000204"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.TaxRates.Delete",
                Name = "Delete Tax Rates",
                Description = "Delete organization tax rates",
                Module = "Organizations",
                Action = "Delete",
                Resource = "TaxRates",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 31,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000212"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.TaxRates.Export",
                Name = "Export Tax Rates",
                Description = "Export organization tax rates",
                Module = "Organizations",
                Action = "Export",
                Resource = "TaxRates",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 33,
                Category = "Export",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000211"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.TaxRates.Import",
                Name = "Import Tax Rates",
                Description = "Import organization tax rates",
                Module = "Organizations",
                Action = "Import",
                Resource = "TaxRates",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 32,
                Category = "Import",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000201"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.TaxRates.Read",
                Name = "Read Tax Rates",
                Description = "View organization tax rates",
                Module = "Organizations",
                Action = "Read",
                Resource = "TaxRates",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 28,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000203"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.TaxRates.Update",
                Name = "Update Tax Rates",
                Description = "Update organization tax rates",
                Module = "Organizations",
                Action = "Update",
                Resource = "TaxRates",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 30,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000053"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Organizations.Update",
                Name = "Update Organizations",
                Description = "Update existing organizations",
                Module = "Organizations",
                Action = "Update",
                Resource = "Organizations",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 10,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111184"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            // Departments Permissions
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000217"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Departments.Create",
                Name = "Create Departments",
                Description = "Create new departments",
                Module = "Departments",
                Action = "Create",
                Resource = "Departments",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 46,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111185"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000218"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Departments.Read",
                Name = "Read Departments",
                Description = "View departments",
                Module = "Departments",
                Action = "Read",
                Resource = "Departments",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 45,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111185"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000219"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Departments.Update",
                Name = "Update Departments",
                Description = "Update existing departments",
                Module = "Departments",
                Action = "Update",
                Resource = "Departments",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 47,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111185"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000220"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Departments.Delete",
                Name = "Delete Departments",
                Description = "Delete departments",
                Module = "Departments",
                Action = "Delete",
                Resource = "Departments",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 48,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111185"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000221"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Departments.Import",
                Name = "Import Departments",
                Description = "Import departments data",
                Module = "Departments",
                Action = "Import",
                Resource = "Departments",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 49,
                Category = "Import",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111185"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000222"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Departments.Export",
                Name = "Export Departments",
                Description = "Export departments data",
                Module = "Departments",
                Action = "Export",
                Resource = "Departments",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 50,
                Category = "Export",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111185"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            // Positions Permissions
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000223"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Positions.Create",
                Name = "Create Positions",
                Description = "Create new positions",
                Module = "Positions",
                Action = "Create",
                Resource = "Positions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 52,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111186"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000224"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Positions.Read",
                Name = "Read Positions",
                Description = "View positions",
                Module = "Positions",
                Action = "Read",
                Resource = "Positions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 51,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111186"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000225"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Positions.Update",
                Name = "Update Positions",
                Description = "Update existing positions",
                Module = "Positions",
                Action = "Update",
                Resource = "Positions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 53,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111186"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000226"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Positions.Delete",
                Name = "Delete Positions",
                Description = "Delete positions",
                Module = "Positions",
                Action = "Delete",
                Resource = "Positions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 54,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111186"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000227"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Positions.Import",
                Name = "Import Positions",
                Description = "Import positions data",
                Module = "Positions",
                Action = "Import",
                Resource = "Positions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 55,
                Category = "Import",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111186"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000228"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Positions.Export",
                Name = "Export Positions",
                Description = "Export positions data",
                Module = "Positions",
                Action = "Export",
                Resource = "Positions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 56,
                Category = "Export",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111186"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },            
            new Permission
            {
                Id = Guid.Parse("5b7c056e-919a-44db-ab52-b226dad6a9e3"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "PasswordPolicy.Read",
                Name = "Read Password Policy",
                Description = "Read Password Policy",
                Module = "PasswordPolicy",
                Action = "Read",
                Resource = "Password Policy",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 0,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111197"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000011f"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Permissions.Create",
                Name = "Create Permissions",
                Description = "Create new permissions",
                Module = "Permissions",
                Action = "Create",
                Resource = "Permissions",
                IsSystemPermission = true,
                IsSystemAdminOnly = true, // Company Admin accessible
                IsActive = true,
                SortOrder = 61,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111194"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000121"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Permissions.Delete",
                Name = "Delete Permissions",
                Description = "Delete permissions",
                Module = "Permissions",
                Action = "Delete",
                Resource = "Permissions",
                IsSystemPermission = true,
                IsSystemAdminOnly = true, // Company Admin accessible
                IsActive = true,
                SortOrder = 61,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111194"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000122"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Permissions.Export",
                Name = "Export Permissions",
                Description = "Export permissions data",
                Module = "Permissions",
                Action = "Export",
                Resource = "Permissions",
                IsSystemPermission = true,
                IsSystemAdminOnly = true, // Company Admin accessible
                IsActive = true,
                SortOrder = 61,
                Category = "Export",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111194"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000123"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Permissions.Import",
                Name = "Import Permissions",
                Description = "Import permissions data",
                Module = "Permissions",
                Action = "Import",
                Resource = "Permissions",
                IsSystemPermission = true,
                IsSystemAdminOnly = true, // Company Admin accessible
                IsActive = true,
                SortOrder = 61,
                Category = "Import",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111194"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000110"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Permissions.Read",
                Name = "Read Permissions",
                Description = "View permissions",
                Module = "Permissions",
                Action = "Read",
                Resource = "Permissions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 61,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111194"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000120"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Permissions.Update",
                Name = "Update Permissions",
                Description = "Update existing permissions",
                Module = "Permissions",
                Action = "Update",
                Resource = "Permissions",
                IsSystemPermission = true,
                IsSystemAdminOnly = true, // Company Admin accessible
                IsActive = true,
                SortOrder = 61,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111194"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000130"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Menus.Create",
                Name = "Create Menus",
                Description = "Create new menus",
                Module = "Menus",
                Action = "Create",
                Resource = "Menus",
                IsSystemPermission = true,
                IsSystemAdminOnly = true, // Company Admin accessible
                IsActive = true,
                SortOrder = 62,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111197"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000131"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Menus.Read",
                Name = "Read Menus",
                Description = "View menus",
                Module = "Menus",
                Action = "Read",
                Resource = "Menus",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 62,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111197"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000132"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Menus.Update",
                Name = "Update Menus",
                Description = "Update existing menus",
                Module = "Menus",
                Action = "Update",
                Resource = "Menus",
                IsSystemPermission = true,
                IsSystemAdminOnly = true, // Company Admin accessible
                IsActive = true,
                SortOrder = 62,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111197"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000133"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Menus.Delete",
                Name = "Delete Menus",
                Description = "Delete menus",
                Module = "Menus",
                Action = "Delete",
                Resource = "Menus",
                IsSystemPermission = true,
                IsSystemAdminOnly = true, // Company Admin accessible
                IsActive = true,
                SortOrder = 62,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111197"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000134"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Menus.Import",
                Name = "Import Menus",
                Description = "Import menus data",
                Module = "Menus",
                Action = "Import",
                Resource = "Menus",
                IsSystemPermission = true,
                IsSystemAdminOnly = true, // Company Admin accessible
                IsActive = true,
                SortOrder = 62,
                Category = "Import",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111197"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000135"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Menus.Export",
                Name = "Export Menus",
                Description = "Export menus data",
                Module = "Menus",
                Action = "Export",
                Resource = "Menus",
                IsSystemPermission = true,
                IsSystemAdminOnly = true, // Company Admin accessible
                IsActive = true,
                SortOrder = 62,
                Category = "Export",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111197"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000011a"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Roles.Create",
                Name = "Create Roles",
                Description = "Create new roles",
                Module = "Roles",
                Action = "Create",
                Resource = "Roles",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 60,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111193"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000011c"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Roles.Delete",
                Name = "Delete Roles",
                Description = "Delete roles",
                Module = "Roles",
                Action = "Delete",
                Resource = "Roles",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 60,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111193"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000011d"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Roles.Export",
                Name = "Export Roles",
                Description = "Export roles data",
                Module = "Roles",
                Action = "Export",
                Resource = "Roles",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 60,
                Category = "Export",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111193"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000011e"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Roles.Import",
                Name = "Import Roles",
                Description = "Import roles data",
                Module = "Roles",
                Action = "Import",
                Resource = "Roles",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 60,
                Category = "Import",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111193"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000109"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Roles.Read",
                Name = "Read Roles",
                Description = "View roles",
                Module = "Roles",
                Action = "Read",
                Resource = "Roles",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 60,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111193"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-00000000011b"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Roles.Update",
                Name = "Update Roles",
                Description = "Update existing roles",
                Module = "Roles",
                Action = "Update",
                Resource = "Roles",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 60,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111193"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("ad909cb1-591b-4244-8dfd-848300d6fc72"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Sessions.Read",
                Name = "Read Sessions",
                Description = "Read Sessions",
                Module = "Sessions",
                Action = "Read",
                Resource = "Sessions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 0,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111195"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("5319d29a-2729-4e40-ac19-16e4d579b91d"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Sessions.ReadAll",
                Name = "Read All Sessions",
                Description = "Read All Sessions",
                Module = "Sessions",
                Action = "Read",
                Resource = "Sessions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 0,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111195"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000001202"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Sessions.Delete",
                Name = "Delete Sessions",
                Description = "Terminate user sessions",
                Module = "Sessions",
                Action = "Delete",
                Resource = "Sessions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 70,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111195"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000018"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Users.Create",
                Name = "Create Users",
                Description = "Create new users",
                Module = "Users",
                Action = "Create",
                Resource = "Users",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 2,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111191"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000020"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Users.Delete",
                Name = "Delete Users",
                Description = "Delete users",
                Module = "Users",
                Action = "Delete",
                Resource = "Users",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 4,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111191"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000021"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Users.Export",
                Name = "Export Users",
                Description = "Export users data",
                Module = "Users",
                Action = "Export",
                Resource = "Users",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 5,
                Category = "Export",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111191"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000022"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Users.Import",
                Name = "Import Users",
                Description = "Import users data",
                Module = "Users",
                Action = "Import",
                Resource = "Users",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 6,
                Category = "Import",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111191"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000017"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Users.Read",
                Name = "Read Users",
                Description = "View user information",
                Module = "Users",
                Action = "Read",
                Resource = "Users",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 1,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111191"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000019"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Users.Update",
                Name = "Update Users",
                Description = "Update existing users",
                Module = "Users",
                Action = "Update",
                Resource = "Users",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 3,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111191"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("99548c5f-f354-4273-8672-68d501303067"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Mfa.Create",
                Name = "Create MFA Settings",
                Description = "Create new MFA settings",
                Module = "MFA",
                Action = "Create",
                Resource = "MfaSettings",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 71,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111196"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("aea70881-c1e5-4a0b-99b7-5945699da333"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Mfa.Update",
                Name = "Update MFA Settings",
                Description = "Update existing MFA settings",
                Module = "MFA",
                Action = "Update",
                Resource = "MfaSettings",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 72,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111196"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("7e681d6e-6e4c-4e76-9731-b0e56b75788f"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "PasswordPolicy.Update",
                Name = "Update Password Policy",
                Description = "Update existing password policy",
                Module = "PasswordPolicy",
                Action = "Update",
                Resource = "PasswordPolicy",
                IsSystemPermission = true,
                IsSystemAdminOnly = true, // Company Admin accessible
                IsActive = true,
                SortOrder = 74,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111198"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("d1604f4b-1e98-4853-ab8a-e022b29e7137"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Sessions.Create",
                Name = "Create Sessions",
                Description = "Create new user sessions",
                Module = "Sessions",
                Action = "Create",
                Resource = "Sessions",
                IsSystemPermission = true,
                IsSystemAdminOnly = true, // Company Admin accessible
                IsActive = true,
                SortOrder = 69,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111195"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("04e3b24b-3863-4ebb-89bc-66ffed52afee"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "Sessions.Update",
                Name = "Update Sessions",
                Description = "Update existing user sessions",
                Module = "Sessions",
                Action = "Update",
                Resource = "Sessions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 70,
                Category = "CRUD",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111195"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            // System-level permissions for System Administrator
            new Permission
            {
                Id = Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffff1"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "System.Dashboard.Read",
                Name = "Read System Dashboard",
                Description = "View system-wide dashboard with all organizations",
                Module = "System",
                Action = "Read",
                Resource = "SystemDashboard",
                IsSystemPermission = true,
                IsSystemAdminOnly = true, // System Admin only
                IsActive = true,
                SortOrder = 1,
                Category = "SYSTEM",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111199"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffff2"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "System.Organizations.Read",
                Name = "Read All Organizations",
                Description = "View all organizations in the system",
                Module = "System",
                Action = "Read",
                Resource = "AllOrganizations",
                IsSystemPermission = true,
                IsSystemAdminOnly = true, // System Admin only
                IsActive = true,
                SortOrder = 2,
                Category = "SYSTEM",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111200"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffff3"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "System.Organizations.Manage",
                Name = "Manage All Organizations",
                Description = "Manage all organizations in the system",
                Module = "System",
                Action = "Manage",
                Resource = "AllOrganizations",
                IsSystemPermission = true,
                IsSystemAdminOnly = true, // System Admin only
                IsActive = true,
                SortOrder = 3,
                Category = "SYSTEM",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111200"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffff4"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "System.Monitoring.Read",
                Name = "Read System Monitoring",
                Description = "View system-wide monitoring data (users, companies, etc.)",
                Module = "System",
                Action = "Read",
                Resource = "Monitoring",
                IsSystemPermission = true,
                IsSystemAdminOnly = true, // System Admin only
                IsActive = true,
                SortOrder = 4,
                Category = "SYSTEM",
                MenuId = Guid.Parse("11111111-1111-1111-1111-111111111199"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
        };

        context.Permissions.AddRange(permissions);
    }

    private static async Task SeedRolePermissionsAsync(AppDbContext context)
    {
        if (await context.RolePermissions.IgnoreQueryFilters().AnyAsync()) return;

        var systemAdminRoleId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var administratorRoleId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var organizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Save changes to ensure permissions are persisted before querying
        await context.SaveChangesAsync();

        // Get all permissions from the database (permissions are system-wide)
        // System organization ID for system-wide permissions
        var systemOrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var allPermissions = await context.Permissions
            .IgnoreQueryFilters()
            .Where(p => p.OrganizationId == systemOrganizationId && p.IsActive)
            .ToListAsync();

        // Create RolePermission entries for System Administrator role (all permissions)
        var systemAdminRolePermissions = allPermissions.Select(permission => new RolePermission
        {
            Id = Guid.NewGuid(),
            OrganizationId = systemOrganizationId,
            RoleId = systemAdminRoleId,
            PermissionId = permission.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        }).ToList();

        context.RolePermissions.AddRange(systemAdminRolePermissions);

        // Create RolePermission entries for Administrator role (Company Admin)
        // Administrator should have access to all company-level features including:
        // - Dashboard, Users, Roles, Permissions (read/update only), Departments, Positions
        // - Sessions, MFA, and all organization settings
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

        var adminPermissions = allPermissions
            .Where(p => adminPermissionCodes.Contains(p.Code))
            .Select(permission => new RolePermission
            {
                Id = Guid.NewGuid(),
                OrganizationId = systemOrganizationId,
                RoleId = administratorRoleId,
                PermissionId = permission.Id,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            }).ToList();

        context.RolePermissions.AddRange(adminPermissions);
    }

    private static async Task SeedUsersAsync(AppDbContext context)
    {
        if (await context.Users.IgnoreQueryFilters().AnyAsync()) return;

        var users = new[]
        {
            new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000027"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Email = "admin@saasbase.com",
                PasswordHash = HashPassword("Admin123!"),
                FullName = "System Administrator",
                FirstName = "System",
                LastName = "Administrator",
                PhoneNumber = "+1-555-0001",
                IsActive = true,
                IsEmailVerified = true,
                IsPhoneVerified = true,
                LastLoginAt = DateTimeOffset.UtcNow,
                JobTitle = "System Administrator",
                Department = "IT",
                EmployeeId = "EMP001",
                TimeZone = "America/New_York",
                Language = "en",
                IsMfaEnabled = false,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000028"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Email = "manager@saasbase.com",
                PasswordHash = HashPassword("Manager123!"),
                FullName = "John Manager",
                FirstName = "John",
                LastName = "Manager",
                PhoneNumber = "+1-555-0002",
                IsActive = true,
                IsEmailVerified = true,
                IsPhoneVerified = true,
                LastLoginAt = DateTimeOffset.UtcNow.AddDays(-1),
                JobTitle = "Operations Manager",
                Department = "Operations",
                EmployeeId = "EMP002",
                TimeZone = "America/New_York",
                Language = "en",
                IsMfaEnabled = false,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000029"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Email = "productmanager@saasbase.com",
                PasswordHash = HashPassword("Product123!"),
                FullName = "Jane Product Manager",
                FirstName = "Jane",
                LastName = "Product Manager",
                PhoneNumber = "+1-555-0003",
                IsActive = true,
                IsEmailVerified = true,
                IsPhoneVerified = true,
                LastLoginAt = DateTimeOffset.UtcNow.AddDays(-2),
                JobTitle = "Product Manager",
                Department = "Product",
                EmployeeId = "EMP003",
                TimeZone = "America/New_York",
                Language = "en",
                IsMfaEnabled = false,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            }
        };

        context.Users.AddRange(users);

        // Add user roles
        var userRoles = new[]
        {
            new UserRole
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000027"),
                RoleId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new UserRole
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000028"),
                RoleId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new UserRole
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000029"),
                RoleId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            }
        };

        context.UserRoles.AddRange(userRoles);
    }

    private static async Task SeedUserSessionsAsync(AppDbContext context)
    {
        if (await context.UserSessions.IgnoreQueryFilters().AnyAsync()) return;

        var sessions = new[]
        {
            new UserSession
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000027"),
                SessionId = Guid.NewGuid().ToString(),
                DeviceId = "device-001",
                DeviceName = "Chrome Browser",
                DeviceType = "Desktop",
                BrowserName = "Chrome",
                BrowserVersion = "120.0",
                OperatingSystem = "Windows 11",
                IpAddress = "192.168.1.100",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
                IsActive = true,
                LastActivityAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new UserSession
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000028"),
                SessionId = Guid.NewGuid().ToString(),
                DeviceId = "device-002",
                DeviceName = "Mobile App",
                DeviceType = "Mobile",
                BrowserName = "Mobile App",
                BrowserVersion = "1.0.0",
                OperatingSystem = "iOS 17",
                IpAddress = "192.168.1.101",
                UserAgent = "AdvanceSupplyChain/1.0.0 (iOS 17.0)",
                IsActive = true,
                LastActivityAt = DateTimeOffset.UtcNow.AddHours(-2),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                CreatedBy = "System"
            }
        };

        context.UserSessions.AddRange(sessions);
    }

    private static async Task SeedMfaSettingsAsync(AppDbContext context)
    {
        if (await context.MfaSettings.IgnoreQueryFilters().AnyAsync()) return;

        var mfaSettings = new[]
        {
            new MfaSettings
            {
                Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000027"),
                MfaType = "TOTP",
                IsEnabled = true,
                SecretKey = "JBSWY3DPEHPK3PXP",
                BackupCodes = "[\"12345678\", \"87654321\", \"11223344\", \"44332211\"]",
                LastUsedAt = DateTimeOffset.UtcNow.AddDays(-1),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new MfaSettings
            {
                Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000028"),
                MfaType = "SMS",
                IsEnabled = true,
                PhoneNumber = "+1-555-0002",
                LastUsedAt = DateTimeOffset.UtcNow.AddHours(-5),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            }
        };

        context.MfaSettings.AddRange(mfaSettings);
    }

    private static async Task SeedLocationsAsync(AppDbContext context)
    {
        if (await context.Locations.IgnoreQueryFilters().AnyAsync()) return;

        var locations = new[]
        {
            new Location
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000030"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Headquarters",
                Description = "Main corporate headquarters",
                Address = "123 Business Park Drive",
                City = "San Francisco",
                State = "CA",
                Country = "USA",
                PostalCode = "94105",
                Phone = "+1-555-0123",
                Email = "hq@saasbase.com",
                ManagerName = "John Smith",
                IsActive = true,
                IsWarehouse = false,
                IsRetail = false,
                IsOffice = true,
                Latitude = 37.7749m,
                Longitude = -122.4194m,
                ParentId = null,
                Level = 0,
                LocationCode = "HQ",
                LocationType = "HEADQUARTERS",
                SortOrder = 1,
                TimeZone = "America/Los_Angeles",
                Currency = "USD",
                Language = "en",
                IsDefault = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Location
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000031"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Main Warehouse",
                Description = "Primary distribution warehouse",
                Address = "456 Industrial Boulevard",
                City = "Oakland",
                State = "CA",
                Country = "USA",
                PostalCode = "94607",
                Phone = "+1-555-0456",
                Email = "warehouse@saasbase.com",
                ManagerName = "Jane Doe",
                IsActive = true,
                IsWarehouse = true,
                IsRetail = false,
                IsOffice = false,
                Latitude = 37.8044m,
                Longitude = -122.2712m,
                ParentId = Guid.Parse("00000000-0000-0000-0000-000000000030"),
                Level = 1,
                LocationCode = "WH001",
                LocationType = "WAREHOUSE",
                SortOrder = 1,
                TimeZone = "America/Los_Angeles",
                Currency = "USD",
                Language = "en",
                IsDefault = false,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            }
        };

        context.Locations.AddRange(locations);
    }

    private static async Task SeedBusinessSettingsAsync(AppDbContext context)
    {
        if (await context.BusinessSettings.IgnoreQueryFilters().AnyAsync()) return;

        var settings = new[]
        {
            new BusinessSetting
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000010"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Key = "CompanyName",
                Value = "Advance Supply Chain Corp",
                Description = "Company display name",
                DataType = "String",
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new BusinessSetting
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000011"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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
                Id = Guid.Parse("00000000-0000-0000-0000-000000000012"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Key = "TimeZone",
                Value = "America/New_York",
                Description = "Default timezone for the organization",
                DataType = "String",
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            }
        };

        context.BusinessSettings.AddRange(settings);
    }

    private static async Task SeedNotificationTemplatesAsync(AppDbContext context)
    {
        if (await context.NotificationTemplates.IgnoreQueryFilters().AnyAsync()) return;

        var templates = new[]
        {
            new NotificationTemplate
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000013"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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
                Id = Guid.Parse("00000000-0000-0000-0000-000000000014"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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
                Id = Guid.Parse("00000000-0000-0000-0000-000000000015"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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
                Id = Guid.Parse("00000000-0000-0000-0000-000000000016"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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
                Id = Guid.Parse("00000000-0000-0000-0000-000000000017"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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
                Id = Guid.Parse("00000000-0000-0000-0000-000000000018"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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
                Id = Guid.Parse("00000000-0000-0000-0000-000000000019"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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
                Id = Guid.Parse("00000000-0000-0000-0000-000000000020"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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
                Id = Guid.Parse("00000000-0000-0000-0000-000000000021"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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
                Id = Guid.Parse("00000000-0000-0000-0000-000000000022"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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
                Id = Guid.Parse("00000000-0000-0000-0000-000000000023"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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

        context.NotificationTemplates.AddRange(templates);
    }

    private static async Task SeedIntegrationSettingsAsync(AppDbContext context)
    {
        if (await context.IntegrationSettings.IgnoreQueryFilters().AnyAsync()) return;

        var integrations = new[]
        {
            new IntegrationSetting
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000015"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "SendGrid Email",
                Description = "Email service integration using SendGrid. IMPORTANT: Update Configuration JSON with your real SendGrid API key, FromEmail, and FromName values.",
                IntegrationType = "EMAIL",
                Provider = "SendGrid",
                Configuration = "{\"ApiKey\":\"YOUR_SENDGRID_API_KEY_HERE\",\"FromEmail\":\"noreply@saasbase.com\",\"FromName\":\"Advance Supply Chain\"}",
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
                Id = Guid.Parse("00000000-0000-0000-0000-000000000016"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Twilio SMS",
                Description = "SMS service integration using Twilio",
                IntegrationType = "SMS",
                Provider = "Twilio",
                Configuration = "{\"AccountSid\":\"{{TWILIO_ACCOUNT_SID}}\",\"AuthToken\":\"{{TWILIO_AUTH_TOKEN}}\",\"FromNumber\":\"+1234567890\"}",
                Credentials = "encrypted_credentials_here",
                IsActive = true,
                IsEnabled = true,
                LastSyncAt = DateTimeOffset.UtcNow,
                LastSyncStatus = "Success",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            }
        };

        context.IntegrationSettings.AddRange(integrations);
    }

    private static async Task SeedDepartmentsAsync(AppDbContext context)
    {
        if (await context.Departments.IgnoreQueryFilters().AnyAsync()) return;

        var departments = new[]
        {
            new Department
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111100"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Information Technology",
                Description = "IT department responsible for technology infrastructure and support",
                Code = "IT",
                ManagerId = Guid.Parse("00000000-0000-0000-0000-000000000027"),
                ManagerName = "System Administrator",
                IsActive = true,
                SortOrder = 1,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Department
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111101"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Operations",
                Description = "Operations department managing day-to-day business operations",
                Code = "OPS",
                ManagerId = Guid.Parse("00000000-0000-0000-0000-000000000028"),
                ManagerName = "John Manager",
                IsActive = true,
                SortOrder = 2,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Department
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111102"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Product Management",
                Description = "Product management department responsible for product strategy and development",
                Code = "PM",
                ManagerId = Guid.Parse("00000000-0000-0000-0000-000000000029"),
                ManagerName = "Jane Product Manager",
                IsActive = true,
                SortOrder = 3,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Department
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111103"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Human Resources",
                Description = "HR department managing employee relations and recruitment",
                Code = "HR",
                ManagerId = null,
                ManagerName = "HR Director",
                IsActive = true,
                SortOrder = 4,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Department
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111104"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Finance",
                Description = "Finance department handling accounting and financial planning",
                Code = "FIN",
                ManagerId = null,
                ManagerName = "CFO",
                IsActive = true,
                SortOrder = 5,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Department
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111105"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Sales",
                Description = "Sales department responsible for customer acquisition and revenue generation",
                Code = "SALES",
                ManagerId = null,
                ManagerName = "Sales Director",
                IsActive = true,
                SortOrder = 6,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Department
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111106"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Marketing",
                Description = "Marketing department handling brand promotion and customer engagement",
                Code = "MKT",
                ManagerId = null,
                ManagerName = "Marketing Director",
                IsActive = true,
                SortOrder = 7,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            }
        };

        context.Departments.AddRange(departments);
    }

    private static async Task SeedPositionsAsync(AppDbContext context)
    {
        if (await context.Positions.IgnoreQueryFilters().AnyAsync()) return;

        var positions = new[]
        {
            // IT Department Positions
            new Position
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222200"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "System Administrator",
                Description = "Manages system infrastructure and user accounts",
                Code = "SYSADMIN",
                Level = "Senior",
                DepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111100"),
                IsActive = true,
                SortOrder = 1,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Position
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222201"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Software Developer",
                Description = "Develops and maintains software applications",
                Code = "DEV",
                Level = "Mid",
                DepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111100"),
                IsActive = true,
                SortOrder = 2,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Position
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222202"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "IT Support Specialist",
                Description = "Provides technical support to end users",
                Code = "ITSUPPORT",
                Level = "Junior",
                DepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111100"),
                IsActive = true,
                SortOrder = 3,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            // Operations Department Positions
            new Position
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222203"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Operations Manager",
                Description = "Manages daily operations and process improvement",
                Code = "OPSMGR",
                Level = "Senior",
                DepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111101"),
                IsActive = true,
                SortOrder = 1,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Position
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222204"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Operations Analyst",
                Description = "Analyzes operational data and processes",
                Code = "OPSANALYST",
                Level = "Mid",
                DepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111101"),
                IsActive = true,
                SortOrder = 2,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            // Product Management Department Positions
            new Position
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222205"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Product Manager",
                Description = "Manages product strategy and roadmap",
                Code = "PM",
                Level = "Senior",
                DepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111102"),
                IsActive = true,
                SortOrder = 1,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Position
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222206"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Product Analyst",
                Description = "Analyzes product performance and market trends",
                Code = "PANALYST",
                Level = "Mid",
                DepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111102"),
                IsActive = true,
                SortOrder = 2,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            // HR Department Positions
            new Position
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222207"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "HR Director",
                Description = "Leads human resources strategy and operations",
                Code = "HRDIR",
                Level = "Senior",
                DepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111103"),
                IsActive = true,
                SortOrder = 1,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Position
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222208"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "HR Specialist",
                Description = "Handles recruitment and employee relations",
                Code = "HRSPEC",
                Level = "Mid",
                DepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111103"),
                IsActive = true,
                SortOrder = 2,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            // Finance Department Positions
            new Position
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222209"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "CFO",
                Description = "Chief Financial Officer responsible for financial strategy",
                Code = "CFO",
                Level = "Executive",
                DepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111104"),
                IsActive = true,
                SortOrder = 1,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Position
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222210"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Accountant",
                Description = "Handles accounting and financial reporting",
                Code = "ACCOUNTANT",
                Level = "Mid",
                DepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111104"),
                IsActive = true,
                SortOrder = 2,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            // Sales Department Positions
            new Position
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222211"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Sales Director",
                Description = "Leads sales strategy and team management",
                Code = "SALESDIR",
                Level = "Senior",
                DepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111105"),
                IsActive = true,
                SortOrder = 1,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Position
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222212"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Sales Representative",
                Description = "Handles customer acquisition and relationship management",
                Code = "SALESREP",
                Level = "Mid",
                DepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111105"),
                IsActive = true,
                SortOrder = 2,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            // Marketing Department Positions
            new Position
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222213"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Marketing Director",
                Description = "Leads marketing strategy and brand management",
                Code = "MKTDIR",
                Level = "Senior",
                DepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111106"),
                IsActive = true,
                SortOrder = 1,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Position
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222214"),
                OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Marketing Specialist",
                Description = "Executes marketing campaigns and content creation",
                Code = "MKTSPEC",
                Level = "Mid",
                DepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111106"),
                IsActive = true,
                SortOrder = 2,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            }
        };

        context.Positions.AddRange(positions);
    }

    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }
}