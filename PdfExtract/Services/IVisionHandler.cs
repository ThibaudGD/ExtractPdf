using PdfExtract.Models;

namespace PdfExtract.Services;

public interface IVisionHandler
{
    IAsyncEnumerable<string> GetTextFromVisionAsync(FileInfo file);
    Task<IEnumerable<Response>> GetTextAsync(IEnumerable<string> files);
}