namespace ReuploadMapToNadeo;

internal sealed record CommandLineOptions(
    string? OriginalMapPath,
    string? OriginalUid,
    string? NewMapPath,
    string? OutputPath,
    string? ResultJsonPath,
    bool NoUpload,
    bool AutoConfirm,
    bool ShowHelp)
{
    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Any(IsHelpOption))
        {
            return new(null, null, null, null, null, NoUpload: false, AutoConfirm: false, ShowHelp: true);
        }

        var positional = new List<string>(capacity: 2);
        string? originalUid = null;
        string? outputPath = null;
        string? resultJsonPath = null;
        bool noUpload = false;
        bool autoConfirm = false;
        bool optionsEnded = false;

        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];

            if (!optionsEnded && argument == "--")
            {
                optionsEnded = true;
                continue;
            }

            if (!optionsEnded && (argument == "--output" || argument == "-o"))
            {
                if (outputPath is not null)
                {
                    throw new CommandLineException("--output can only be specified once.");
                }

                if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    throw new CommandLineException("--output requires a file or directory path.");
                }

                outputPath = args[index];
                continue;
            }

            if (!optionsEnded && argument == "--original-uid")
            {
                if (originalUid is not null)
                {
                    throw new CommandLineException("--original-uid can only be specified once.");
                }

                if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    throw new CommandLineException("--original-uid requires a map UID.");
                }

                originalUid = args[index].Trim();
                continue;
            }

            if (!optionsEnded && argument.StartsWith("--original-uid=", StringComparison.Ordinal))
            {
                if (originalUid is not null)
                {
                    throw new CommandLineException("--original-uid can only be specified once.");
                }

                originalUid = argument["--original-uid=".Length..].Trim();
                if (string.IsNullOrWhiteSpace(originalUid))
                {
                    throw new CommandLineException("--original-uid requires a map UID.");
                }

                continue;
            }

            if (!optionsEnded && argument.StartsWith("--output=", StringComparison.Ordinal))
            {
                if (outputPath is not null)
                {
                    throw new CommandLineException("--output can only be specified once.");
                }

                outputPath = argument["--output=".Length..];
                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    throw new CommandLineException("--output requires a file or directory path.");
                }

                continue;
            }

            if (!optionsEnded && argument == "--result-json")
            {
                if (resultJsonPath is not null)
                {
                    throw new CommandLineException("--result-json can only be specified once.");
                }

                if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    throw new CommandLineException("--result-json requires a file path.");
                }

                resultJsonPath = args[index];
                continue;
            }

            if (!optionsEnded && argument.StartsWith("--result-json=", StringComparison.Ordinal))
            {
                if (resultJsonPath is not null)
                {
                    throw new CommandLineException("--result-json can only be specified once.");
                }

                resultJsonPath = argument["--result-json=".Length..];
                if (string.IsNullOrWhiteSpace(resultJsonPath))
                {
                    throw new CommandLineException("--result-json requires a file path.");
                }

                continue;
            }

            if (!optionsEnded && argument == "--no-upload")
            {
                noUpload = true;
                continue;
            }

            if (!optionsEnded && (argument == "--yes" || argument == "-y"))
            {
                autoConfirm = true;
                continue;
            }

            if (!optionsEnded && argument.StartsWith('-'))
            {
                throw new CommandLineException($"Unknown option '{argument}'.");
            }

            positional.Add(argument);
        }

        if (originalUid is null && positional.Count != 2)
        {
            throw new CommandLineException("Exactly two map paths are required: the original map and the new map.");
        }

        if (originalUid is not null && positional.Count != 1)
        {
            throw new CommandLineException(
                "When --original-uid is used, exactly one map path is required: the new map.");
        }

        return new(
            originalUid is null ? positional[0] : null,
            originalUid,
            originalUid is null ? positional[1] : positional[0],
            outputPath,
            resultJsonPath,
            noUpload,
            autoConfirm,
            ShowHelp: false);
    }

    public static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("ReuploadMapToNadeo");
        writer.WriteLine("Copies an existing map UID into a replacement map and updates that map on Nadeo.");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine("  ReuploadMapToNadeo <original.Map.Gbx> <new.Map.Gbx> [options]");
        writer.WriteLine("  ReuploadMapToNadeo --original-uid <uid> <new.Map.Gbx> [options]");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("      --original-uid <uid>");
        writer.WriteLine("                       Use an existing Nadeo map UID instead of an original map file.");
        writer.WriteLine("  -o, --output <path>  Prepared output file or existing directory.");
        writer.WriteLine("                       Default: ./out/<new map file name>");
        writer.WriteLine("      --no-upload      Prepare and verify the file without contacting Nadeo.");
        writer.WriteLine("      --result-json <path>");
        writer.WriteLine("                       Write a machine-readable success result.");
        writer.WriteLine("  -y, --yes            Skip the remote-update confirmation prompt.");
        writer.WriteLine("  -h, --help           Show this help text.");
        writer.WriteLine();
        writer.WriteLine("Credentials:");
        writer.WriteLine("  Set NADEO_LOGIN and NADEO_PASSWORD in the environment or a local .env file.");
        writer.WriteLine("  These must be Trackmania service-account credentials, not Ubisoft credentials.");
        writer.WriteLine("  Automation may instead set a short-lived NADEO_ACCESS_TOKEN.");
    }

    private static bool IsHelpOption(string argument)
    {
        return argument is "--help" or "-h" or "/?";
    }
}

internal sealed class CommandLineException(string message) : Exception(message);
