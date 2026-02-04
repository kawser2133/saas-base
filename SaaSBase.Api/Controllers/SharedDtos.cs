namespace SaaSBase.Api.Controllers;

/// <summary>
/// Shared DTO for bulk delete operations across all controllers
/// </summary>
public class BulkDeleteRequest
{
    public List<Guid> Ids { get; set; } = new();
}
