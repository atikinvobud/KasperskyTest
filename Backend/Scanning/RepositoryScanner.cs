
using System.Text.RegularExpressions;
using Backend.Models;
using LibGit2Sharp;

namespace Backend.Scanning;

public class RepositoryScanner : IRepositoryScanner
{
    private const int MaxFiles = 10_000;
    private const long MaxFileSize = 1_048_576;
    private const int MaxDeleteAttempts = 5;

    private static readonly ScanRule[] Rules =
    [
        new ScanRule
        {
            Name = "Weak MD5 hash",
            Criticality = CriticalityLevel.Medium,
            Pattern = @"\bMD5\.Create\s*\(",
            Description = "MD5 не рекомендуется для криптографической защиты."
        },
        new ScanRule
        {
            Name = "Weak SHA1 hash",
            Criticality = CriticalityLevel.Medium,
            Pattern = @"\bSHA1\.Create\s*\(",
            Description = "SHA1 не рекомендуется для криптографической защиты."
        },
        new ScanRule
        {
            Name = "Potential hardcoded password",
            Criticality = CriticalityLevel.High,
            Pattern = """(?i)\b(password|passwd|secret|apiKey)\s*=\s*"[^"]{4,}""",
            Description = "Возможно, секретное значение хранится прямо в коде."
        },
        new ScanRule
        {
            Name = "Potential SQL injection",
            Criticality = CriticalityLevel.High,
            Pattern = @"\b(ExecuteSqlRaw|ExecuteSqlCommand)\s*\(\s*\$""",
            Description = "Интерполяция строки в SQL-команде может привести к SQL-инъекции."
        },
        new ScanRule
        {
            Name = "Insecure process execution",
            Criticality = CriticalityLevel.High,
            Pattern = @"\bProcess\.Start\s*\(",
            Description = "Запуск внешнего процесса может быть опасен при использовании недоверенного ввода."
        },
        new ScanRule
        {
            Name = "Dangerous binary deserialization",
            Criticality = CriticalityLevel.Critical,
            Pattern = @"\bBinaryFormatter\b",
            Description = "BinaryFormatter небезопасен для десериализации недоверенных данных."
        },
        new ScanRule
        {
            Name = "TLS certificate validation disabled",
            Criticality = CriticalityLevel.High,
            Pattern = @"ServerCertificateCustomValidationCallback\s*=\s*\(\s*[^)]*\)\s*=>\s*true",
            Description = "Отключение проверки TLS-сертификата позволяет принимать недоверенные сертификаты."
        },
        new ScanRule
        {
            Name = "Weak random number generator",
            Criticality = CriticalityLevel.Medium,
            Pattern = @"\bnew\s+Random\s*\(",
            Description = "System.Random не подходит для генерации криптографических секретов."
        },
        new ScanRule
        {
            Name = "Potential disabled TLS validation",
            Criticality = CriticalityLevel.High,
            Pattern = @"DangerousAcceptAnyServerCertificateValidator",
            Description = "Используется обработчик, принимающий любой TLS-сертификат."
        }
    ];

    private readonly RepositoryUrlValidator _urlValidator;

    public RepositoryScanner(RepositoryUrlValidator urlValidator)
    {
        _urlValidator = urlValidator;
    }

