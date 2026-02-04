using System;
using System.Threading.Tasks;

namespace SaaSBase.Application.Services;

public interface IFileService
{
    Task<string> SaveFileAsync(byte[] fileData, string fileName, string folderPath);
    Task<bool> DeleteFileAsync(string filePath);
    Task<byte[]?> GetFileAsync(string filePath);
    string GetFileUrl(string filePath);
}
