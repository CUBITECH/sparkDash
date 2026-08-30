using Microsoft.Win32;

namespace SparkDash.DesktopTile;

internal sealed class StartupRegistration(string valueName)
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    internal bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var expectedCommand = GetCommand();
            return expectedCommand is not null &&
                key?.GetValue(valueName) is string registeredCommand &&
                string.Equals(registeredCommand, expectedCommand, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Windows startup registry key is unavailable.");
        if (!enabled)
        {
            key.DeleteValue(valueName, throwOnMissingValue: false);
            return;
        }

        var command = GetCommand()
            ?? throw new InvalidOperationException("The desktop tile executable path is unavailable.");
        key.SetValue(valueName, command, RegistryValueKind.String);
    }

    private static string? GetCommand()
    {
        return Environment.ProcessPath is string executable ? $"\"{executable}\"" : null;
    }
}
