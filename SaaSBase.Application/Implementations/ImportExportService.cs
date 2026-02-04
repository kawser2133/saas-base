using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using SaaSBase.Domain;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;
using System.Text.Json;

namespace SaaSBase.Application.Implementations;

/// <summary>
/// Generic service for handling async import/export operations
/// </summary>
public class ImportExportService : IImportExportService
{
	private readonly IUnitOfWork _unitOfWork;
	private readonly ICurrentTenantService _tenantService;
	private readonly IServiceScopeFactory _serviceScopeFactory;
	private readonly ICacheService _cacheService;
	private readonly string _fileStoragePath;

	private static readonly Dictionary<string, ImportJobStatusDto> _importJobs = new();
	private static readonly Dictionary<string, ExportJobStatusDto> _exportJobs = new();

	public ImportExportService(
		IUnitOfWork unitOfWork,
		ICurrentTenantService tenantService,
		IServiceScopeFactory serviceScopeFactory,
		ICacheService cacheService,
		string fileStoragePath = "ImportExportFiles")
	{
		_unitOfWork = unitOfWork;
		_tenantService = tenantService;
		_serviceScopeFactory = serviceScopeFactory;
		_cacheService = cacheService;
		_fileStoragePath = fileStoragePath;

		if (!Directory.Exists(_fileStoragePath))
		{
			Directory.CreateDirectory(_fileStoragePath);
		}
	}

	public async Task<string> StartExportJobAsync<TEntity>(
		string entityType,
		ExportFormat format,
		Func<Dictionary<string, object?>, Task<List<TEntity>>> dataFetcher,
		Dictionary<string, object?> filters,
		Func<TEntity, Dictionary<string, object>> columnMapper) where TEntity : class
	{
		var jobId = Guid.NewGuid().ToString();
		var organizationId = _tenantService.GetCurrentOrganizationId();
		var userId = _tenantService.GetCurrentUserId();
		var userName = _tenantService.GetCurrentUserName();

		var jobStatus = new ExportJobStatusDto
		{
			JobId = jobId,
			EntityType = entityType,
			Format = format.ToString(),
			Status = "Pending",
			ProgressPercent = 0,
			TotalRows = 0,
			ProcessedRows = 0,
			StartedAt = DateTimeOffset.UtcNow
		};

		_exportJobs[jobId] = jobStatus;

		var history = new ImportExportHistory
		{
			Id = Guid.NewGuid(),
			EntityType = entityType,
			OperationType = ImportExportOperationType.Export,
			FileName = $"{entityType}_Export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{GetFileExtension(format)}",
			Format = format.ToString(),
			JobId = jobId,
			Status = Domain.ProcessingStatus.Pending,
			Progress = 0,
			ImportedBy = userName,
			OrganizationId = organizationId,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			StartedAt = DateTimeOffset.UtcNow,
			AppliedFilters = JsonSerializer.Serialize(filters),
			ExpiresAt = DateTimeOffset.UtcNow.AddHours(24) // Export files expire after 24 hours
		};

		await _unitOfWork.Repository<ImportExportHistory>().AddAsync(history);
		await _unitOfWork.SaveChangesAsync();

		// Start background job with organization context (same pattern as import)
		_ = Task.Run(async () => await ProcessExportJobWithContextAsync(jobId, entityType, format, dataFetcher, filters, columnMapper, history.Id, organizationId, userId, userName));

		return jobId;
	}

	private async Task ProcessExportJobWithContextAsync<TEntity>(
		string jobId,
		string entityType,
		ExportFormat format,
		Func<Dictionary<string, object?>, Task<List<TEntity>>> dataFetcher,
		Dictionary<string, object?> filters,
		Func<TEntity, Dictionary<string, object>> columnMapper,
		Guid historyId,
		Guid organizationId,
		Guid userId,
		string userName) where TEntity : class
	{
		// Create new scope for background task
		using var scope = _serviceScopeFactory.CreateScope();
		var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
		var scopedTenantService = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();

		// Set background context for this operation (CRITICAL: ensures tenant filtering works)
		scopedTenantService.SetBackgroundContext(organizationId, userId, userName);

		await ProcessExportJobAsync(jobId, entityType, format, dataFetcher, filters, columnMapper, historyId, scopedUnitOfWork);
	}

