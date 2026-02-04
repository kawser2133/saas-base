using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SaaSBase.Application.Services;

/// <summary>
/// Generic service for monitoring any service performance metrics
/// </summary>
public interface IPerformanceService
{
    /// <summary>
    /// Monitor method execution time and log performance metrics
    /// </summary>
    Task<T> MonitorAsync<T>(string operationName, Func<Task<T>> operation);

    /// <summary>
    /// Monitor method execution time and log performance metrics (void return)
    /// </summary>
    Task MonitorAsync(string operationName, Func<Task> operation);

    /// <summary>
    /// Log performance metrics
    /// </summary>
    void LogPerformance(string operationName, TimeSpan duration, bool success, string? additionalInfo = null);
}