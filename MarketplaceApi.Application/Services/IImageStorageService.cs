namespace MarketplaceApi.Application.Services;

public interface IImageStorageService
{
    /// <summary>
    /// Faz o upload de uma imagem no Object Storage e retorna a URL ou chave de acesso.
    /// </summary>
    Task<string> UploadImageAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Recupera o stream de uma imagem a partir de seu identificador/nome de arquivo.
    /// </summary>
    Task<Stream?> GetImageAsync(string fileName, CancellationToken ct = default);

    /// <summary>
    /// Remove uma imagem do Object Storage.
    /// </summary>
    Task<bool> DeleteImageAsync(string fileName, CancellationToken ct = default);
}
