using System.Diagnostics;
using System.Text;

namespace LiveAudioTranslator.App.Services.Languages;

public sealed class WindowsLanguagePackService : ILanguagePackService
{
    public async Task<bool> IsLanguageInstalledAsync(string cultureCode, CancellationToken cancellationToken = default)
    {
        var command = $"Import-Module LanguagePackManagement; $lang = Get-InstalledLanguage -Language {cultureCode} -ErrorAction SilentlyContinue; if ($null -ne $lang) {{ Write-Output 'true' }} else {{ Write-Output 'false' }}";
        var output = await RunPowerShellAsync(command, elevate: false, cancellationToken);
        return string.Equals(output.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> InstallLanguageAsync(string cultureCode, CancellationToken cancellationToken = default)
    {
        var command = $"Import-Module LanguagePackManagement; Install-Language -Language {cultureCode} -ErrorAction Stop | Out-Null";
        await RunPowerShellAsync(command, elevate: true, cancellationToken);
        return await IsLanguageInstalledAsync(cultureCode, cancellationToken);
    }

    private static async Task<string> RunPowerShellAsync(string command, bool elevate, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
            UseShellExecute = elevate,
            Verb = elevate ? "runas" : string.Empty,
            RedirectStandardOutput = !elevate,
            RedirectStandardError = !elevate,
            CreateNoWindow = !elevate
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        if (elevate)
        {
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("언어팩 설치가 완료되지 않았습니다.");
            }

            return string.Empty;
        }

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                standardOutput.AppendLine(eventArgs.Data);
            }
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                standardError.AppendLine(eventArgs.Data);
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = standardError.ToString().Trim();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "언어팩 상태 확인에 실패했습니다." : error);
        }

        return standardOutput.ToString();
    }
}
