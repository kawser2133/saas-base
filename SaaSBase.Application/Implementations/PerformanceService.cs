using SaaSBase.Application.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace SaaSBase.Application.Implementations
{
    /// <summary>
    /// Implementation of IPerformanceService for monitoring any service performance
    /// </summary>
    public class PerformanceService : IPerformanceService
    {
        private readonly ILogger<PerformanceService> _logger;

        public PerformanceService(ILogger<PerformanceService> logger)
        {
            _logger = logger;
        }

        public async Task<T> MonitorAsync<T>(string operationName, Func<Task<T>> operation)
        {
            var stopwatch = Stopwatch.StartNew();
            var success = true;
            Exception? exception = null;

            try
            {
                var result = await operation();
                return result;
            }
            catch (Exception ex)
            {
                success = false;
                exception = ex;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                LogPerformance(operationName, stopwatch.Elapsed, success, exception?.Message);
            }
        }

        public async Task MonitorAsync(string operationName, Func<Task> operation)
        {
            var stopwatch = Stopwatch.StartNew();
            var success = true;
            Exception? exception = null;

            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                success = false;
                exception = ex;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                LogPerformance(operationName, stopwatch.Elapsed, success, exception?.Message);
            }
        }

        public void LogPerformance(string operationName, TimeSpan duration, bool success, string? additionalInfo = null)
        {
            var logLevel = success ? LogLevel.Information : LogLevel.Error;
            var message = $"Performance: {operationName} completed in {duration.TotalMilliseconds:F2}ms";

            if (!success && !string.IsNullOrEmpty(additionalInfo))
            {
                message += $" - Error: {additionalInfo}";
            }

            _logger.Log(logLevel, message);

            // Log slow operations as warnings
            if (duration.TotalMilliseconds > 1000) // More than 1 second
            {
                _logger.LogWarning("Slow operation detected: {OperationName} took {Duration}ms",
                    operationName, duration.TotalMilliseconds);
            }

            // Log very slow operations as errors
            if (duration.TotalMilliseconds > 5000) // More than 5 seconds
            {
                _logger.LogError("Very slow operation detected: {OperationName} took {Duration}ms",
                    operationName, duration.TotalMilliseconds);
            }
        }
    }
}
