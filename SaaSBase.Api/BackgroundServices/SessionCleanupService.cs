using SaaSBase.Application;
using SaaSBase.Application.Services;
using SaaSBase.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SaaSBase.Api.BackgroundServices;

/// <summary>
/// Background service that periodically cleans up expired sessions
/// Marks expired sessions as inactive automatically
/// </summary>
public class SessionCleanupService : BackgroundService
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<SessionCleanupService> _logger;
	private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(15); // Run every 15 minutes

	public SessionCleanupService(
		IServiceProvider serviceProvider,
		ILogger<SessionCleanupService> logger)
	{
		_serviceProvider = serviceProvider;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("Session Cleanup Service is starting.");

		// Don't run cleanup immediately on startup - wait for first interval
		await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); // Initial delay of 2 minutes

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				_logger.LogInformation("Session Cleanup Service is running cleanup task at: {time}", DateTimeOffset.UtcNow);

				await CleanupExpiredSessionsAsync();

				_logger.LogInformation("Session Cleanup Service completed cleanup task successfully.");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error occurred during session cleanup: {message}", ex.Message);
			}

			// Wait for the next interval
			try
			{
				await Task.Delay(_cleanupInterval, stoppingToken);
			}
			catch (TaskCanceledException)
			{
				// This is expected when the service is stopping
				_logger.LogInformation("Session Cleanup Service is stopping.");
				break;
			}
		}
	}

	private async Task CleanupExpiredSessionsAsync()
	{
		using var scope = _serviceProvider.CreateScope();
		var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
		var sessionRepo = unitOfWork.Repository<UserSession>();

		try
		{
			var now = DateTimeOffset.UtcNow;
			
			// Find all active sessions that have expired
			var expiredSessions = await sessionRepo.FindManyAsync(
				x => x.IsActive && x.ExpiresAt <= now
			);

			if (!expiredSessions.Any())
			{
				_logger.LogInformation("No expired sessions found to cleanup.");
				return;
			}

			var count = 0;
			foreach (var session in expiredSessions)
			{
				session.IsActive = false;
				sessionRepo.Update(session);
				count++;
			}

			await unitOfWork.SaveChangesAsync();
			_logger.LogInformation("Successfully marked {count} expired session(s) as inactive.", count);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to cleanup expired sessions: {message}", ex.Message);
			throw;
		}
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Session Cleanup Service is stopping.");
		await base.StopAsync(cancellationToken);
	}
}