	private async Task ProcessExportJobAsync<TEntity>(
		string jobId,
		string entityType,
		ExportFormat format,
		Func<Dictionary<string, object?>, Task<List<TEntity>>> dataFetcher,
		Dictionary<string, object?> filters,
		Func<TEntity, Dictionary<string, object>> columnMapper,
		Guid historyId,
		IUnitOfWork? scopedUnitOfWork = null) where TEntity : class
	{
		// Use provided scope or create new one
		IUnitOfWork unitOfWork;
		IServiceScope? scope = null;
		
		if (scopedUnitOfWork != null)
		{
			unitOfWork = scopedUnitOfWork;
		}
		else
		{
			scope = _serviceScopeFactory.CreateScope();
			unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
		}

		try
		{
			// Update status to Processing
			UpdateExportJobStatus(jobId, "Processing", 5);
			await UpdateHistoryStatusScoped(unitOfWork, historyId, Domain.ProcessingStatus.Processing, 5);

			// Fetch data
			var data = await dataFetcher(filters);
			var totalRows = data.Count;

			UpdateExportJobStatus(jobId, "Processing", 30, totalRows: totalRows);
			await UpdateHistoryStatusScoped(unitOfWork, historyId, Domain.ProcessingStatus.Processing, 30, totalRows: totalRows);

			// Generate file based on format
			byte[] fileData;
			string fileName;

			switch (format)
			{
				case ExportFormat.Excel:
					fileData = await GenerateExcelExportAsync(data, columnMapper, entityType);
					fileName = $"{entityType}_Export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
					break;

				case ExportFormat.CSV:
					fileData = await GenerateCsvExportAsync(data, columnMapper);
					fileName = $"{entityType}_Export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
					break;

				case ExportFormat.PDF:
					fileData = await GeneratePdfExportAsync(data, columnMapper, entityType);
					fileName = $"{entityType}_Export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
					break;

				case ExportFormat.JSON:
					fileData = await GenerateJsonExportAsync(data);
					fileName = $"{entityType}_Export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
					break;

				default:
					throw new ArgumentException($"Unsupported export format: {format}");
			}

			UpdateExportJobStatus(jobId, "Processing", 70);
			await UpdateHistoryStatusScoped(unitOfWork, historyId, Domain.ProcessingStatus.Processing, 70);

			// Store file
			var filePath = await StoreFileAsync(fileName, fileData, TimeSpan.FromHours(24));

			UpdateExportJobStatus(jobId, "Processing", 90);
			await UpdateHistoryStatusScoped(unitOfWork, historyId, Domain.ProcessingStatus.Processing, 90);

			// Generate download URL
			var downloadUrl = $"/api/import-export/download/{jobId}";

			// Mark as completed
			var completedAt = DateTimeOffset.UtcNow;
			UpdateExportJobStatus(jobId, "Completed", 100, downloadUrl: downloadUrl, fileSizeBytes: fileData.Length, completedAt: completedAt);

			await UpdateHistoryStatusScoped(unitOfWork, historyId, Domain.ProcessingStatus.Completed, 100,
				totalRows: totalRows,
				successCount: totalRows,
				filePath: filePath,
				downloadUrl: downloadUrl,
				fileSizeBytes: fileData.Length,
				completedAt: completedAt);
		}
		catch (Exception ex)
		{
			// Mark as failed
			UpdateExportJobStatus(jobId, "Failed", 0, message: ex.Message);
			await UpdateHistoryStatusScoped(unitOfWork, historyId, Domain.ProcessingStatus.Failed, 0, errorMessage: ex.Message);
		}
		finally
		{
			scope?.Dispose();
		}
	}

	private async Task<byte[]> GenerateExcelExportAsync<TEntity>(
		List<TEntity> data,
		Func<TEntity, Dictionary<string, object>> columnMapper,
		string entityType) where TEntity : class
	{
		using var workbook = new XLWorkbook();
		var worksheet = workbook.Worksheets.Add(entityType);

		if (data.Any())
		{
			// Get columns from first item
			var firstItem = columnMapper(data.First());
			var columns = firstItem.Keys.ToList();

			// Write headers
			for (int i = 0; i < columns.Count; i++)
			{
				worksheet.Cell(1, i + 1).Value = columns[i];
				worksheet.Cell(1, i + 1).Style.Font.Bold = true;
				worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
			}

			// Write data
			for (int rowIndex = 0; rowIndex < data.Count; rowIndex++)
			{
				var rowData = columnMapper(data[rowIndex]);
				for (int colIndex = 0; colIndex < columns.Count; colIndex++)
				{
					var value = rowData[columns[colIndex]];
					worksheet.Cell(rowIndex + 2, colIndex + 1).Value = value?.ToString() ?? "";
				}
			}

			// Auto-fit columns
			worksheet.Columns().AdjustToContents();
		}

		using var stream = new MemoryStream();
		workbook.SaveAs(stream);
		return stream.ToArray();
	}

