using SaaSBase.Application.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace SaaSBase.Infrastructure.Services;

public class FileService : IFileService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly string _basePath;

    public FileService(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;

        // If WebRootPath exists, use it directly (it already points to wwwroot)
        // Otherwise use ContentRootPath and append wwwroot
        if (!string.IsNullOrEmpty(_environment.WebRootPath))
        {
            _basePath = Path.Combine(_environment.WebRootPath, "media");
        }
        else
        {
            _basePath = Path.Combine(_environment.ContentRootPath, "wwwroot", "media");
        }
    }

    public async Task<string> SaveFileAsync(byte[] fileData, string fileName, string folderPath)
    {
        try
        {
            // Create directory if it doesn't exist
            var fullDirectoryPath = Path.Combine(_basePath, folderPath);
            Directory.CreateDirectory(fullDirectoryPath);

            // Generate unique filename to avoid conflicts
            var fileExtension = Path.GetExtension(fileName);
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var fullFilePath = Path.Combine(fullDirectoryPath, uniqueFileName);

            // Save file
            await File.WriteAllBytesAsync(fullFilePath, fileData);

            // Return relative path for database storage
            return Path.Combine(folderPath, uniqueFileName).Replace("\\", "/");
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Failed to save file");
        }
    }

    public async Task<bool> DeleteFileAsync(string filePath)
    {
        try
        {
            var fullFilePath = Path.Combine(_basePath, filePath);
            if (File.Exists(fullFilePath))
            {
                File.Delete(fullFilePath);
                return true;
            }
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<byte[]?> GetFileAsync(string filePath)
    {
        try
        {
            var fullFilePath = Path.Combine(_basePath, filePath);
            if (File.Exists(fullFilePath))
            {
                return await File.ReadAllBytesAsync(fullFilePath);
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public string GetFileUrl(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return string.Empty;

        // Return URL path for frontend access
        return $"/media/{filePath}";
    }
}
