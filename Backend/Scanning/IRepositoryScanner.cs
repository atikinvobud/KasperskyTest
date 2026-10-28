using Backend.Models;

namespace Backend.Scanning;

public interface IRepositoryScanner
{
    Task<List<ScanFinding>> ScanAsync(string repositoryUrl, CancellationToken cancellationToken);
}