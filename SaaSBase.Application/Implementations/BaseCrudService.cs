using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using SaaSBase.Domain;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace SaaSBase.Application.Implementations;

/// <summary>
/// Base service implementation providing generic CRUD operations for all entities
/// </summary>
/// <typeparam name="TEntity">Domain entity type</typeparam>
/// <typeparam name="TDto">Entity DTO type</typeparam>
/// <typeparam name="TCreateDto">Create DTO type</typeparam>
/// <typeparam name="TUpdateDto">Update DTO type</typeparam>
/// <typeparam name="TStatisticsDto">Statistics DTO type</typeparam>
/// <typeparam name="TDropdownOptionsDto">Dropdown options DTO type</typeparam>
public abstract class BaseCrudService<TEntity, TDto, TCreateDto, TUpdateDto, TStatisticsDto, TDropdownOptionsDto>
	: IBaseEntityService<TDto, TCreateDto, TUpdateDto, TStatisticsDto, TDropdownOptionsDto>
	where TEntity : BaseEntity
	where TDto : class
	where TCreateDto : class
	where TUpdateDto : class
	where TStatisticsDto : BaseStatisticsDto, new()
	where TDropdownOptionsDto : class, new()
{
	protected readonly IUnitOfWork _unitOfWork;
	protected readonly ICurrentTenantService _tenantService;
	protected readonly IMapper _mapper;
	protected IRepository<TEntity> Repository => _unitOfWork.Repository<TEntity>();

	protected BaseCrudService(
		IUnitOfWork unitOfWork,
		ICurrentTenantService tenantService,
		IMapper mapper)
	{
		_unitOfWork = unitOfWork;
		_tenantService = tenantService;
		_mapper = mapper;
	}

	/// <summary>
	/// Get paginated list with filters and sorting
	/// </summary>
	public virtual async Task<PagedResultDto<TDto>> GetAllAsync(
		Dictionary<string, object?> filters,
		int page,
		int pageSize,
		string? sortField = null,
		string? sortDirection = "desc")
	{
		var organizationId = _tenantService.GetCurrentOrganizationId();

		var query = _unitOfWork.Repository<TEntity>().GetQueryable()
			.Where(e => e.OrganizationId == organizationId && !e.IsDeleted);

		query = ApplyFilters(query, filters);
		var totalCount = await query.CountAsync();
		query = ApplySorting(query, sortField, sortDirection);

		var items = await query
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync();

		var dtos = _mapper.Map<List<TDto>>(items);

		return new PagedResultDto<TDto>
		{
			Items = dtos,
			Page = page,
			PageSize = pageSize,
			TotalCount = totalCount,
			TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
		};
	}

	public virtual async Task<TDto?> GetByIdAsync(Guid id)
	{
		var organizationId = _tenantService.GetCurrentOrganizationId();

		var entity = await _unitOfWork.Repository<TEntity>().GetQueryable()
			.FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == organizationId && !e.IsDeleted);

		if (entity == null)
			return null;

		await OnEntityLoadedAsync(entity);
		return _mapper.Map<TDto>(entity);
	}

	public virtual async Task<TDto> CreateAsync(TCreateDto dto)
	{
		var organizationId = _tenantService.GetCurrentOrganizationId();

		await ValidateCreateAsync(dto);

		var entity = _mapper.Map<TEntity>(dto);
		entity.OrganizationId = organizationId;
		entity.CreatedAtUtc = DateTimeOffset.UtcNow;

		await OnBeforeCreateAsync(entity, dto);
		await _unitOfWork.Repository<TEntity>().AddAsync(entity);
		await _unitOfWork.SaveChangesAsync();
		await OnAfterCreateAsync(entity, dto);

		return _mapper.Map<TDto>(entity);
	}

	public virtual async Task<TDto?> UpdateAsync(Guid id, TUpdateDto dto)
	{
		var organizationId = _tenantService.GetCurrentOrganizationId();

		var entity = await _unitOfWork.Repository<TEntity>().GetQueryable()
			.FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == organizationId && !e.IsDeleted);

		if (entity == null)
			return null;

		await ValidateUpdateAsync(id, dto, entity);
		await OnBeforeUpdateAsync(entity, dto);
		_mapper.Map(dto, entity);
		entity.ModifiedAtUtc = DateTimeOffset.UtcNow;

		_unitOfWork.Repository<TEntity>().Update(entity);
		await _unitOfWork.SaveChangesAsync();
		await OnAfterUpdateAsync(entity, dto);

		return _mapper.Map<TDto>(entity);
	}

	public virtual Task<bool> SetActiveAsync(Guid id, bool isActive)
	{
		throw new NotImplementedException("SetActiveAsync must be implemented in derived class for entities with IsActive property");
	}

	public virtual async Task<bool> DeleteAsync(Guid id)
	{
		var organizationId = _tenantService.GetCurrentOrganizationId();

		var entity = await _unitOfWork.Repository<TEntity>().GetQueryable()
			.FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == organizationId && !e.IsDeleted);

		if (entity == null)
			return false;

		await ValidateDeleteAsync(entity);

		entity.IsDeleted = true;
		entity.DeletedAtUtc = DateTimeOffset.UtcNow;
		entity.ModifiedAtUtc = DateTimeOffset.UtcNow;

		Repository.Update(entity);
		await _unitOfWork.SaveChangesAsync();
		await OnAfterDeleteAsync(entity);

		return true;
	}

	public virtual async Task BulkDeleteAsync(List<Guid> ids)
	{
		if (ids == null || !ids.Any())
			return;

		var organizationId = _tenantService.GetCurrentOrganizationId();

		var entities = await _unitOfWork.Repository<TEntity>().GetQueryable()
			.Where(e => ids.Contains(e.Id) && e.OrganizationId == organizationId && !e.IsDeleted)
			.ToListAsync();

		if (!entities.Any())
			return;

		await ValidateBulkDeleteAsync(entities);

		foreach (var entity in entities)
		{
			entity.IsDeleted = true;
			entity.DeletedAtUtc = DateTimeOffset.UtcNow;
			entity.ModifiedAtUtc = DateTimeOffset.UtcNow;
			_unitOfWork.Repository<TEntity>().Update(entity);
		}

		await _unitOfWork.SaveChangesAsync();
		await OnAfterBulkDeleteAsync(entities);
	}

	/// <summary>
	/// Get entity statistics
	/// </summary>
	public virtual async Task<TStatisticsDto> GetStatisticsAsync()
	{
		var organizationId = _tenantService.GetCurrentOrganizationId();

		var query = _unitOfWork.Repository<TEntity>().GetQueryable()
			.Where(e => e.OrganizationId == organizationId && !e.IsDeleted);

		var statistics = new TStatisticsDto
		{
			Total = await query.CountAsync(),
			Active = 0,
			Inactive = 0
		};

		await OnCalculateStatisticsAsync(statistics, query);
		return statistics;
	}

	public abstract Task<TDropdownOptionsDto> GetDropdownOptionsAsync();

	public virtual Task<string> StartExportJobAsync(ExportFormat format, Dictionary<string, object?> filters)
	{
		throw new NotImplementedException("Export functionality should be implemented using IImportExportService");
	}

	public virtual Task<ExportJobStatusDto?> GetExportJobStatusAsync(string jobId)
	{
		throw new NotImplementedException("Export functionality should be implemented using IImportExportService");
	}

	public virtual Task<byte[]?> DownloadExportFileAsync(string jobId)
	{
		throw new NotImplementedException("Export functionality should be implemented using IImportExportService");
	}


	public virtual Task<byte[]> GetImportTemplateAsync(ImportExportFormat format)
	{
		throw new NotImplementedException("Import functionality should be implemented using IImportExportService");
	}

	public virtual Task<string> StartImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip)
	{
		throw new NotImplementedException("Import functionality should be implemented using IImportExportService");
	}

	public virtual Task<ImportJobStatusDto?> GetImportJobStatusAsync(string jobId)
	{
		throw new NotImplementedException("Import functionality should be implemented using IImportExportService");
	}

	public virtual Task<byte[]?> GetImportErrorReportAsync(string errorReportId)
	{
		throw new NotImplementedException("Import functionality should be implemented using IImportExportService");
	}


	public virtual Task<PagedResultDto<ImportExportHistoryDto>> GetImportExportHistoryAsync(ImportExportType? type, int page, int pageSize)
	{
		throw new NotImplementedException("History functionality should be implemented using IImportExportService");
	}

	/// <summary>
	/// Override to apply entity-specific filters
	/// </summary>
	protected virtual IQueryable<TEntity> ApplyFilters(IQueryable<TEntity> query, Dictionary<string, object?> filters)
	{
		if (filters == null || !filters.Any())
			return query;

		if (filters.TryGetValue("search", out var search) && search != null)
		{
			query = ApplySearchFilter(query, search.ToString()!);
		}

		if (filters.TryGetValue("createdFrom", out var createdFrom) && createdFrom != null)
		{
			query = query.Where(e => e.CreatedAtUtc >= (DateTimeOffset)createdFrom);
		}

		if (filters.TryGetValue("createdTo", out var createdTo) && createdTo != null)
		{
			query = query.Where(e => e.CreatedAtUtc <= (DateTimeOffset)createdTo);
		}

		query = ApplyCustomFilters(query, filters);

		return query;
	}

	protected virtual IQueryable<TEntity> ApplySearchFilter(IQueryable<TEntity> query, string search)
	{
		return query;
	}

	protected virtual IQueryable<TEntity> ApplyCustomFilters(IQueryable<TEntity> query, Dictionary<string, object?> filters)
	{
		return query;
	}
	protected virtual IQueryable<TEntity> ApplySorting(IQueryable<TEntity> query, string? sortField, string? sortDirection)
	{
		if (string.IsNullOrWhiteSpace(sortField))
		{
			return query.OrderByDescending(e => e.CreatedAtUtc);
		}

		var isDescending = sortDirection?.ToLower() == "desc";

		return sortField.ToLower() switch
		{
			"createdat" or "createdatutc" => isDescending ? query.OrderByDescending(e => e.CreatedAtUtc) : query.OrderBy(e => e.CreatedAtUtc),
			"updatedat" or "updatedatutc" or "modifiedat" or "modifiedatutc" => isDescending ? query.OrderByDescending(e => e.ModifiedAtUtc) : query.OrderBy(e => e.ModifiedAtUtc),
			_ => ApplyCustomSorting(query, sortField, isDescending)
		};
	}

	protected virtual IQueryable<TEntity> ApplyCustomSorting(IQueryable<TEntity> query, string sortField, bool isDescending)
	{
		return query.OrderByDescending(e => e.CreatedAtUtc);
	}

	protected virtual Task ValidateCreateAsync(TCreateDto dto) => Task.CompletedTask;
	protected virtual Task ValidateUpdateAsync(Guid id, TUpdateDto dto, TEntity existingEntity) => Task.CompletedTask;
	protected virtual Task ValidateDeleteAsync(TEntity entity) => Task.CompletedTask;
	protected virtual Task ValidateBulkDeleteAsync(List<TEntity> entities) => Task.CompletedTask;
	protected virtual Task ValidateStatusChangeAsync(TEntity entity, bool newStatus) => Task.CompletedTask;

	protected virtual Task OnEntityLoadedAsync(TEntity entity) => Task.CompletedTask;
	protected virtual Task OnBeforeCreateAsync(TEntity entity, TCreateDto dto) => Task.CompletedTask;
	protected virtual Task OnAfterCreateAsync(TEntity entity, TCreateDto dto) => Task.CompletedTask;
	protected virtual Task OnBeforeUpdateAsync(TEntity entity, TUpdateDto dto) => Task.CompletedTask;
	protected virtual Task OnAfterUpdateAsync(TEntity entity, TUpdateDto dto) => Task.CompletedTask;
	protected virtual Task OnAfterDeleteAsync(TEntity entity) => Task.CompletedTask;
	protected virtual Task OnAfterBulkDeleteAsync(List<TEntity> entities) => Task.CompletedTask;
	protected virtual Task OnAfterStatusChangeAsync(TEntity entity, bool newStatus) => Task.CompletedTask;
	protected virtual Task OnCalculateStatisticsAsync(TStatisticsDto statistics, IQueryable<TEntity> query) => Task.CompletedTask;
}
