using SkiaSharp;

namespace PdfExtract.Services;

public sealed class ChunkHandler : IChunkHandler
{
    public Dictionary<int, string> SplitBySize(string imagePath, int chunkWidth,
        int chunkHeight, int overlapMargin, int scale = 100, int quality = 100)
    {
        using var sourceStream = File.OpenRead(imagePath);
        using var originalBitmap = SKBitmap.Decode(sourceStream);
        using var source = new SKBitmap(
            originalBitmap.Width * scale / 100,
            originalBitmap.Height * scale / 100);
        originalBitmap.ScalePixels(source, new SKSamplingOptions(SKCubicResampler.Mitchell));


        var tiles = new Dictionary<int, string>();

        var stepX = chunkWidth - overlapMargin;
        var stepY = chunkHeight - overlapMargin;
        var index = 0;

        for (var y = 0; y < source.Height; y += stepY)
        {
            for (var x = 0; x < source.Width; x += stepX)
            {
                var actualWidth = Math.Min(chunkWidth, source.Width - x);
                var actualHeight = Math.Min(chunkHeight, source.Height - y);

                var cropRect = new SKRectI(x, y, x + actualWidth, y + actualHeight);
                using var tile = new SKBitmap(actualWidth, actualHeight);

                using (var canvas = new SKCanvas(tile))
                {
                    var destRect = new SKRectI(0, 0, actualWidth, actualHeight);
                    canvas.DrawBitmap(source, cropRect, destRect);
                }

                var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                using var image = SKImage.FromBitmap(tile);
                using var data = image.Encode(SKEncodedImageFormat.Webp, quality);
                using var stream = File.OpenWrite(filePath);
                data.SaveTo(stream);
                stream.Close();

                tiles.Add(index++, filePath);
            }
        }

        return tiles;
    }
}