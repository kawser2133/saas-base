using System.Threading.Tasks;

namespace SaaSBase.Application.Services;

public interface ISmsService
{
	Task<bool> SendSmsAsync(string phoneNumber, string message);
	Task<bool> SendMfaCodeSmsAsync(string phoneNumber, string code);
}
