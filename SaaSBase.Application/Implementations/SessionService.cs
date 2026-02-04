using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using SaaSBase.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;

namespace SaaSBase.Application.Implementations;

public class SessionService : ISessionService
{
	private readonly IUnitOfWork _unitOfWork;
	private readonly ICurrentTenantService _tenantService;
    private readonly IUserContextService _userContextService;
    private readonly IPermissionService _permissionService;
    private readonly IImportExportService _importExportService;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public SessionService(IUnitOfWork unitOfWork, ICurrentTenantService tenantService, IUserContextService userContextService, IPermissionService permissionService, IImportExportService importExportService, IServiceScopeFactory serviceScopeFactory)
	{
		_unitOfWork = unitOfWork;
		_tenantService = tenantService;
        _userContextService = userContextService;
        _permissionService = permissionService;
        _importExportService = importExportService;
        _serviceScopeFactory = serviceScopeFactory;
	}

	private static string GenerateSecureToken(int numBytes = 64)
	{
		var bytes = new byte[numBytes];
		RandomNumberGenerator.Fill(bytes);
		return Convert.ToBase64String(bytes)
			.Replace("+", "-")
			.Replace("/", "_")
			.TrimEnd('=');
	}

	public async Task<UserSessionDto> CreateSessionAsync(Guid userId, CreateSessionDto dto)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var sessionRepo = _unitOfWork.Repository<UserSession>();
		
		var session = new UserSession
		{
			Id = Guid.NewGuid(),
			OrganizationId = OrganizationId,
			UserId = userId,
			SessionId = Guid.NewGuid().ToString(),
			DeviceId = dto.DeviceId,
			DeviceName = dto.DeviceName,
			DeviceType = dto.DeviceType,
			BrowserName = dto.BrowserName,
			BrowserVersion = dto.BrowserVersion,
			OperatingSystem = dto.OperatingSystem,
			IpAddress = dto.IpAddress,
			UserAgent = dto.UserAgent,
			Location = dto.Location,
			LastActivityAt = DateTimeOffset.UtcNow,
			ExpiresAt = dto.ExpiresAt,
			IsActive = true,
			Notes = dto.Notes,
			RefreshToken = GenerateSecureToken()
		};

		await sessionRepo.AddAsync(session);
		await _unitOfWork.SaveChangesAsync();

