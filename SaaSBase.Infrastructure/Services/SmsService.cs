using System;
using System.Threading.Tasks;
using SaaSBase.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace SaaSBase.Infrastructure.Services;

public class SmsService : ISmsService
{
	private readonly ILogger<SmsService> _logger;
	private readonly IConfiguration _configuration;

	public SmsService(ILogger<SmsService> logger, IConfiguration configuration)
	{
		_logger = logger;
		_configuration = configuration;
	}

	public async Task<bool> SendSmsAsync(string phoneNumber, string message)
	{
		try
		{
			var accountSid = _configuration["Twilio:AccountSid"];
			var authToken = _configuration["Twilio:AuthToken"];
			var fromPhoneNumber = _configuration["Twilio:FromPhoneNumber"];

			if (string.IsNullOrEmpty(accountSid) || string.IsNullOrEmpty(authToken) || string.IsNullOrEmpty(fromPhoneNumber))
			{
				_logger.LogWarning("Twilio configuration missing, SMS not sent to {PhoneNumber}", phoneNumber);
				return false;
			}

			TwilioClient.Init(accountSid, authToken);

			var messageResource = await MessageResource.CreateAsync(
				body: message,
				from: new Twilio.Types.PhoneNumber(fromPhoneNumber),
				to: new Twilio.Types.PhoneNumber(phoneNumber)
			);

			if (messageResource.Status == MessageResource.StatusEnum.Sent || 
			    messageResource.Status == MessageResource.StatusEnum.Queued)
			{
				_logger.LogInformation("SMS sent successfully to {PhoneNumber}: {Message}", phoneNumber, message);
				return true;
			}
			else
			{
				_logger.LogError("Failed to send SMS to {PhoneNumber}. Status: {Status}", phoneNumber, messageResource.Status);
				return false;
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to send SMS to {PhoneNumber}", phoneNumber);
			return false;
		}
	}

	public async Task<bool> SendMfaCodeSmsAsync(string phoneNumber, string code)
	{
		var message = $"Your MFA verification code is: {code}. This code expires in 10 minutes.";
		return await SendSmsAsync(phoneNumber, message);
	}
}
