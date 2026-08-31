using CommunityToolkit.Maui.Storage;
using SecondBrain.Application.NotionAudit;

namespace SecondBrain.Presentation;

public sealed class MauiNotionExportSourcePicker : INotionExportSourcePicker
{
    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        var result = await FolderPicker.Default.PickAsync(cancellationToken);
        if (result.IsSuccessful)
        {
            return result.Folder.Path;
        }

        if (result.Exception is not null)
        {
            throw result.Exception;
        }

        return null;
    }

    public async Task<string?> PickArchiveAsync(CancellationToken cancellationToken = default)
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Choose a Notion ZIP archive or supported manifest"
        });
        cancellationToken.ThrowIfCancellationRequested();
        return file?.FullPath;
    }
}
