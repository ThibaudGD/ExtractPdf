namespace PdfExtract.Services;

public interface IChunkHandler
{
    Dictionary<int, string> SplitBySize(string imagePath, int chunkWidth,
        int chunkHeight, int overlapMargin, int scale = 100, int quality = 100);
}