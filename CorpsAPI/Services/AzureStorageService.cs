using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace CorpsAPI.Services
{
    public class AzureStorageService
    {
        private readonly IConfiguration _configuration;

        public AzureStorageService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private (string ConnectionString, string ContainerName) GetStorageSettings()
        {
            // Prefer explicit app settings, but allow Azure Functions fallback.
            var connectionString =
                _configuration["AzureStorage:ConnectionString"] ??
                _configuration["AzureWebJobsStorage"];

            var containerName = _configuration["AzureStorage:ContainerName"];
            if (string.IsNullOrWhiteSpace(containerName))
                containerName = "images";

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Azure storage connection string is not configured.");

            return (connectionString, containerName);
        }

        public async Task<string> UploadImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file provided.");

            var (connectionString, containerName) = GetStorageSettings();
            var containerClient   = new BlobContainerClient(connectionString, containerName);

            await containerClient.CreateIfNotExistsAsync();

            var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var blob      = containerClient.GetBlobClient(fileName);

            using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, overwrite: true);

            return blob.Uri.ToString();
        }

        /// <summary>
        /// Deletes a blob given its full URL, if it exists.
        /// </summary>
        public async Task DeleteImageAsync(string blobUrl)
        {
            if (string.IsNullOrWhiteSpace(blobUrl))
                return;

            // Extract the blob name from the URL
            var uri = new Uri(blobUrl);
            var blobName = Path.GetFileName(uri.LocalPath);

            var (connectionString, containerName) = GetStorageSettings();
            var containerClient = new BlobContainerClient(connectionString, containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            await blobClient.DeleteIfExistsAsync();
        }
    }
}
