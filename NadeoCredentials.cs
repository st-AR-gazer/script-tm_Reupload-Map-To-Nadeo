using System.Text;

namespace ReuploadMapToNadeo;

internal sealed record NadeoCredentials(string Login, string Password)
{
    public static NadeoCredentials Read()
    {
        string login = ReadSetting("NADEO_LOGIN") ?? PromptForLogin();
        string password = ReadSetting("NADEO_PASSWORD") ?? ConsolePrompts.ReadPassword(
            "Nadeo service-account password: ");

        if (string.IsNullOrWhiteSpace(login))
        {
            throw new InvalidOperationException("NADEO_LOGIN cannot be empty.");
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("NADEO_PASSWORD cannot be empty.");
        }

        return new NadeoCredentials(login.Trim(), password);
    }

    private static string PromptForLogin()
    {
        if (Console.IsInputRedirected)
        {
            throw new InvalidOperationException(
                "NADEO_LOGIN is missing and cannot be prompted for because input is redirected.");
        }

        Console.Write("Nadeo service-account login: ");
        return Console.ReadLine() ?? string.Empty;
    }

    private static string? ReadSetting(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

internal static class ConsolePrompts
{
    public static bool ConfirmRemoteUpdate(string mapName, string mapUid)
    {
        if (Console.IsInputRedirected)
        {
            throw new InvalidOperationException(
                "Remote-update confirmation requires an interactive terminal. Pass --yes to confirm non-interactively.");
        }

        Console.WriteLine();
        Console.WriteLine($"This will replace the existing Nadeo map '{mapName}' ({mapUid}).");
        Console.Write("Continue? [y/N] ");

        ConsoleKey key = Console.ReadKey(intercept: true).Key;
        Console.WriteLine();
        return key == ConsoleKey.Y;
    }

    public static string ReadPassword(string prompt)
    {
        if (Console.IsInputRedirected)
        {
            throw new InvalidOperationException(
                "NADEO_PASSWORD is missing and cannot be prompted for because input is redirected.");
        }

        Console.Write(prompt);
        var password = new StringBuilder();

        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return password.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }
    }
}
