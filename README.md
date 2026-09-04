# Reupload Map To Nadeo

A .NET command-line tool that takes two Trackmania 2020 maps:

1. an **original map**
2. a **new map** whose contents should replace the original

The tool copies the original map UID into the new map, verifies that the UID is consistent in the saved GBX header, body, and XML header, then updates the existing map through `ManiaAPI.NadeoAPI`.

The input files are left untouched by default. The prepared replacement is written to `out/<new map file name>`.

## Important behavior

This tool calls `UpdateMapAsync`. Updating keeps the existing Nadeo map ID and leaderboard.

The original map must already exist on Nadeo, and the authenticated Trackmania account must be allowed to update it.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer
- A [Trackmania service account](https://www.trackmania.com/player/service-account) when running the CLI directly with a login and password

The standalone CLI supports ManiaAPI's service-account login, or a short-lived `NadeoServices` token supplied by trusted automation through `NADEO_ACCESS_TOKEN`.

## Configure standalone credentials

Copy `.env.example` to `.env` and enter the service-account credentials:

```dotenv
NADEO_LOGIN=your_service_account_login
NADEO_PASSWORD=your_service_account_password
```

You can set the same environment variables outside a file instead. If either value is missing in an interactive terminal, the tool prompts for it and hides password input.

## Run

```powershell
dotnet run -- "C:\Maps\Original.Map.Gbx" "C:\Maps\New.Map.Gbx"
```

The tool authenticates, shows the matching remote map, and asks for confirmation immediately before the update.

Prepare and verify the replacement without contacting Nadeo:

```powershell
.\ReuploadMapToNadeo.exe "C:\Maps\Original.Map.Gbx" "C:\Maps\New.Map.Gbx" --no-upload
```

Choose an output file or existing directory:

```powershell
.\ReuploadMapToNadeo.exe "C:\Maps\Original.Map.Gbx" "C:\Maps\New.Map.Gbx" --output "C:\Maps\Prepared.Map.Gbx"
```

Skip the final confirmation, for example in automation:

```powershell
.\ReuploadMapToNadeo.exe "C:\Maps\Original.Map.Gbx" "C:\Maps\New.Map.Gbx" --yes
```

Show all options:

```powershell
.\ReuploadMapToNadeo.exe --help
```

## Publish the standalone Windows runtime

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1
```

The deterministic NativeAOT publish is written to `artifacts/win-x64`. Keep
`ReuploadMapToNadeo.exe` and `liblzo2.dll` together, then run:

```powershell
.\artifacts\win-x64\ReuploadMapToNadeo.exe "C:\Maps\Original.Map.Gbx" "C:\Maps\New.Map.Gbx"
```
