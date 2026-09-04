using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReuploadMapToNadeo;

internal static class ResultFile
{
    public static async Task WriteAsync(string? path, ReuploadResult result, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("The result JSON path has no parent directory.");
        Directory.CreateDirectory(directory);

        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous);
            await JsonSerializer.SerializeAsync(
                stream,
                result,
                ReuploadResultJsonContext.Default.ReuploadResult,
                cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Close();
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

internal sealed record ReuploadResult(
    string OriginalMapName,
    string ReplacementMapName,
    string OriginalUid,
    string PreviousUid,
    bool Uploaded,
    string? RemoteMapId,
    string? RemoteMapName);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(ReuploadResult))]
internal sealed partial class ReuploadResultJsonContext : JsonSerializerContext
{
}
