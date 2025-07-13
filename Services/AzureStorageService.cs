using Azure.Storage.Blobs;
using CorpsAPI.DTOs;

namespace CorpsAPI.Services
{
    public class AzureStorageService
    {
        private readonly IConfiguration _configuration;

        public AzureStorageService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<string> UploadImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file provided.");

            var containerName = _configuration["AzureStorage:ContainerName"];
            var connectionString = _configuration["AzureStorage:ConnectionString"];

            var blobClient = new BlobContainerClient(connectionString, containerName);
            await blobClient.CreateIfNotExistsAsync();

            var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var blob = blobClient.GetBlobClient(fileName);

            using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, overwrite: true);

            return blob.Uri.ToString();
        }
    }
}
