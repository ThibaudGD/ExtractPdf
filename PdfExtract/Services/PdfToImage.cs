using PDFtoImage;
using SkiaSharp;

namespace PdfExtract.Services;

public sealed class PdfToImage : IPdfToImage
{
    public Task<FileInfo> ConvertAsync(FileInfo pdf, int quality = 100, CancellationToken cancellationToken = default)
    {
        var pdfStream = File.OpenRead(pdf.FullName);
        var tmpFolder = Path.GetTempPath();
        var tmpFile = new FileInfo(Path.Combine(tmpFolder, Path.GetRandomFileName()));
        var imgStream = File.Create(tmpFile.FullName);
                
#pragma warning disable CA1416
        var im = Conversion.ToImage(
            pdfStream,
            Index.Start);
#pragma warning restore CA1416
        
                
        im.Encode(
            imgStream,
            SKEncodedImageFormat.Jpeg, quality);
        
        imgStream.Close();
        
        return Task.FromResult(tmpFile);
    }
}