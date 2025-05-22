using Azure;
using Azure.AI.Inference;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace PdfExtract.Services;

public static class ServiceExtensions
{
    public static void RegisterServices(this IServiceCollection services)
    {
        services.AddTransient<IPdfToImage, PdfToImage>();
        services.AddTransient<IChunkHandler, ChunkHandler>();
        services.AddTransient<IVisionHandler, VisionHandler>();
        //services.AddTransient<IChatClient>(_ => new OllamaApiClient("http://localhost:11434", "gemma3:12b"));
        services.AddTransient<IChatClient>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var endpoint = cfg.GetValue<string>("MISTRAL_ENDPOINT") ?? throw new ArgumentNullException("MISTRAL_ENDPOINT");
            var key = cfg.GetValue<string>("MISTRAL_API_KEY") ?? throw new ArgumentNullException("MISTRAL_API_KEY");
            var model = cfg.GetValue<string>("MISTRAL_MODEL") ?? throw new ArgumentNullException("MISTRAL_MODEL");
            return new ChatCompletionsClient(new Uri(endpoint), new AzureKeyCredential(key)).AsIChatClient(model);
        });
    }
}