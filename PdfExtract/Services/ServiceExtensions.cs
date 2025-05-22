namespace PdfExtract.Services;

public static class ServiceExtensions
{
    public static void RegisterServices(this IServiceCollection services)
    {
        services.AddTransient<IPdfToImage, PdfToImage>();
        services.AddTransient<IChunkHandler, ChunkHandler>();
    }
}