		return new UserSessionDto
		{
			Id = session.Id,
			UserId = session.UserId,
			SessionId = session.SessionId,
			DeviceId = session.DeviceId,
			DeviceName = session.DeviceName,
			DeviceType = session.DeviceType,
			BrowserName = session.BrowserName,
			BrowserVersion = session.BrowserVersion,
			OperatingSystem = session.OperatingSystem,
			IpAddress = session.IpAddress,
			UserAgent = session.UserAgent,
			Location = session.Location,
			LastActivityAt = session.LastActivityAt,
			ExpiresAt = session.ExpiresAt,
			IsActive = session.IsActive,
			Notes = session.Notes,
			CreatedAtUtc = session.CreatedAtUtc,
			RefreshToken = session.RefreshToken
		};
	}

	public async Task<UserSessionDto?> GetSessionAsync(string sessionId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var sessionRepo = _unitOfWork.Repository<UserSession>();
		var session = await sessionRepo.FindAsync(x => x.SessionId == sessionId && x.OrganizationId == OrganizationId && x.IsActive);

		if (session == null) return null;

		return new UserSessionDto
		{
			Id = session.Id,
			UserId = session.UserId,
			SessionId = session.SessionId,
			DeviceId = session.DeviceId,
			DeviceName = session.DeviceName,
			DeviceType = session.DeviceType,
			BrowserName = session.BrowserName,
			BrowserVersion = session.BrowserVersion,
			OperatingSystem = session.OperatingSystem,
			IpAddress = session.IpAddress,
			UserAgent = session.UserAgent,
			Location = session.Location,
			LastActivityAt = session.LastActivityAt,
			ExpiresAt = session.ExpiresAt,
			IsActive = session.IsActive,
			Notes = session.Notes,
			CreatedAtUtc = session.CreatedAtUtc,
			RefreshToken = session.RefreshToken
		};
	}

    public async Task<PagedResultDto<UserSessionDto>> GetUserSessionsAsync(Guid userId, int page, int pageSize, string? search = null, string? sortField = null, string? sortDirection = null)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var sessionRepo = _unitOfWork.Repository<UserSession>();
		var now = DateTimeOffset.UtcNow;
        // Only get active sessions that haven't expired yet
        var all = await sessionRepo.FindManyAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId && x.IsActive && x.ExpiresAt > now);

        // Build DTOs with user email upfront for search/sort
        var userRepo = _unitOfWork.Repository<User>();
        var userIdsAll = all.Select(s => s.UserId).Distinct().ToList();
        var usersAll = await userRepo.FindManyAsync(u => userIdsAll.Contains(u.Id));
        var emailMapAll = usersAll.ToDictionary(u => u.Id, u => u.Email);
        var dtos = all.Select(x => new UserSessionDto
        {
            Id = x.Id,
            UserId = x.UserId,
            UserEmail = emailMapAll.TryGetValue(x.UserId, out var em) ? em : string.Empty,
            SessionId = x.SessionId,
            DeviceId = x.DeviceId,
            DeviceName = x.DeviceName,
            DeviceType = x.DeviceType,
            BrowserName = x.BrowserName,
            BrowserVersion = x.BrowserVersion,
            OperatingSystem = x.OperatingSystem,
            IpAddress = x.IpAddress,
            UserAgent = x.UserAgent,
            Location = x.Location,
            LastActivityAt = x.LastActivityAt,
            ExpiresAt = x.ExpiresAt,
            IsActive = x.IsActive,
            Notes = x.Notes,
            CreatedAtUtc = x.CreatedAtUtc,
            RefreshToken = x.RefreshToken
        }).ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLowerInvariant();
            dtos = dtos.Where(x =>
                (x.UserEmail ?? string.Empty).ToLower().Contains(q) ||
                (x.DeviceName ?? string.Empty).ToLower().Contains(q) ||
                (x.DeviceType ?? string.Empty).ToLower().Contains(q) ||
                (x.BrowserName ?? string.Empty).ToLower().Contains(q) ||
                (x.BrowserVersion ?? string.Empty).ToLower().Contains(q) ||
                (x.OperatingSystem ?? string.Empty).ToLower().Contains(q) ||
                (x.IpAddress ?? string.Empty).ToLower().Contains(q) ||
                (x.Location ?? string.Empty).ToLower().Contains(q) ||
                (x.SessionId ?? string.Empty).ToLower().Contains(q)
            ).ToList();
        }

        bool desc = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        dtos = (sortField?.ToLowerInvariant()) switch
        {
            "useremail" => (desc ? dtos.OrderByDescending(x => x.UserEmail) : dtos.OrderBy(x => x.UserEmail)).ToList(),
            "device" => (desc ? dtos.OrderByDescending(x => x.DeviceName) : dtos.OrderBy(x => x.DeviceName)).ToList(),
            "browser" => (desc ? dtos.OrderByDescending(x => x.BrowserName) : dtos.OrderBy(x => x.BrowserName)).ToList(),
            "os" => (desc ? dtos.OrderByDescending(x => x.OperatingSystem) : dtos.OrderBy(x => x.OperatingSystem)).ToList(),
            "ip" => (desc ? dtos.OrderByDescending(x => x.IpAddress) : dtos.OrderBy(x => x.IpAddress)).ToList(),
            "location" => (desc ? dtos.OrderByDescending(x => x.Location) : dtos.OrderBy(x => x.Location)).ToList(),
            "lastactivityat" => (desc ? dtos.OrderByDescending(x => x.LastActivityAt) : dtos.OrderBy(x => x.LastActivityAt)).ToList(),
            "expiresat" => (desc ? dtos.OrderByDescending(x => x.ExpiresAt) : dtos.OrderBy(x => x.ExpiresAt)).ToList(),
            _ => dtos.OrderByDescending(x => x.LastActivityAt).ToList()
        };

        var total = dtos.LongCount();
        var items = dtos
            .Skip(Math.Max(0, (page - 1) * pageSize))
            .Take(Math.Max(1, pageSize))
            .ToList();

        return new PagedResultDto<UserSessionDto>
		{
			Page = page,
			PageSize = pageSize,
			TotalCount = total,
			Items = items
		};
	}

    public async Task<PagedResultDto<UserSessionDto>> GetOrganizationSessionsAsync(int page, int pageSize, Guid? organizationId = null, string? search = null, string? sortField = null, string? sortDirection = null)
	{
		var currentOrganizationId = _tenantService.GetOrganizationId();
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        
        // Determine which organization to filter by
        Guid filterOrganizationId;
        if (isSystemAdmin && organizationId.HasValue)
        {
            // System Admin can filter by any organization
            filterOrganizationId = organizationId.Value;
        }
        else
        {
            // Regular users can only see their own organization
            filterOrganizationId = currentOrganizationId;
        }
        
		var sessionRepo = _unitOfWork.Repository<UserSession>();
		var now = DateTimeOffset.UtcNow;
        
        // Build query with proper filtering
        IQueryable<UserSession> query;
        if (isSystemAdmin && !organizationId.HasValue)
        {
            // System Admin without filter - show all organizations
            query = sessionRepo.GetQueryable()
                .IgnoreQueryFilters()
                .Where(x => x.IsActive && x.ExpiresAt > now);
        }
        else
        {
            // Filter by specific organization
            query = sessionRepo.GetQueryable()
                .IgnoreQueryFilters()
                .Where(x => x.OrganizationId == filterOrganizationId && x.IsActive && x.ExpiresAt > now);
        }
        
        var all = await query.ToListAsync();

        var userRepo = _unitOfWork.Repository<User>();
        var orgRepo = _unitOfWork.Repository<Organization>();
        var userIdsAll = all.Select(s => s.UserId).Distinct().ToList();
        var orgIdsAll = all.Select(s => s.OrganizationId).Distinct().ToList();
        
        // For System Admin viewing cross-organization data, use IgnoreQueryFilters
        IQueryable<User> usersQuery;
        if (isSystemAdmin && !organizationId.HasValue)
        {
            usersQuery = userRepo.GetQueryable().IgnoreQueryFilters().Where(u => userIdsAll.Contains(u.Id) && !u.IsDeleted);
        }
        else
        {
            usersQuery = userRepo.GetQueryable().IgnoreQueryFilters().Where(u => userIdsAll.Contains(u.Id) && u.OrganizationId == filterOrganizationId && !u.IsDeleted);
        }
        var usersAll = await usersQuery.ToListAsync();
        var emailMapAll = usersAll.ToDictionary(u => u.Id, u => u.Email);
        
        // Get organization names
        var orgsAll = await orgRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(o => orgIdsAll.Contains(o.Id) && !o.IsDeleted)
            .Select(o => new { o.Id, o.Name })
            .ToListAsync();
        var orgNameMap = orgsAll.ToDictionary(o => o.Id, o => o.Name);
        
        var dtos = all.Select(x => new UserSessionDto
        {
            Id = x.Id,
            UserId = x.UserId,
            UserEmail = emailMapAll.TryGetValue(x.UserId, out var em) ? em : string.Empty,
            SessionId = x.SessionId,
            DeviceId = x.DeviceId,
            DeviceName = x.DeviceName,
            DeviceType = x.DeviceType,
            BrowserName = x.BrowserName,
            BrowserVersion = x.BrowserVersion,
            OperatingSystem = x.OperatingSystem,
            IpAddress = x.IpAddress,
            UserAgent = x.UserAgent,
            Location = x.Location,
            LastActivityAt = x.LastActivityAt,
            ExpiresAt = x.ExpiresAt,
            IsActive = x.IsActive,
            Notes = x.Notes,
            CreatedAtUtc = x.CreatedAtUtc,
            RefreshToken = x.RefreshToken,
            OrganizationId = x.OrganizationId,
            OrganizationName = orgNameMap.TryGetValue(x.OrganizationId, out var orgName) ? orgName : null
        }).ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLowerInvariant();
            dtos = dtos.Where(x =>
                (x.UserEmail ?? string.Empty).ToLower().Contains(q) ||
                (x.DeviceName ?? string.Empty).ToLower().Contains(q) ||
                (x.DeviceType ?? string.Empty).ToLower().Contains(q) ||
                (x.BrowserName ?? string.Empty).ToLower().Contains(q) ||
                (x.BrowserVersion ?? string.Empty).ToLower().Contains(q) ||
                (x.OperatingSystem ?? string.Empty).ToLower().Contains(q) ||
                (x.IpAddress ?? string.Empty).ToLower().Contains(q) ||
                (x.Location ?? string.Empty).ToLower().Contains(q) ||
                (x.SessionId ?? string.Empty).ToLower().Contains(q) ||
                (x.OrganizationName ?? string.Empty).ToLower().Contains(q)
            ).ToList();
        }

        bool desc = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        dtos = (sortField?.ToLowerInvariant()) switch
        {
            "useremail" => (desc ? dtos.OrderByDescending(x => x.UserEmail) : dtos.OrderBy(x => x.UserEmail)).ToList(),
            "device" => (desc ? dtos.OrderByDescending(x => x.DeviceName) : dtos.OrderBy(x => x.DeviceName)).ToList(),
            "browser" => (desc ? dtos.OrderByDescending(x => x.BrowserName) : dtos.OrderBy(x => x.BrowserName)).ToList(),
            "os" => (desc ? dtos.OrderByDescending(x => x.OperatingSystem) : dtos.OrderBy(x => x.OperatingSystem)).ToList(),
            "ip" => (desc ? dtos.OrderByDescending(x => x.IpAddress) : dtos.OrderBy(x => x.IpAddress)).ToList(),
            "location" => (desc ? dtos.OrderByDescending(x => x.Location) : dtos.OrderBy(x => x.Location)).ToList(),
            "organizationname" or "organizationid" => (desc ? dtos.OrderByDescending(x => x.OrganizationName ?? string.Empty) : dtos.OrderBy(x => x.OrganizationName ?? string.Empty)).ToList(),
            "lastactivityat" => (desc ? dtos.OrderByDescending(x => x.LastActivityAt) : dtos.OrderBy(x => x.LastActivityAt)).ToList(),
            "expiresat" => (desc ? dtos.OrderByDescending(x => x.ExpiresAt) : dtos.OrderBy(x => x.ExpiresAt)).ToList(),
            _ => dtos.OrderByDescending(x => x.LastActivityAt).ToList()
        };

        var total = dtos.LongCount();
        var items = dtos
            .Skip(Math.Max(0, (page - 1) * pageSize))
            .Take(Math.Max(1, pageSize))
            .ToList();

        return new PagedResultDto<UserSessionDto>
		{
			Page = page,
			PageSize = pageSize,
			TotalCount = total,
			Items = items
		};
	}

	public async Task<bool> UpdateSessionActivityAsync(string sessionId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var sessionRepo = _unitOfWork.Repository<UserSession>();
		var session = await sessionRepo.FindAsync(x => x.SessionId == sessionId && x.OrganizationId == OrganizationId && x.IsActive);

		if (session == null) return false;

		session.LastActivityAt = DateTimeOffset.UtcNow;

		sessionRepo.Update(session);
		await _unitOfWork.SaveChangesAsync();
		return true;
	}

	public async Task<bool> RevokeSessionAsync(string sessionId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var sessionRepo = _unitOfWork.Repository<UserSession>();
		var session = await sessionRepo.FindAsync(x => x.SessionId == sessionId && x.OrganizationId == OrganizationId && x.IsActive);

		if (session == null) return false;

		session.IsActive = false;

		sessionRepo.Update(session);
		await _unitOfWork.SaveChangesAsync();
		return true;
	}

	public async Task<bool> RevokeAllUserSessionsAsync(Guid userId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var sessionRepo = _unitOfWork.Repository<UserSession>();
		var sessions = await sessionRepo.FindManyAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId && x.IsActive);

		foreach (var session in sessions)
		{
			session.IsActive = false;
			sessionRepo.Update(session);
		}

		await _unitOfWork.SaveChangesAsync();
		return true;
	}

	public async Task<int> BulkRevokeSessionsAsync(List<string> sessionIds)
	{
		if (sessionIds == null || !sessionIds.Any())
			return 0;

		var OrganizationId = _tenantService.GetOrganizationId();
		var sessionRepo = _unitOfWork.Repository<UserSession>();
		var sessions = await sessionRepo.FindManyAsync(x => sessionIds.Contains(x.SessionId) && x.OrganizationId == OrganizationId && x.IsActive);

		if (!sessions.Any())
			return 0;

		foreach (var session in sessions)
		{
			session.IsActive = false;
			sessionRepo.Update(session);
		}

		await _unitOfWork.SaveChangesAsync();
		return sessions.Count();
	}

	public async Task<bool> RevokeExpiredSessionsAsync()
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var sessionRepo = _unitOfWork.Repository<UserSession>();
		var expiredSessions = await sessionRepo.FindManyAsync(x => x.OrganizationId == OrganizationId && x.IsActive && x.ExpiresAt <= DateTimeOffset.UtcNow);

		foreach (var session in expiredSessions)
		{
			session.IsActive = false;
			sessionRepo.Update(session);
		}

		await _unitOfWork.SaveChangesAsync();
		return true;
	}

	public async Task<List<UserSessionDto>> GetActiveSessionsAsync(Guid userId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var sessionRepo = _unitOfWork.Repository<UserSession>();
		var sessions = await sessionRepo.FindManyAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId && x.IsActive && x.ExpiresAt > DateTimeOffset.UtcNow);

		return sessions.Select(x => new UserSessionDto
		{
			Id = x.Id,
			UserId = x.UserId,
			SessionId = x.SessionId,
			DeviceId = x.DeviceId,
			DeviceName = x.DeviceName,
			DeviceType = x.DeviceType,
			BrowserName = x.BrowserName,
			BrowserVersion = x.BrowserVersion,
			OperatingSystem = x.OperatingSystem,
			IpAddress = x.IpAddress,
			UserAgent = x.UserAgent,
			Location = x.Location,
			LastActivityAt = x.LastActivityAt,
			ExpiresAt = x.ExpiresAt,
			IsActive = x.IsActive,
			Notes = x.Notes,
			CreatedAtUtc = x.CreatedAtUtc,
			RefreshToken = x.RefreshToken
		}).ToList();
	}

    // ========================================
    // Async Export (non-blocking)
    // ========================================

    public async Task<string> StartExportJobAsync(ExportFormat format, Dictionary<string, object?> filters)
    {
        // Capture context before background task - SAME PATTERN as Roles/Users
        var currentUserId = _tenantService.GetCurrentUserId();
        var canReadAll = currentUserId != Guid.Empty && await _permissionService.UserHasPermissionAsync(currentUserId, "Sessions.ReadAll");
        var organizationId = _tenantService.GetOrganizationId();

        // Add scope and context to filters
        filters["scope"] = canReadAll ? "org" : "self";
        filters["currentUserId"] = currentUserId;
        filters["organizationId"] = organizationId;

        return await _importExportService.StartExportJobAsync<UserSessionDto>(
            entityType: "Session",
            format: format,
            dataFetcher: async (f) =>
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var scopedTenantService = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();
                
                // CRITICAL: Set background context to ensure DbContext global query filter works correctly
                var orgId = organizationId; // Use captured value from outer scope
                scopedTenantService.SetBackgroundContext(orgId, currentUserId, null);
                
                var scopedSessionRepo = scopedUnitOfWork.Repository<UserSession>();
                var scopedUserRepo = scopedUnitOfWork.Repository<User>();
                
                var s = f.GetValueOrDefault("search")?.ToString();
                var sf = f.GetValueOrDefault("sortField")?.ToString();
                var sd = f.GetValueOrDefault("sortDirection")?.ToString();
                var scopeType = f.GetValueOrDefault("scope")?.ToString();
                var userId = f.GetValueOrDefault("currentUserId") is Guid guid ? guid : Guid.Empty;
                var selectedIds = f.GetValueOrDefault("selectedIds") as List<string>;

                IEnumerable<UserSession> all;
                if (scopeType == "org")
                {
                    all = await scopedSessionRepo.FindManyAsync(x => x.OrganizationId == orgId && x.IsActive);
                }
                else
                {
                    all = await scopedSessionRepo.FindManyAsync(x => x.UserId == userId && x.OrganizationId == orgId && x.IsActive);
                }
                
                var sessions = all.ToList();

                // Filter by selectedIds if provided
                if (selectedIds != null && selectedIds.Any())
                {
                    sessions = sessions.Where(x => selectedIds.Contains(x.SessionId)).ToList();
                }

                var userIdsAll = sessions.Select(s => s.UserId).Distinct().ToList();
                // CRITICAL: Filter users by organizationId to ensure we only get users from the same organization
                var usersAll = await scopedUserRepo.FindManyAsync(u => userIdsAll.Contains(u.Id) && u.OrganizationId == orgId && !u.IsDeleted);
                var emailMapAll = usersAll.ToDictionary(u => u.Id, u => u.Email);

                var dtos = sessions.Select(x => new UserSessionDto
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    UserEmail = emailMapAll.TryGetValue(x.UserId, out var em) ? em : string.Empty,
                    SessionId = x.SessionId,
                    DeviceId = x.DeviceId,
                    DeviceName = x.DeviceName,
                    DeviceType = x.DeviceType,
                    BrowserName = x.BrowserName,
                    BrowserVersion = x.BrowserVersion,
                    OperatingSystem = x.OperatingSystem,
                    IpAddress = x.IpAddress,
                    UserAgent = x.UserAgent,
                    Location = x.Location,
                    LastActivityAt = x.LastActivityAt,
                    ExpiresAt = x.ExpiresAt,
                    IsActive = x.IsActive,
                    Notes = x.Notes,
                    CreatedAtUtc = x.CreatedAtUtc,
                    RefreshToken = x.RefreshToken
                }).ToList();

                if (!string.IsNullOrWhiteSpace(s))
                {
                    var q = s.Trim().ToLowerInvariant();
                    dtos = dtos.Where(x =>
                        (x.UserEmail ?? string.Empty).ToLower().Contains(q) ||
                        (x.DeviceName ?? string.Empty).ToLower().Contains(q) ||
                        (x.DeviceType ?? string.Empty).ToLower().Contains(q) ||
                        (x.BrowserName ?? string.Empty).ToLower().Contains(q) ||
                        (x.BrowserVersion ?? string.Empty).ToLower().Contains(q) ||
                        (x.OperatingSystem ?? string.Empty).ToLower().Contains(q) ||
                        (x.IpAddress ?? string.Empty).ToLower().Contains(q) ||
                        (x.Location ?? string.Empty).ToLower().Contains(q) ||
                        (x.SessionId ?? string.Empty).ToLower().Contains(q)
                    ).ToList();
                }

                bool desc = string.Equals(sd, "desc", StringComparison.OrdinalIgnoreCase);
                dtos = (sf?.ToLowerInvariant()) switch
                {
                    "useremail" => (desc ? dtos.OrderByDescending(x => x.UserEmail) : dtos.OrderBy(x => x.UserEmail)).ToList(),
                    "device" => (desc ? dtos.OrderByDescending(x => x.DeviceName) : dtos.OrderBy(x => x.DeviceName)).ToList(),
                    "browser" => (desc ? dtos.OrderByDescending(x => x.BrowserName) : dtos.OrderBy(x => x.BrowserName)).ToList(),
                    "os" => (desc ? dtos.OrderByDescending(x => x.OperatingSystem) : dtos.OrderBy(x => x.OperatingSystem)).ToList(),
                    "ip" => (desc ? dtos.OrderByDescending(x => x.IpAddress) : dtos.OrderBy(x => x.IpAddress)).ToList(),
                    "location" => (desc ? dtos.OrderByDescending(x => x.Location) : dtos.OrderBy(x => x.Location)).ToList(),
                    "lastactivityat" => (desc ? dtos.OrderByDescending(x => x.LastActivityAt) : dtos.OrderBy(x => x.LastActivityAt)).ToList(),
                    "expiresat" => (desc ? dtos.OrderByDescending(x => x.ExpiresAt) : dtos.OrderBy(x => x.ExpiresAt)).ToList(),
                    _ => dtos.OrderByDescending(x => x.LastActivityAt).ToList()
                };

                return dtos;
            },
            filters: filters,
            columnMapper: (session) =>
            {
                var email = !string.IsNullOrWhiteSpace(session?.UserEmail) 
                    ? session.UserEmail.Trim() 
                    : string.Empty;
                
                return new Dictionary<string, object>
                {
                    ["UserEmail"] = email,
                    ["Device"] = session?.DeviceName ?? string.Empty,
                    ["Browser"] = session?.BrowserName ?? string.Empty,
                    ["OS"] = session?.OperatingSystem ?? string.Empty,
                    ["IP"] = session?.IpAddress ?? string.Empty,
                    ["Location"] = session?.Location ?? string.Empty,
                    ["LastActivity"] = session?.LastActivityAt.ToString("o") ?? string.Empty,
                    ["Expires"] = session?.ExpiresAt.ToString("o") ?? string.Empty,
                    ["SessionId"] = session?.SessionId ?? string.Empty
                };
            });
    }

    public Task<ExportJobStatusDto?> GetExportJobStatusAsync(string jobId)
    {
        return _importExportService.GetExportJobStatusAsync(jobId);
    }

    public Task<byte[]?> DownloadExportFileAsync(string jobId)
    {
        return _importExportService.DownloadExportFileAsync(jobId);
    }

    public async Task<PagedResultDto<ImportExportHistoryDto>> GetImportExportHistoryAsync(ImportExportType? type, int page, int pageSize)
    {
        var organizationId = _tenantService.GetOrganizationId();

        var query = _unitOfWork.Repository<ImportExportHistory>().GetQueryable()
            .Where(h => h.OrganizationId == organizationId && h.EntityType == "Session");

        if (type.HasValue)
            query = query.Where(h => h.OperationType == (ImportExportOperationType)type.Value);

        var totalCount = await query.CountAsync();

        var history = await query
            .OrderByDescending(h => h.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new ImportExportHistoryDto
            {
                Id = h.Id,
                JobId = h.JobId,
                EntityType = h.EntityType,
                OperationType = h.OperationType.ToString(),
                FileName = h.FileName,
                Format = h.Format,
                FileSizeBytes = h.FileSizeBytes,
                Status = h.Status.ToString(),
                Progress = h.Progress,
                ErrorMessage = h.ErrorMessage,
                TotalRows = h.TotalRows,
                SuccessCount = h.SuccessCount,
                UpdatedCount = h.UpdatedCount,
                SkippedCount = h.SkippedCount,
                ErrorCount = h.ErrorCount,
                DuplicateHandlingStrategy = h.DuplicateHandlingStrategy,
                ErrorReportId = h.ErrorReportId,
                DownloadUrl = h.DownloadUrl,
                AppliedFilters = h.AppliedFilters,
                CreatedAtUtc = h.CreatedAtUtc,
                CompletedAtUtc = h.CompletedAt,
                ImportedBy = h.ImportedBy
            })
            .ToListAsync();

        return new PagedResultDto<ImportExportHistoryDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = history
        };
    }
}
