using Microsoft.AspNetCore.Mvc;
using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using SaaSBase.Application;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;

namespace SaaSBase.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/locations")]
[ApiVersion("1.0")]
[Authorize]
public class LocationsController : ControllerBase
{
    private readonly ILocationService _locationService;

    public LocationsController(ILocationService locationService)
    {
        _locationService = locationService;
    }

    /// <summary>
    /// Get all locations for the current organization
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<LocationDto>>> GetLocations([FromQuery] bool? isActive = null)
    {
        try
        {
            var locations = await _locationService.GetLocationsAsync(isActive);
            return Ok(locations);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving locations.", error = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific location by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<LocationDto>> GetLocation(Guid id)
    {
        try
        {
            var location = await _locationService.GetLocationByIdAsync(id);
            if (location == null)
            {
                return NotFound(new { message = "Location not found." });
            }
            return Ok(location);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving the location.", error = ex.Message });
        }
    }

    /// <summary>
    /// Create a new location
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<LocationDto>> CreateLocation([FromBody] CreateLocationDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var location = await _locationService.CreateLocationAsync(dto);
            return CreatedAtAction(nameof(GetLocation), new { id = location.Id }, location);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while creating the location.", error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing location
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<LocationDto>> UpdateLocation(Guid id, [FromBody] UpdateLocationDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var location = await _locationService.UpdateLocationAsync(id, dto);
            if (location == null)
            {
                return NotFound(new { message = "Location not found." });
            }
            return Ok(location);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating the location.", error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a location
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteLocation(Guid id)
    {
        try
        {
            var result = await _locationService.DeleteLocationAsync(id);
            if (!result)
            {
                return NotFound(new { message = "Location not found." });
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while deleting the location.", error = ex.Message });
        }
    }

    /// <summary>
    /// Get location hierarchy for the current organization
    /// </summary>
    [HttpGet("hierarchy")]
    public async Task<ActionResult<List<LocationHierarchyDto>>> GetLocationHierarchy()
    {
        try
        {
            var hierarchy = await _locationService.GetLocationHierarchyAsync();
            return Ok(hierarchy);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving location hierarchy.", error = ex.Message });
        }
    }

    /// <summary>
    /// Get active locations only
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<List<LocationDto>>> GetActiveLocations()
    {
        try
        {
            var activeLocations = await _locationService.GetLocationsAsync(isActive: true);
            return Ok(activeLocations);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving active locations.", error = ex.Message });
        }
    }

    /// <summary>
    /// Get locations by type (Office, Warehouse, Retail)
    /// </summary>
    [HttpGet("by-type/{locationType}")]
    public async Task<ActionResult<List<LocationDto>>> GetLocationsByType(string locationType)
    {
        try
        {
            var filteredLocations = await _locationService.GetLocationsByTypeAsync(locationType);
            return Ok(filteredLocations);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving locations by type.", error = ex.Message });
        }
    }

    /// <summary>
    /// Get child locations of a parent location
    /// </summary>
    [HttpGet("{parentId}/children")]
    public async Task<ActionResult<List<LocationDto>>> GetChildLocations(Guid parentId)
    {
        try
        {
            var childLocations = await _locationService.GetChildLocationsAsync(parentId);
            return Ok(childLocations);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving child locations.", error = ex.Message });
        }
    }

    /// <summary>
    /// Get root locations (locations without parent)
    /// </summary>
    [HttpGet("root")]
    public async Task<ActionResult<List<LocationDto>>> GetRootLocations()
    {
        try
        {
            var rootLocations = await _locationService.GetRootLocationsAsync();
            return Ok(rootLocations);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving root locations.", error = ex.Message });
        }
    }

    /// <summary>
    /// Set location active/inactive status
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<ActionResult> SetLocationStatus(Guid id, [FromBody] SetLocationStatusDto dto)
    {
        try
        {
            var result = await _locationService.SetActiveAsync(id, dto.IsActive);
            if (!result)
            {
                return NotFound(new { message = "Location not found." });
            }
            return Ok(new { message = $"Location {(dto.IsActive ? "activated" : "deactivated")} successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating location status.", error = ex.Message });
        }
    }

    [HttpGet("dropdown-options")]
    public async Task<ActionResult<List<LocationDropdownDto>>> GetLocationDropdownOptions([FromQuery] bool? isActive = null)
    {
        try
        {
            var options = await _locationService.GetLocationDropdownOptionsAsync(isActive);
            return Ok(options);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving location dropdown options.", error = ex.Message });
        }
    }
}
