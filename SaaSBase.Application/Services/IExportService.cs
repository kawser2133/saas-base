namespace SaaSBase.Application.Services;

public interface IExportService<T>
{
	Task<byte[]> ExportToCsvAsync(IEnumerable<T> data);
	Task<byte[]> ExportToExcelAsync(IEnumerable<T> data);
}
