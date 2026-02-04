using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using SaaSBase.Application;

namespace SaaSBase.Application.Implementations;

public class LocationService : ILocationService
{
    private readonly IOrganizationService _organizationService;
    private readonly ICurrentTenantService _tenantService;

    public LocationService(IOrganizationService organizationService, ICurrentTenantService tenantService)
    {
        _organizationService = organizationService;
        _tenantService = tenantService;
    }

    public async Task<List<LocationDto>> GetLocationsAsync(bool? isActive = null)
    {
        var organizationId = _tenantService.GetOrganizationId();
        var locations = await _organizationService.GetLocationsAsync(organizationId);
        
        if (isActive.HasValue)
        {
            locations = locations.Where(x => x.IsActive == isActive.Value).ToList();
        }

        return locations.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToList();
    }

    public async Task<LocationDto?> GetLocationByIdAsync(Guid id)
    {
        return await _organizationService.GetLocationByIdAsync(id);
    }

    public async Task<LocationDto> CreateLocationAsync(CreateLocationDto dto)
    {
        var organizationId = _tenantService.GetOrganizationId();
        return await _organizationService.CreateLocationAsync(organizationId, dto);
    }

    public async Task<LocationDto?> UpdateLocationAsync(Guid id, UpdateLocationDto dto)
    {
        return await _organizationService.UpdateLocationAsync(id, dto);
    }

    public async Task<bool> DeleteLocationAsync(Guid id)
    {
        return await _organizationService.DeleteLocationAsync(id);
    }

    public async Task<bool> SetActiveAsync(Guid id, bool isActive)
    {
        var location = await _organizationService.GetLocationByIdAsync(id);
        if (location == null) return false;

        var updateDto = new UpdateLocationDto
        {
            Name = location.Name,
            Description = location.Description,
            Address = location.Address,
            City = location.City,
            State = location.State,
            Country = location.Country,
            PostalCode = location.PostalCode,
            Phone = location.Phone,
            Email = location.Email,
            ManagerName = location.ManagerName,
            IsActive = isActive,
            IsWarehouse = location.IsWarehouse,
            IsRetail = location.IsRetail,
            IsOffice = location.IsOffice,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            ParentLocationId = location.ParentLocationId,
            LocationCode = location.LocationCode,
            LocationType = location.LocationType,
            TimeZone = location.TimeZone,
            Currency = location.Currency,
            Language = location.Language,
            IsDefault = location.IsDefault
        };

        var updatedLocation = await _organizationService.UpdateLocationAsync(id, updateDto);
        return updatedLocation != null;
    }

    public async Task<List<LocationHierarchyDto>> GetLocationHierarchyAsync()
    {
        var organizationId = _tenantService.GetOrganizationId();
        return await _organizationService.GetLocationHierarchyAsync(organizationId);
    }

    public async Task<List<LocationDto>> GetLocationsByTypeAsync(string locationType)
    {
        var locations = await GetLocationsAsync();
        
        return locationType.ToLower() switch
        {
            "office" => locations.Where(l => l.IsOffice).ToList(),
            "warehouse" => locations.Where(l => l.IsWarehouse).ToList(),
            "retail" => locations.Where(l => l.IsRetail).ToList(),
            _ => locations.Where(l => l.LocationType.Equals(locationType, StringComparison.OrdinalIgnoreCase)).ToList()
        };
    }

    public async Task<List<LocationDto>> GetChildLocationsAsync(Guid parentLocationId)
    {
        var locations = await GetLocationsAsync();
        return locations.Where(l => l.ParentLocationId == parentLocationId).ToList();
    }

    public async Task<List<LocationDto>> GetRootLocationsAsync()
    {
        var locations = await GetLocationsAsync();
        return locations.Where(l => l.ParentLocationId == null).ToList();
    }

    public async Task<List<LocationDropdownDto>> GetLocationDropdownOptionsAsync(bool? isActive = null)
    {
        return await _organizationService.GetLocationDropdownOptionsAsync(isActive);
    }
}
