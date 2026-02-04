using SaaSBase.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SaaSBase.Api.BackgroundServices;

/// <summary>
/// Background service that periodically cleans up expired export/import files
/// </summary>
public class FileCleanupService : BackgroundService
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<FileCleanupService> _logger;
	private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(6); // Run every 6 hours

	public FileCleanupService(
		IServiceProvider serviceProvider,
		ILogger<FileCleanupService> logger)
	{
		_serviceProvider = serviceProvider;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("File Cleanup Service is starting.");

		// Don't run cleanup immediately on startup - wait for first interval
		await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Initial delay of 5 minutes

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				_logger.LogInformation("File Cleanup Service is running cleanup task at: {time}", DateTimeOffset.UtcNow);

				await CleanupExpiredFilesAsync();

				_logger.LogInformation("File Cleanup Service completed cleanup task successfully.");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred during file cleanup: {message}", ex.Message);
			}

			// Wait for the next interval
			try
			{
				await Task.Delay(_cleanupInterval, stoppingToken);
			}
			catch (TaskCanceledException)
			{
				// This is expected when the service is stopping
				_logger.LogInformation("File Cleanup Service is stopping.");
				break;
			}
		}
	}

	private async Task CleanupExpiredFilesAsync()
	{
		using var scope = _serviceProvider.CreateScope();
		var importExportService = scope.ServiceProvider.GetRequiredService<IImportExportService>();

		try
		{
			await importExportService.CleanupExpiredFilesAsync();
			_logger.LogInformation("Successfully cleaned up expired files.");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to cleanup expired files: {message}", ex.Message);
			throw;
		}
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("File Cleanup Service is stopping.");
		await base.StopAsync(cancellationToken);
	}
}
