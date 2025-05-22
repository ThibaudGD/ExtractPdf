using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
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
    [Inject] public required IPdfToImage PdfToImage { get; set; }
    [Inject] public required IChunkHandler ChunkHandler { get; set; }
    [Inject] public required IStringLocalizer<Home> Localizer { get; set; }

    private Task OnCompletedAsync(IEnumerable<FluentInputFileEventArgs> files)
    {
        _file = files.FirstOrDefault();
        _progressPercent = 0;
        return Task.CompletedTask;
    }

    private async Task StartAsync()
    {
        _chunkProcessing = true;
        await InvokeAsync(StateHasChanged);
        if (_file?.LocalFile is null)
        {
            return;
        }

        var imgFileInfo = await PdfToImage.ConvertAsync(_file.LocalFile, _quality);

        var chunks = ChunkHandler.SplitBySize(imgFileInfo.FullName, _chunkSize, _chunkSize, _chunkOverlap, _chunkScale,
            _chunkQuality);
        _chunkProcessing = false;
        await InvokeAsync(StateHasChanged);
    }


    public void Dispose()
    {
        _file?.LocalFile?.Delete();
    }
}