	private Task<byte[]> GenerateCsvExportAsync<TEntity>(
		List<TEntity> data,
		Func<TEntity, Dictionary<string, object>> columnMapper) where TEntity : class
	{
		var csv = new StringBuilder();

		if (data.Any())
		{
			// Get columns from first item
			var firstItem = columnMapper(data.First());
			var columns = firstItem.Keys.ToList();

			// Write headers
			csv.AppendLine(string.Join(",", columns.Select(c => $"\"{c}\"")));

			// Write data
			foreach (var item in data)
			{
				var rowData = columnMapper(item);
				var values = columns.Select(col =>
				{
					var value = rowData[col]?.ToString() ?? "";
					return $"\"{value.Replace("\"", "\"\"")}\""; // Escape quotes
				});
				csv.AppendLine(string.Join(",", values));
			}
		}

		return Task.FromResult(Encoding.UTF8.GetBytes(csv.ToString()));
	}

	private Task<byte[]> GenerateJsonExportAsync<TEntity>(List<TEntity> data) where TEntity : class
	{
		var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
		{
			WriteIndented = true
		});
		return Task.FromResult(Encoding.UTF8.GetBytes(json));
	}

	private Task<byte[]> GeneratePdfExportAsync<TEntity>(
		List<TEntity> data,
		Func<TEntity, Dictionary<string, object>> columnMapper,
		string entityType) where TEntity : class
	{
		// Configure QuestPDF license (Community license for free use)
		QuestPDF.Settings.License = LicenseType.Community;

		var document = Document.Create(container =>
		{
			container.Page(page =>
			{
				page.Size(PageSizes.A4.Landscape());
				page.Margin(30);
				page.PageColor(Colors.White);
				page.DefaultTextStyle(x => x.FontSize(10));

				// Header
				page.Header()
					.AlignCenter()
					.Text($"{entityType} Export Report")
					.FontSize(20)
					.Bold()
					.FontColor(Colors.Blue.Medium);

				// Content
				page.Content()
					.PaddingVertical(10)
					.Table(table =>
					{
						if (!data.Any())
						{
							table.ColumnsDefinition(columns =>
							{
								columns.ConstantColumn(500);
							});

							table.Cell().Text("No data available").FontSize(12);
							return;
						}

						// Get columns from first item
						var firstItem = columnMapper(data.First());
						var columns = firstItem.Keys.ToList();
						var columnCount = columns.Count;

						// Define columns
						table.ColumnsDefinition(columnsBuilder =>
						{
							for (int i = 0; i < columnCount; i++)
							{
								columnsBuilder.RelativeColumn();
							}
						});

						// Header row
						for (int i = 0; i < columns.Count; i++)
						{
							table.Cell().Row(1).Column((uint)(i + 1))
								.Background(Colors.Blue.Medium)
								.Padding(5)
								.Text(columns[i])
								.FontColor(Colors.White)
								.Bold()
								.FontSize(9);
						}

						// Data rows
						for (int rowIndex = 0; rowIndex < data.Count; rowIndex++)
						{
							var rowData = columnMapper(data[rowIndex]);
							var actualRowIndex = rowIndex + 2; // +2 because row 1 is header

							for (int colIndex = 0; colIndex < columns.Count; colIndex++)
							{
								var value = rowData[columns[colIndex]]?.ToString() ?? "";
								var backgroundColor = actualRowIndex % 2 == 0 ? Colors.Grey.Lighten3 : Colors.White;

								table.Cell().Row((uint)actualRowIndex).Column((uint)(colIndex + 1))
									.Background(backgroundColor)
									.Padding(5)
									.Text(value)
									.FontSize(8);
							}
						}
					});

				// Footer
				page.Footer()
					.AlignCenter()
					.DefaultTextStyle(x => x.FontSize(9))
					.Text(text =>
					{
						text.Span($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Total Records: {data.Count}");
					});
			});
		});

		return Task.FromResult(document.GeneratePdf());
	}

	public Task<ExportJobStatusDto?> GetExportJobStatusAsync(string jobId)
	{
		_exportJobs.TryGetValue(jobId, out var status);
		return Task.FromResult(status);
	}

	public async Task<byte[]?> DownloadExportFileAsync(string jobId)
	{
		var jobStatus = await GetExportJobStatusAsync(jobId);
		if (jobStatus?.Status != "Completed" || string.IsNullOrEmpty(jobStatus.DownloadUrl))
			return null;

		// Get file path from history
		var history = await _unitOfWork.Repository<ImportExportHistory>().GetQueryable()
			.FirstOrDefaultAsync(h => h.JobId == jobId);

		if (history?.FilePath == null)
			return null;

		return await GetFileAsync(history.FilePath);
	}

	public async Task<string> StartImportJobAsync<TCreateDto>(
		string entityType,
		Stream fileStream,
		string fileName,
		Func<IUnitOfWork, Dictionary<string, string>, TCreateDto?, Task<(bool success, string? error, bool isUpdate, bool isSkip)>> rowProcessor,
		DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip,
		Dictionary<string, string>? headerMapping = null) where TCreateDto : class, new()
	{
		var jobId = Guid.NewGuid().ToString();
		var organizationId = _tenantService.GetCurrentOrganizationId();
		var userName = _tenantService.GetCurrentUserName();

		// Store uploaded file
		using var memoryStream = new MemoryStream();
		await fileStream.CopyToAsync(memoryStream);
		var fileData = memoryStream.ToArray();
		var storedFilePath = await StoreFileAsync(fileName, fileData);

		var jobStatus = new ImportJobStatusDto
		{
			JobId = jobId,
			EntityType = entityType,
			Status = "Pending",
			ProgressPercent = 0,
			StartedAt = DateTimeOffset.UtcNow
		};

		_importJobs[jobId] = jobStatus;

		// Create history record
		var history = new ImportExportHistory
		{
			Id = Guid.NewGuid(),
			EntityType = entityType,
			OperationType = ImportExportOperationType.Import,
			FileName = fileName,
			Format = fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? "CSV" : "Excel",
			JobId = jobId,
			Status = Domain.ProcessingStatus.Pending,
			Progress = 0,
			ImportedBy = userName,
			OrganizationId = organizationId,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			StartedAt = DateTimeOffset.UtcNow,
			FileSizeBytes = fileData.Length,
			FilePath = storedFilePath,
			DuplicateHandlingStrategy = duplicateStrategy.ToString()
		};

		await _unitOfWork.Repository<ImportExportHistory>().AddAsync(history);
		await _unitOfWork.SaveChangesAsync();

		// Start background job with organization context
		_ = Task.Run(async () => await ProcessImportJobWithContextAsync(jobId, entityType, storedFilePath, rowProcessor, duplicateStrategy, headerMapping, history.Id, organizationId, userName));

		return jobId;
	}

	private async Task ProcessImportJobWithContextAsync<TCreateDto>(
		string jobId,
		string entityType,
		string filePath,
		Func<IUnitOfWork, Dictionary<string, string>, TCreateDto?, Task<(bool success, string? error, bool isUpdate, bool isSkip)>> rowProcessor,
		DuplicateHandlingStrategy duplicateStrategy,
		Dictionary<string, string>? headerMapping,
		Guid historyId,
		Guid organizationId,
		string userName) where TCreateDto : class, new()
	{
		// Create new scope for background task
		using var scope = _serviceScopeFactory.CreateScope();
		var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
		var scopedTenantService = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();

		// Set background context for this operation
		scopedTenantService.SetBackgroundContext(organizationId, null, userName);

		await ProcessImportJobAsync(jobId, entityType, filePath, rowProcessor, duplicateStrategy, headerMapping, historyId, scopedUnitOfWork);
	}

	private async Task ProcessImportJobAsync<TCreateDto>(
		string jobId,
		string entityType,
		string filePath,
		Func<IUnitOfWork, Dictionary<string, string>, TCreateDto?, Task<(bool success, string? error, bool isUpdate, bool isSkip)>> rowProcessor,
		DuplicateHandlingStrategy duplicateStrategy,
		Dictionary<string, string>? headerMapping,
		Guid historyId,
		IUnitOfWork? scopedUnitOfWork = null) where TCreateDto : class, new()
	{
		// Use provided scope or create new one
		IUnitOfWork unitOfWork;
		IServiceScope? scope = null;
		
		if (scopedUnitOfWork != null)
		{
			unitOfWork = scopedUnitOfWork;
		}
		else
		{
			scope = _serviceScopeFactory.CreateScope();
			unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
		}

		try
		{
			var errors = new List<ImportErrorDetailDto>();
			var skippedRecords = new List<ImportErrorDetailDto>();
			int successCount = 0, updatedCount = 0, skippedCount = 0, errorCount = 0;

			// Update status to Processing
			UpdateImportJobStatus(jobId, "Processing", 5);
			await UpdateHistoryStatusScoped(unitOfWork, historyId, Domain.ProcessingStatus.Processing, 5);

			// Read file
			var fileData = await GetFileAsync(filePath);
			if (fileData == null)
				throw new Exception("Import file not found");

			// Parse file (Excel or CSV)
			List<Dictionary<string, string>> rows;
			if (filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
			{
				rows = ParseCsv(fileData, headerMapping);
			}
			else
			{
				rows = ParseExcel(fileData, headerMapping);
			}

			var totalRows = rows.Count;
			UpdateImportJobStatus(jobId, "Processing", 10, totalRows: totalRows);
			await UpdateHistoryStatusScoped(scopedUnitOfWork, historyId, Domain.ProcessingStatus.Processing, 10, totalRows: totalRows);

			// Process each row
			for (int i = 0; i < rows.Count; i++)
			{
				var row = rows[i];
				var rowNumber = i + 2; // +2 because row 1 is header, and we're 0-indexed

				try
				{
					var result = await rowProcessor(scopedUnitOfWork, row, null);

					// Check for skipped records first, regardless of success value
					// This handles cases where skip returns (false, message, false, true)
					if (result.isSkip)
					{
						skippedCount++;
						// Add skipped records to the report
						skippedRecords.Add(new ImportErrorDetailDto
						{
							RowNumber = rowNumber,
							EntityIdentifier = row.Values.FirstOrDefault() ?? "",
							ErrorType = "Skipped",
							ErrorMessage = result.error ?? "Record skipped (duplicate or already exists)",
							RowData = row
						});
					}
					else if (result.success)
					{
						if (result.isUpdate)
							updatedCount++;
						else
							successCount++;
					}
					else
					{
						errorCount++;
						errors.Add(new ImportErrorDetailDto
						{
							RowNumber = rowNumber,
							EntityIdentifier = row.Values.FirstOrDefault() ?? "",
							ErrorType = "Validation",
							ErrorMessage = result.error ?? "Unknown error",
							RowData = row
						});
					}
				}
				catch (Exception ex)
				{
					errorCount++;
					errors.Add(new ImportErrorDetailDto
					{
						RowNumber = rowNumber,
						EntityIdentifier = row.Values.FirstOrDefault() ?? "",
						ErrorType = "System",
						ErrorMessage = ex.Message,
						RowData = row
					});
				}

				// Update progress
				var progress = 10 + (int)((i + 1) / (double)totalRows * 80);
				UpdateImportJobStatus(jobId, "Processing", progress, totalRows: totalRows, processedRows: i + 1,
					successCount: successCount, updatedCount: updatedCount, skippedCount: skippedCount, errorCount: errorCount);

				// Update history every 100 rows
				if ((i + 1) % 100 == 0)
				{
					await UpdateHistoryStatusScoped(unitOfWork, historyId, Domain.ProcessingStatus.Processing, progress,
						totalRows: totalRows, processedRows: i + 1,
						successCount: successCount, updatedCount: updatedCount,
						skippedCount: skippedCount, errorCount: errorCount);
				}
			}

			// Generate error report if there are errors OR skipped records
			string? errorReportId = null;
			if (errors.Any() || skippedRecords.Any())
			{
				// Combine errors and skipped records into one report
				var allIssues = errors.Concat(skippedRecords).ToList();
				errorReportId = await GenerateErrorReportAsync(entityType, allIssues);
			}

			// Mark as completed
			var completedAt = DateTimeOffset.UtcNow;
			UpdateImportJobStatus(jobId, "Completed", 100, totalRows: totalRows, processedRows: totalRows,
				successCount: successCount, updatedCount: updatedCount, skippedCount: skippedCount,
				errorCount: errorCount, errorReportId: errorReportId, completedAt: completedAt);

			await UpdateHistoryStatusScoped(unitOfWork, historyId, Domain.ProcessingStatus.Completed, 100,
				totalRows: totalRows, successCount: successCount, updatedCount: updatedCount,
				skippedCount: skippedCount, errorCount: errorCount,
				errorReportId: errorReportId, completedAt: completedAt);

			// CRITICAL: Invalidate cache after successful import
			// Get organizationId from history record
			var history = await unitOfWork.Repository<ImportExportHistory>().GetByIdAsync(historyId);
			if (history != null)
			{
				await InvalidateCacheForEntity(entityType, history.OrganizationId);
			}
		}
		catch (Exception ex)
		{
			UpdateImportJobStatus(jobId, "Failed", 0, message: ex.Message);
			await UpdateHistoryStatusScoped(unitOfWork, historyId, Domain.ProcessingStatus.Failed, 0, errorMessage: ex.Message);
		}
		finally
		{
			scope?.Dispose();
		}
	}

	public Task<ImportJobStatusDto?> GetImportJobStatusAsync(string jobId)
	{
		_importJobs.TryGetValue(jobId, out var status);
		return Task.FromResult(status);
	}

	public async Task<byte[]?> GetImportErrorReportAsync(string errorReportId)
	{
		var filePath = Path.Combine(_fileStoragePath, "ErrorReports", $"{errorReportId}.xlsx");
		if (!File.Exists(filePath))
			return null;

		return await File.ReadAllBytesAsync(filePath);
	}

	public async Task<byte[]> GenerateImportTemplateAsync(
		string entityType,
		ImportExportFormat format,
		List<string> headers,
		List<Dictionary<string, object>>? sampleData = null,
		Dictionary<string, List<string>>? dropdownOptions = null)
	{
		if (format == ImportExportFormat.Excel)
		{
			return await GenerateExcelTemplateAsync(entityType, headers, sampleData, dropdownOptions);
		}
		else
		{
			return await GenerateCsvTemplateAsync(headers, sampleData);
		}
	}

	private Task<byte[]> GenerateExcelTemplateAsync(
		string entityType,
		List<string> headers,
		List<Dictionary<string, object>>? sampleData,
		Dictionary<string, List<string>>? dropdownOptions)
	{
		using var workbook = new XLWorkbook();
		var worksheet = workbook.Worksheets.Add(entityType);

		// Write headers
		for (int i = 0; i < headers.Count; i++)
		{
			var cell = worksheet.Cell(1, i + 1);
			cell.Value = headers[i];
			cell.Style.Font.Bold = true;
			cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
			cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
		}

		// Write sample data if provided
		if (sampleData != null && sampleData.Any())
		{
			for (int rowIndex = 0; rowIndex < sampleData.Count; rowIndex++)
			{
				var row = sampleData[rowIndex];
				for (int colIndex = 0; colIndex < headers.Count; colIndex++)
				{
					var header = headers[colIndex];
					if (row.TryGetValue(header, out var value))
					{
						worksheet.Cell(rowIndex + 2, colIndex + 1).Value = value?.ToString() ?? "";
					}
				}
			}
		}

		// Add data validations (dropdowns)
		if (dropdownOptions != null)
		{
			for (int colIndex = 0; colIndex < headers.Count; colIndex++)
			{
				var header = headers[colIndex];
				if (dropdownOptions.TryGetValue(header, out var options) && options.Any())
				{
					var range = worksheet.Range(2, colIndex + 1, 1000, colIndex + 1); // Apply to first 1000 rows
					var validation = range.SetDataValidation();
					validation.List(string.Join(",", options.Select(o => $"\"{o}\"")), true);
				}
			}
		}

		// Auto-fit columns
		worksheet.Columns().AdjustToContents();

		using var stream = new MemoryStream();
		workbook.SaveAs(stream);
		return Task.FromResult(stream.ToArray());
	}

	private Task<byte[]> GenerateCsvTemplateAsync(List<string> headers, List<Dictionary<string, object>>? sampleData)
	{
		var csv = new StringBuilder();

		// Write headers
		csv.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

		// Write sample data if provided
		if (sampleData != null && sampleData.Any())
		{
			foreach (var row in sampleData)
			{
				var values = headers.Select(header =>
				{
					row.TryGetValue(header, out var value);
					var valueStr = value?.ToString() ?? "";
					return $"\"{valueStr.Replace("\"", "\"\"")}\"";
				});
				csv.AppendLine(string.Join(",", values));
			}
		}

		return Task.FromResult(Encoding.UTF8.GetBytes(csv.ToString()));
	}

	public async Task<PagedResultDto<ImportExportHistoryDto>> GetHistoryAsync(
		string entityType,
		ImportExportType? operationType,
		int page,
		int pageSize)
	{
		var organizationId = _tenantService.GetCurrentOrganizationId();

		var query = _unitOfWork.Repository<ImportExportHistory>().GetQueryable()
			.Where(h => h.EntityType == entityType && h.OrganizationId == organizationId);

		if (operationType.HasValue)
		{
			var opType = operationType.Value == ImportExportType.Import
				? ImportExportOperationType.Import
				: ImportExportOperationType.Export;
			query = query.Where(h => h.OperationType == opType);
		}

		var totalCount = await query.CountAsync();

		var items = await query
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
				TotalRows = h.TotalRows,
				SuccessCount = h.SuccessCount,
				UpdatedCount = h.UpdatedCount,
				SkippedCount = h.SkippedCount,
				ErrorCount = h.ErrorCount,
				Status = h.Status.ToString(),
				Progress = h.Progress,
				DuplicateHandlingStrategy = h.DuplicateHandlingStrategy,
				ErrorReportId = h.ErrorReportId,
				DownloadUrl = h.DownloadUrl,
				AppliedFilters = h.AppliedFilters,
				FileSizeBytes = h.FileSizeBytes,
				ImportedBy = h.ImportedBy,
				CreatedAtUtc = h.CreatedAtUtc,
				CompletedAtUtc = h.CompletedAt,
				ErrorMessage = h.ErrorMessage
			})
			.ToListAsync();

		return new PagedResultDto<ImportExportHistoryDto>
		{
			Items = items,
			Page = page,
			PageSize = pageSize,
			TotalCount = totalCount,
			TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
		};
	}

	public async Task<PagedResultDto<ImportExportHistoryDto>> GetAllHistoryAsync(
		string? entityType,
		ImportExportType? operationType,
		Services.ProcessingStatus? status,
		int page,
		int pageSize)
	{
		var organizationId = _tenantService.GetCurrentOrganizationId();

		var query = _unitOfWork.Repository<ImportExportHistory>().GetQueryable()
			.Where(h => h.OrganizationId == organizationId);

		if (!string.IsNullOrEmpty(entityType))
			query = query.Where(h => h.EntityType == entityType);

		if (operationType.HasValue)
		{
			var opType = operationType.Value == ImportExportType.Import
				? ImportExportOperationType.Import
				: ImportExportOperationType.Export;
			query = query.Where(h => h.OperationType == opType);
		}

		if (status.HasValue)
			query = query.Where(h => h.Status == (Domain.ProcessingStatus)status.Value);

		var totalCount = await query.CountAsync();

		var items = await query
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
				TotalRows = h.TotalRows,
				SuccessCount = h.SuccessCount,
				UpdatedCount = h.UpdatedCount,
				SkippedCount = h.SkippedCount,
				ErrorCount = h.ErrorCount,
				Status = h.Status.ToString(),
				Progress = h.Progress,
				DuplicateHandlingStrategy = h.DuplicateHandlingStrategy,
				ErrorReportId = h.ErrorReportId,
				DownloadUrl = h.DownloadUrl,
				AppliedFilters = h.AppliedFilters,
				FileSizeBytes = h.FileSizeBytes,
				ImportedBy = h.ImportedBy,
				CreatedAtUtc = h.CreatedAtUtc,
				CompletedAtUtc = h.CompletedAt,
				ErrorMessage = h.ErrorMessage
			})
			.ToListAsync();

		return new PagedResultDto<ImportExportHistoryDto>
		{
			Items = items,
			Page = page,
			PageSize = pageSize,
			TotalCount = totalCount,
			TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
		};
	}

	public async Task CleanupExpiredFilesAsync()
	{
		var expiredHistory = await _unitOfWork.Repository<ImportExportHistory>().GetQueryable()
			.Where(h => h.ExpiresAt != null && h.ExpiresAt < DateTimeOffset.UtcNow)
			.ToListAsync();

		foreach (var history in expiredHistory)
		{
			if (!string.IsNullOrEmpty(history.FilePath))
			{
				await DeleteFileAsync(history.FilePath);
			}
		}
	}

	public async Task<string> StoreFileAsync(string fileName, byte[] fileData, TimeSpan? expiresIn = null)
	{
		var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
		var filePath = Path.Combine(_fileStoragePath, uniqueFileName);

		await File.WriteAllBytesAsync(filePath, fileData);

		return uniqueFileName;
	}

	public async Task<byte[]?> GetFileAsync(string filePath)
	{
		var fullPath = Path.Combine(_fileStoragePath, filePath);
		if (!File.Exists(fullPath))
			return null;

		return await File.ReadAllBytesAsync(fullPath);
	}

	public Task DeleteFileAsync(string filePath)
	{
		var fullPath = Path.Combine(_fileStoragePath, filePath);
		if (File.Exists(fullPath))
		{
			File.Delete(fullPath);
		}
		return Task.CompletedTask;
	}

	private void UpdateExportJobStatus(string jobId, string status, int progress,
		int? totalRows = null, string? message = null, string? downloadUrl = null,
		long? fileSizeBytes = null, DateTimeOffset? completedAt = null)
	{
		if (_exportJobs.TryGetValue(jobId, out var job))
		{
			job.Status = status;
			job.ProgressPercent = progress;
			if (totalRows.HasValue) job.TotalRows = totalRows.Value;
			if (message != null) job.Message = message;
			if (downloadUrl != null) job.DownloadUrl = downloadUrl;
			if (fileSizeBytes.HasValue) job.FileSizeBytes = fileSizeBytes.Value;
			if (completedAt.HasValue) job.CompletedAt = completedAt.Value;
		}
	}

	private void UpdateImportJobStatus(string jobId, string status, int progress,
		int? totalRows = null, int? processedRows = null, int? successCount = null,
		int? updatedCount = null, int? skippedCount = null, int? errorCount = null,
		string? message = null, string? errorReportId = null, DateTimeOffset? completedAt = null)
	{
		if (_importJobs.TryGetValue(jobId, out var job))
		{
			job.Status = status;
			job.ProgressPercent = progress;
			if (totalRows.HasValue) job.TotalRows = totalRows.Value;
			if (processedRows.HasValue) job.ProcessedRows = processedRows.Value;
			if (successCount.HasValue) job.SuccessCount = successCount.Value;
			if (updatedCount.HasValue) job.UpdatedCount = updatedCount.Value;
			if (skippedCount.HasValue) job.SkippedCount = skippedCount.Value;
			if (errorCount.HasValue) job.ErrorCount = errorCount.Value;
			if (message != null) job.Message = message;
			if (errorReportId != null) job.ErrorReportId = errorReportId;
			if (completedAt.HasValue) job.CompletedAt = completedAt.Value;
		}
	}

	private async Task UpdateHistoryStatus(Guid historyId, Domain.ProcessingStatus status, int progress,
		int? totalRows = null, int? processedRows = null, int? successCount = null,
		int? updatedCount = null, int? skippedCount = null, int? errorCount = null,
		string? filePath = null, string? downloadUrl = null, long? fileSizeBytes = null,
		string? errorReportId = null, string? errorMessage = null, DateTimeOffset? completedAt = null)
	{
		var history = await _unitOfWork.Repository<ImportExportHistory>().GetByIdAsync(historyId);
		if (history != null)
		{
			history.Status = status;
			history.Progress = progress;
			if (totalRows.HasValue) history.TotalRows = totalRows.Value;
			if (successCount.HasValue) history.SuccessCount = successCount.Value;
			if (updatedCount.HasValue) history.UpdatedCount = updatedCount.Value;
			if (skippedCount.HasValue) history.SkippedCount = skippedCount.Value;
			if (errorCount.HasValue) history.ErrorCount = errorCount.Value;
			if (filePath != null) history.FilePath = filePath;
			if (downloadUrl != null) history.DownloadUrl = downloadUrl;
			if (fileSizeBytes.HasValue) history.FileSizeBytes = fileSizeBytes.Value;
			if (errorReportId != null) history.ErrorReportId = errorReportId;
			if (errorMessage != null) history.ErrorMessage = errorMessage;
			if (completedAt.HasValue) history.CompletedAt = completedAt.Value;

			_unitOfWork.Repository<ImportExportHistory>().Update(history);
			await _unitOfWork.SaveChangesAsync();
		}
	}

	private async Task UpdateHistoryStatusScoped(IUnitOfWork scopedUnitOfWork, Guid historyId, Domain.ProcessingStatus status, int progress,
		int? totalRows = null, int? processedRows = null, int? successCount = null,
		int? updatedCount = null, int? skippedCount = null, int? errorCount = null,
		string? filePath = null, string? downloadUrl = null, long? fileSizeBytes = null,
		string? errorReportId = null, string? errorMessage = null, DateTimeOffset? completedAt = null)
	{
		var history = await scopedUnitOfWork.Repository<ImportExportHistory>().GetByIdAsync(historyId);
		if (history != null)
		{
			history.Status = status;
			history.Progress = progress;
			if (totalRows.HasValue) history.TotalRows = totalRows.Value;
			if (successCount.HasValue) history.SuccessCount = successCount.Value;
			if (updatedCount.HasValue) history.UpdatedCount = updatedCount.Value;
			if (skippedCount.HasValue) history.SkippedCount = skippedCount.Value;
			if (errorCount.HasValue) history.ErrorCount = errorCount.Value;
			if (filePath != null) history.FilePath = filePath;
			if (downloadUrl != null) history.DownloadUrl = downloadUrl;
			if (fileSizeBytes.HasValue) history.FileSizeBytes = fileSizeBytes.Value;
			if (errorReportId != null) history.ErrorReportId = errorReportId;
			if (errorMessage != null) history.ErrorMessage = errorMessage;
			if (completedAt.HasValue) history.CompletedAt = completedAt.Value;

			scopedUnitOfWork.Repository<ImportExportHistory>().Update(history);
			await scopedUnitOfWork.SaveChangesAsync();
		}
	}

	private async Task<string> GenerateErrorReportAsync(string entityType, List<ImportErrorDetailDto> errors)
	{
		var errorReportId = Guid.NewGuid().ToString();
		var errorReportPath = Path.Combine(_fileStoragePath, "ErrorReports");

		if (!Directory.Exists(errorReportPath))
		{
			Directory.CreateDirectory(errorReportPath);
		}

		using var workbook = new XLWorkbook();
		var worksheet = workbook.Worksheets.Add("Import Errors");

		// Headers
		worksheet.Cell(1, 1).Value = "Row Number";
		worksheet.Cell(1, 2).Value = "Identifier";
		worksheet.Cell(1, 3).Value = "Error Type";
		worksheet.Cell(1, 4).Value = "Error Message";
		worksheet.Cell(1, 5).Value = "Column";

		// Get all possible columns from row data
		var allColumns = errors.SelectMany(e => e.RowData.Keys).Distinct().ToList();
		for (int i = 0; i < allColumns.Count; i++)
		{
			worksheet.Cell(1, 6 + i).Value = allColumns[i];
		}

		// Style headers
		var headerRow = worksheet.Row(1);
		headerRow.Style.Font.Bold = true;
		headerRow.Style.Fill.BackgroundColor = XLColor.Red;
		headerRow.Style.Font.FontColor = XLColor.White;

		// Write errors
		for (int i = 0; i < errors.Count; i++)
		{
			var error = errors[i];
			worksheet.Cell(i + 2, 1).Value = error.RowNumber;
			worksheet.Cell(i + 2, 2).Value = error.EntityIdentifier;
			worksheet.Cell(i + 2, 3).Value = error.ErrorType;
			worksheet.Cell(i + 2, 4).Value = error.ErrorMessage;
			worksheet.Cell(i + 2, 5).Value = error.Column ?? "";

			// Write row data
			for (int j = 0; j < allColumns.Count; j++)
			{
				if (error.RowData.TryGetValue(allColumns[j], out var value))
				{
					worksheet.Cell(i + 2, 6 + j).Value = value;
				}
			}
		}

		worksheet.Columns().AdjustToContents();

		var filePath = Path.Combine(errorReportPath, $"{errorReportId}.xlsx");
		workbook.SaveAs(filePath);

		return errorReportId;
	}

	private List<Dictionary<string, string>> ParseExcel(byte[] fileData, Dictionary<string, string>? headerMapping)
	{
		var rows = new List<Dictionary<string, string>>();

		using var stream = new MemoryStream(fileData);
		using var workbook = new XLWorkbook(stream);
		var worksheet = workbook.Worksheets.First();

		// Read headers from first row
		var headers = new List<string>();
		var headerRow = worksheet.Row(1);
		var lastColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

		// List of unwanted columns to filter out (case-insensitive)
		var unwantedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"history", "History", "HISTORY",
			"id", "Id", "ID", // Filter out ID columns that might cause validation errors
			"createdat", "CreatedAt", "Created Date", "created date",
			"updatedat", "UpdatedAt", "Updated Date", "updated date",
			"deletedat", "DeletedAt", "Deleted Date", "deleted date"
		};

		for (int col = 1; col <= lastColumn; col++)
		{
			var headerValue = headerRow.Cell(col).GetString().Trim();
			// Only add headers that are not in unwanted columns list
			if (!string.IsNullOrWhiteSpace(headerValue) && !unwantedColumns.Contains(headerValue))
			{
				headers.Add(headerValue);
			}
		}

		// Read data rows
		var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
		for (int rowNum = 2; rowNum <= lastRow; rowNum++)
		{
			var row = worksheet.Row(rowNum);
			var rowData = new Dictionary<string, string>();

			int headerIndex = 0;
			for (int col = 1; col <= lastColumn; col++)
			{
				var headerValue = headerRow.Cell(col).GetString().Trim();
				// Skip unwanted columns
				if (string.IsNullOrWhiteSpace(headerValue) || unwantedColumns.Contains(headerValue))
					continue;

				if (headerIndex < headers.Count)
				{
					var header = headers[headerIndex];
					var mappedHeader = headerMapping?.GetValueOrDefault(header, header) ?? header;
					var value = row.Cell(col).GetString().Trim();
					rowData[mappedHeader] = value;
					headerIndex++;
				}
			}

			// Only add non-empty rows
			if (rowData.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
			{
				rows.Add(rowData);
			}
		}

		return rows;
	}

	private List<Dictionary<string, string>> ParseCsv(byte[] fileData, Dictionary<string, string>? headerMapping)
	{
		var rows = new List<Dictionary<string, string>>();
		var csvContent = Encoding.UTF8.GetString(fileData);
		var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

		if (lines.Length == 0)
			return rows;

		// Parse headers
		var headers = ParseCsvLine(lines[0]);

		// Parse data rows
		for (int i = 1; i < lines.Length; i++)
		{
			var values = ParseCsvLine(lines[i]);
			var rowData = new Dictionary<string, string>();

			for (int j = 0; j < Math.Min(headers.Count, values.Count); j++)
			{
				var header = headers[j];
				var mappedHeader = headerMapping?.GetValueOrDefault(header, header) ?? header;
				rowData[mappedHeader] = values[j];
			}

			if (rowData.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
			{
				rows.Add(rowData);
			}
		}

		return rows;
	}

	private List<string> ParseCsvLine(string line)
	{
		var values = new List<string>();
		var current = new StringBuilder();
		bool inQuotes = false;

		for (int i = 0; i < line.Length; i++)
		{
			char c = line[i];

			if (c == '"')
			{
				if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
				{
					current.Append('"');
					i++;
				}
				else
				{
					inQuotes = !inQuotes;
				}
			}
			else if (c == ',' && !inQuotes)
			{
				values.Add(current.ToString().Trim());
				current.Clear();
			}
			else
			{
				current.Append(c);
			}
		}

		values.Add(current.ToString().Trim());
		return values;
	}

	private string GetFileExtension(ExportFormat format)
	{
		return format switch
		{
			ExportFormat.Excel => "xlsx",
			ExportFormat.CSV => "csv",
			ExportFormat.PDF => "pdf",
			ExportFormat.JSON => "json",
			_ => "xlsx"
		};
	}

	/// <summary>
	/// Invalidate cache for specific entity type after import
	/// </summary>
	private async Task InvalidateCacheForEntity(string entityType, Guid organizationId)
	{
		// Clear list cache for the specific entity type (use wildcard to match all pages/filters)
		var entityLower = entityType.ToLower();
		await _cacheService.RemoveCacheByPatternAsync($"{entityLower}:list:{organizationId}:*");

		// Also clear dropdown and stats cache
		await _cacheService.RemoveCacheByPatternAsync($"{entityLower}:dropdown:{organizationId}");
		await _cacheService.RemoveCacheByPatternAsync($"{entityLower}:stats:{organizationId}");

		// Handle special plural forms
		if (entityType.Equals("User", StringComparison.OrdinalIgnoreCase))
		{
			await _cacheService.RemoveCacheByPatternAsync($"users:list:{organizationId}:*");
			await _cacheService.RemoveCacheByPatternAsync($"users:dropdown:{organizationId}");
			await _cacheService.RemoveCacheByPatternAsync($"users:stats:{organizationId}");
		}
		else if (entityType.Equals("Role", StringComparison.OrdinalIgnoreCase))
		{
			await _cacheService.RemoveCacheByPatternAsync($"roles:list:{organizationId}:*");
			await _cacheService.RemoveCacheByPatternAsync($"roles:dropdown:{organizationId}");
			await _cacheService.RemoveCacheByPatternAsync($"roles:stats:{organizationId}");
			await _cacheService.RemoveCacheByPatternAsync($"roles:hierarchy:{organizationId}");
		}
		else if (entityType.Equals("Department", StringComparison.OrdinalIgnoreCase))
		{
			await _cacheService.RemoveCacheByPatternAsync($"departments:list:{organizationId}:*");
			await _cacheService.RemoveCacheByPatternAsync($"departments:list:{organizationId}");
			await _cacheService.RemoveCacheByPatternAsync($"departments:dropdown:{organizationId}");
			await _cacheService.RemoveCacheByPatternAsync($"departments:stats:{organizationId}");
		}
		else if (entityType.Equals("Position", StringComparison.OrdinalIgnoreCase))
		{
			await _cacheService.RemoveCacheByPatternAsync($"positions:list:{organizationId}:*");
			await _cacheService.RemoveCacheByPatternAsync($"positions:list:{organizationId}");
			await _cacheService.RemoveCacheByPatternAsync($"positions:dropdown:{organizationId}");
			await _cacheService.RemoveCacheByPatternAsync($"positions:stats:{organizationId}");
		}
		else if (entityType.Equals("Menu", StringComparison.OrdinalIgnoreCase))
		{
			await _cacheService.RemoveCacheByPatternAsync($"menus:list:{organizationId}:*");
			await _cacheService.RemoveCacheByPatternAsync($"menus:list:{organizationId}");
			await _cacheService.RemoveCacheByPatternAsync($"menus:dropdown:{organizationId}");
			await _cacheService.RemoveCacheByPatternAsync($"menus:stats:{organizationId}");
			await _cacheService.RemoveCacheByPatternAsync($"user_menus_*_{organizationId}");
			await _cacheService.RemoveCacheByPatternAsync($"menu:detail:*");
		}
	}
}