    public async Task<List<ScanFinding>> ScanAsync(
        string repositoryUrl,
        CancellationToken cancellationToken)
    {
        var findings = new List<ScanFinding>();

        var temporaryPath = Path.Combine(
            Path.GetTempPath(),
            $"vulnerability-scan-{Guid.NewGuid():N}");

        try
        {
            if (!await _urlValidator.IsValidAsync(
                    repositoryUrl,
                    cancellationToken))
            {
                throw new ArgumentException(
                    "URL репозитория не прошёл проверку безопасности.",
                    nameof(repositoryUrl));
            }

            cancellationToken.ThrowIfCancellationRequested();

            Console.WriteLine($"Начало клонирования: {repositoryUrl}");

            // LibGit2Sharp выполняет клонирование синхронно.
            // Токен проверяется до и после клонирования.
            await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    Repository.Clone(
                        repositoryUrl,
                        temporaryPath,
                        new CloneOptions
                        {
                            IsBare = false
                        });

                    cancellationToken.ThrowIfCancellationRequested();
                },
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            Console.WriteLine(
                "Клонирование завершено. Начинаем анализ файлов.");

            var filesScanned = 0;

            foreach (var file in EnumerateCSharpFiles(temporaryPath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (++filesScanned > MaxFiles)
                {
                    Console.WriteLine(
                        $"Достигнут лимит файлов: {MaxFiles}.");

                    break;
                }

                var fileInfo = new FileInfo(file);

                if (fileInfo.Length > MaxFileSize)
                    continue;

                var lines = await File.ReadAllLinesAsync(
                    file,
                    cancellationToken);

                for (var i = 0; i < lines.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var rule in Rules)
                    {
                        var isMatch = Regex.IsMatch(
                            lines[i],
                            rule.Pattern,
                            RegexOptions.None,
                            TimeSpan.FromMilliseconds(100));

                        if (!isMatch)
                            continue;

                        findings.Add(new ScanFinding
                        {
                            Name = rule.Name,
                            Criticality = rule.Criticality,
                            FilePath = Path.GetRelativePath(
                                temporaryPath,
                                file),
                            Line = i + 1
                        });
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            Console.WriteLine(
                $"Анализ завершён. Проверено файлов: " +
                $"{Math.Min(filesScanned, MaxFiles)}. " +
                $"Найдено совпадений: {findings.Count}.");

            return findings;
        }
        finally
        {
            await DeleteTemporaryDirectoryAsync(temporaryPath);
        }
    }

    private static IEnumerable<string> EnumerateCSharpFiles(
        string rootPath)
    {
        var directories = new Stack<string>();
        directories.Push(rootPath);

        while (directories.Count > 0)
        {
            var currentDirectory = directories.Pop();

            foreach (var directory in Directory.EnumerateDirectories(
                         currentDirectory))
            {
                if (string.Equals(
                        Path.GetFileName(directory),
                        ".git",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                directories.Push(directory);
            }

            foreach (var file in Directory.EnumerateFiles(
                         currentDirectory,
                         "*.cs"))
            {
                yield return file;
            }
        }
    }

    private static async Task DeleteTemporaryDirectoryAsync(string path)
    {
        for (var attempt = 1; attempt <= MaxDeleteAttempts; attempt++)
        {
            if (!Directory.Exists(path))
                return;

            try
            {
                RemoveReadOnlyAttributes(path);
                Directory.Delete(path, recursive: true);

                Console.WriteLine("Временная папка удалена.");
                return;
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException)
            {
                Console.WriteLine(
                    $"Попытка удаления {attempt} не удалась: {ex.Message}");

                if (attempt < MaxDeleteAttempts)
                    await Task.Delay(500 * attempt);
            }
        }

        // Не маскируем результат сканирования ошибкой очистки.
        Console.WriteLine(
            $"Предупреждение: не удалось удалить временную папку: {path}");
    }

    private static void RemoveReadOnlyAttributes(string path)
    {
        foreach (var file in Directory.EnumerateFiles(
                     path, "*", SearchOption.AllDirectories))
        {
            var attributes = File.GetAttributes(file);

            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(
                    file,
                    attributes & ~FileAttributes.ReadOnly);
            }
        }

        foreach (var directory in Directory.EnumerateDirectories(
                     path, "*", SearchOption.AllDirectories))
        {
            var attributes = File.GetAttributes(directory);

            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(
                    directory,
                    attributes & ~FileAttributes.ReadOnly);
            }
        }

        var rootAttributes = File.GetAttributes(path);

        if ((rootAttributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(
                path,
                rootAttributes & ~FileAttributes.ReadOnly);
        }
    }
}
