using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace SaaSBase.Api.Infrastructure;

public class CorrelationIdMiddleware
{
	private const string HeaderName = "X-Correlation-ID";
	private readonly RequestDelegate _next;

	public CorrelationIdMiddleware(RequestDelegate next)
	{
		_next = next;
	}

	public async Task Invoke(HttpContext context)
	{
		var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing)
			&& Guid.TryParse(existing, out var parsed)
			? parsed.ToString()
			: Guid.NewGuid().ToString();

		context.Response.OnStarting(() =>
		{
			context.Response.Headers[HeaderName] = correlationId;
			return Task.CompletedTask;
		});

		using (LogContext.PushProperty("CorrelationId", correlationId))
		{
			await _next(context);
		}
	}
}


