namespace PdfExtract.Services;

public interface IPdfToImage
{
    Task<FileInfo> ConvertAsync(FileInfo pdf, int quality = 100, CancellationToken cancellationToken = default);
}