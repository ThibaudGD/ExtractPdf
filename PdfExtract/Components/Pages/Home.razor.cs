using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using PdfExtract.Models;
using PdfExtract.Services;

namespace PdfExtract.Components.Pages;

public partial class Home : IDisposable
{
    private int _progressPercent;
    private FluentInputFileEventArgs? _file;
    private int _quality = 70;
    private int _chunkSize = 1500;
    private int _chunkOverlap = 75;
    private int _chunkScale = 50;
    private int _chunkQuality = 50;
    private bool _chunkProcessing;
    private IQueryable<Response> _results = Array.Empty<Response>().AsQueryable();
    private int _step;
    private int _totalChunks;
    private int _currentChunk;
    [Inject] public required IPdfToImage PdfToImage { get; set; }
    [Inject] public required IChunkHandler ChunkHandler { get; set; }
    [Inject] public required IVisionHandler VisionHandler { get; set; }
    [Inject] public required IStringLocalizer<Home> Localizer { get; set; }

    private Task OnCompletedAsync(IEnumerable<FluentInputFileEventArgs> files)
    {
        _file = files.FirstOrDefault();
        _progressPercent = 0;
        _step++;
        return Task.CompletedTask;
    }

    private async Task StartAsync()
    {
        _chunkProcessing = true;
        _results = Array.Empty<Response>().AsQueryable();
        await Task.Delay(100);
        if (_file?.LocalFile is null)
        {
            return;
        }

        var imgFileInfo = await PdfToImage.ConvertAsync(_file.LocalFile, _quality);

        var chunks = ChunkHandler.SplitBySize(imgFileInfo.FullName, _chunkSize, _chunkSize, _chunkOverlap, _chunkScale,
            _chunkQuality);
        var outputs = new List<string>();

        _step++;
        _totalChunks = chunks.Count;
        await InvokeAsync(StateHasChanged);
        foreach (var chunk in chunks)
        {
            _currentChunk++;
            await InvokeAsync(StateHasChanged);

            var fileInfo = new FileInfo(chunk.Value);
            if (!fileInfo.Exists) continue;
            if (_currentChunk <= 3)
            {
                var responses = await VisionHandler.GetTextFromVisionAsync(fileInfo).ToListAsync();
                outputs.AddRange(responses);
            }

            fileInfo.Delete();
        }

        _step++;
        await InvokeAsync(StateHasChanged);

        var result = await VisionHandler.GetTextAsync(outputs);
        _results = result.AsQueryable();
        _chunkProcessing = false;

        _step = 0;
        await InvokeAsync(StateHasChanged);
    }


    public void Dispose()
    {
        _file?.LocalFile?.Delete();
    }
}