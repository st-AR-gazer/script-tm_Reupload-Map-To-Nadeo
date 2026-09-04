using ManiaAPI.NadeoAPI;
using ManiaAPI.NadeoAPI.Extensions.Gbx;

namespace ReuploadMapToNadeo;

internal static class CliApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        using var cancellation = new CancellationTokenSource();

        void CancelHandler(object? _, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        }

        Console.CancelKeyPress += CancelHandler;

        try
        {
            return await RunCoreAsync(args, cancellation.Token);
        }
        catch (CommandLineException ex)
        {
            WriteError(ex.Message);
            Console.Error.WriteLine();
            CommandLineOptions.WriteUsage(Console.Error);
            return 2;
        }
        catch (OperationCanceledException)
        {
            WriteError("Operation cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            WriteError(ex.Message);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= CancelHandler;
        }
    }

    private static async Task<int> RunCoreAsync(string[] args, CancellationToken cancellationToken)
    {
        var options = CommandLineOptions.Parse(args);
        if (options.ShowHelp)
        {
            CommandLineOptions.WriteUsage(Console.Out);
            return 0;
        }

        LoadEnvironmentFiles();

        string? originalPath = options.OriginalMapPath is null ? null : Path.GetFullPath(options.OriginalMapPath);
        string newPath = Path.GetFullPath(options.NewMapPath!);
        string outputPath = ResolveOutputPath(options.OutputPath, newPath);

        if (originalPath is not null && PathEquals(originalPath, outputPath))
        {
            throw new CommandLineException("The output cannot overwrite the original map.");
        }

        Console.WriteLine("Preparing replacement map...");
        var result = originalPath is null
            ? MapUidRewriter.RewriteFromUid(options.OriginalUid!, newPath, outputPath)
            : MapUidRewriter.Rewrite(originalPath, newPath, outputPath);

        Console.WriteLine($"Original map : {result.OriginalMapName ?? "(UID supplied directly)"}");
        Console.WriteLine($"Original UID : {result.OriginalUid}");
        Console.WriteLine($"New map      : {result.NewMapName}");
        Console.WriteLine($"Previous UID : {result.PreviousUid}");
        Console.WriteLine($"Prepared file: {result.OutputPath}");
        Console.WriteLine("Verified     : GBX header, body, and XML UID all match");

        if (options.NoUpload)
        {
            await ResultFile.WriteAsync(
                options.ResultJsonPath,
                new ReuploadResult(
                    result.OriginalMapName ?? "(UID supplied directly)",
                    result.NewMapName,
                    result.OriginalUid,
                    result.PreviousUid,
                    Uploaded: false,
                    RemoteMapId: null,
                    RemoteMapName: null),
                cancellationToken);
            Console.WriteLine("Upload skipped (--no-upload). The prepared file is ready for inspection.");
            return 0;
        }

        Console.WriteLine("Authenticating with Nadeo Services...");
        using var nadeoServices = await CreateNadeoServicesAsync(cancellationToken);

        Console.WriteLine("Resolving the remote map from its original UID...");
        var remoteMap = await nadeoServices.GetMapInfoAsync(result.OriginalUid, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Nadeo has no map with UID '{result.OriginalUid}'. " +
                "The original map must already be uploaded before it can be replaced.");

        Console.WriteLine($"Remote map   : {remoteMap.Name}");
        Console.WriteLine($"Remote map ID: {remoteMap.MapId}");

        if (!options.AutoConfirm && !ConsolePrompts.ConfirmRemoteUpdate(remoteMap.Name, result.OriginalUid))
        {
            Console.WriteLine("Remote update cancelled. The prepared local file was kept.");
            return 0;
        }

        Console.WriteLine("Uploading replacement to Nadeo...");
        var updatedMap = await nadeoServices.UpdateMapAsync(
            remoteMap.MapId,
            result.OutputPath,
            cancellationToken);

        if (updatedMap.MapId != remoteMap.MapId ||
            !string.Equals(updatedMap.MapUid, result.OriginalUid, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Nadeo returned an unexpected map identity after the update. " +
                $"Expected {remoteMap.MapId}/{result.OriginalUid}, " +
                $"received {updatedMap.MapId}/{updatedMap.MapUid}.");
        }

        Console.WriteLine($"Updated successfully: {updatedMap.Name} ({updatedMap.MapUid})");
        Console.WriteLine("The existing Nadeo map identity and leaderboard were preserved.");
        await ResultFile.WriteAsync(
            options.ResultJsonPath,
            new ReuploadResult(
                result.OriginalMapName ?? remoteMap.Name,
                result.NewMapName,
                result.OriginalUid,
                result.PreviousUid,
                Uploaded: true,
                RemoteMapId: updatedMap.MapId.ToString(),
                RemoteMapName: updatedMap.Name),
            cancellationToken);
        return 0;
    }

    private static async Task<NadeoServices> CreateNadeoServicesAsync(CancellationToken cancellationToken)
    {
        string? accessToken = ReadSetting("NADEO_ACCESS_TOKEN");
        if (accessToken is not null)
        {
            var client = new HttpClient(new NadeoAccessTokenHandler(accessToken, new HttpClientHandler()));
            return new NadeoServices(client, new NadeoAPIHandler(), automaticallyAuthorize: false);
        }

        var credentials = NadeoCredentials.Read();
        var services = new NadeoServices();
        try
        {
            await services.AuthorizeAsync(credentials.Login, credentials.Password, cancellationToken);
            return services;
        }
        catch
        {
            services.Dispose();
            throw;
        }
    }

    private static string? ReadSetting(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void LoadEnvironmentFiles()
    {
        var loadedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string candidate in new[]
                 {
                     Path.Combine(AppContext.BaseDirectory, ".env"),
                     Path.Combine(Directory.GetCurrentDirectory(), ".env")
                 })
        {
            string fullPath = Path.GetFullPath(candidate);
            if (loadedPaths.Add(fullPath))
            {
                EnvFile.Load(fullPath);
            }
        }
    }

    private static string ResolveOutputPath(string? configuredOutputPath, string newMapPath)
    {
        if (string.IsNullOrWhiteSpace(configuredOutputPath))
        {
            return Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                "out",
                Path.GetFileName(newMapPath)));
        }

        string outputPath = Path.GetFullPath(configuredOutputPath);
        if (Directory.Exists(outputPath) || Path.EndsInDirectorySeparator(configuredOutputPath))
        {
            outputPath = Path.Combine(outputPath, Path.GetFileName(newMapPath));
        }

        return Path.GetFullPath(outputPath);
    }

    private static bool PathEquals(string left, string right)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static void WriteError(string message)
    {
        Console.Error.WriteLine($"Error: {message}");
    }
}
