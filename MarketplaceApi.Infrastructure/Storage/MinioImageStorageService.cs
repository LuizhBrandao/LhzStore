using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using MarketplaceApi.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MarketplaceApi.Infrastructure.Storage;

public class MinioImageStorageService : IImageStorageService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MinioImageStorageService> _logger;
    private readonly string _bucketName;
    private readonly string? _endpoint;
    private readonly string _localFallbackDir;
    private IAmazonS3? _s3Client;
    private bool _bucketInitialized;

    public MinioImageStorageService(IConfiguration configuration, ILogger<MinioImageStorageService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _bucketName = _configuration["Storage:BucketName"] ?? "lhzstore-cards";
        _endpoint = _configuration["Storage:S3Endpoint"];
        _localFallbackDir = Path.Combine(AppContext.BaseDirectory, "uploads", "cards");

        InitializeS3Client();
    }

    private void InitializeS3Client()
    {
        if (string.IsNullOrWhiteSpace(_endpoint))
        {
            _logger.LogWarning("Endpoint S3/MinIO não configurado. Utilizando fallback para armazenamento local em disco: {Dir}", _localFallbackDir);
            Directory.CreateDirectory(_localFallbackDir);
            return;
        }

        try
        {
            var accessKey = _configuration["Storage:AccessKey"] ?? "admin";
            var secretKey = _configuration["Storage:SecretKey"] ?? "password123";

            var config = new AmazonS3Config
            {
                ServiceURL = _endpoint,
                ForcePathStyle = true, // Obrigatório para MinIO
                UseHttp = _endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            };

            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            _s3Client = new AmazonS3Client(credentials, config);
            _logger.LogInformation("Cliente S3/MinIO inicializado com sucesso para o endpoint: {Endpoint}", _endpoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao inicializar cliente S3/MinIO. Ativando fallback para armazenamento local.");
            _s3Client = null;
            Directory.CreateDirectory(_localFallbackDir);
        }
    }

    private async Task EnsureBucketExistsAsync(CancellationToken ct)
    {
        if (_s3Client == null || _bucketInitialized)
            return;

        try
        {
            var exists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, _bucketName);
            if (!exists)
            {
                await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = _bucketName }, ct);
                _logger.LogInformation("Bucket '{Bucket}' criado com sucesso no MinIO.", _bucketName);
            }
            _bucketInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível verificar/criar bucket '{Bucket}'. Tentativas continuarão sob demanda.", _bucketName);
        }
    }

    public async Task<string> UploadImageAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var sanitizedFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";

        if (_s3Client != null)
        {
            try
            {
                await EnsureBucketExistsAsync(ct);

                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = sanitizedFileName,
                    InputStream = content,
                    ContentType = contentType
                };

                await _s3Client.PutObjectAsync(putRequest, ct);
                _logger.LogInformation("Imagem enviada ao MinIO com sucesso: {File}", sanitizedFileName);

                return $"/api/images/{sanitizedFileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no upload para MinIO. Salvando localmente como contingência.");
            }
        }

        // Contingência / Armazenamento Local
        Directory.CreateDirectory(_localFallbackDir);
        var localPath = Path.Combine(_localFallbackDir, sanitizedFileName);
        
        if (content.CanSeek)
            content.Position = 0;

        using (var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write))
        {
            await content.CopyToAsync(fileStream, ct);
        }

        return $"/api/images/{sanitizedFileName}";
    }

    public async Task<Stream?> GetImageAsync(string fileName, CancellationToken ct = default)
    {
        var safeFileName = Path.GetFileName(fileName);

        if (_s3Client != null)
        {
            try
            {
                var response = await _s3Client.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = safeFileName
                }, ct);

                var memoryStream = new MemoryStream();
                await response.ResponseStream.CopyToAsync(memoryStream, ct);
                memoryStream.Position = 0;
                return memoryStream;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Imagem não encontrada no MinIO: {File}", safeFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar imagem no MinIO: {File}", safeFileName);
            }
        }

        // Tenta localmente se não encontrou no S3
        var localPath = Path.Combine(_localFallbackDir, safeFileName);
        if (File.Exists(localPath))
        {
            var memoryStream = new MemoryStream();
            using (var fileStream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await fileStream.CopyToAsync(memoryStream, ct);
            }
            memoryStream.Position = 0;
            return memoryStream;
        }

        return null;
    }

    public async Task<bool> DeleteImageAsync(string fileName, CancellationToken ct = default)
    {
        var safeFileName = Path.GetFileName(fileName);
        var deleted = false;

        if (_s3Client != null)
        {
            try
            {
                await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = safeFileName
                }, ct);
                deleted = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover imagem do MinIO: {File}", safeFileName);
            }
        }

        var localPath = Path.Combine(_localFallbackDir, safeFileName);
        if (File.Exists(localPath))
        {
            File.Delete(localPath);
            deleted = true;
        }

        return deleted;
    }
}
