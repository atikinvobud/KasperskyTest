
using System.Collections.Concurrent;
using Backend.Models;

namespace Backend.Repositories;

public class ScanRepository : IScanRepository
{
    private readonly ConcurrentDictionary<Guid, ScanTask> _scanTasks = new();

    public Guid Add(ScanTask scanTask)
    {
        if (!_scanTasks.TryAdd(scanTask.Id, scanTask))
        {
            throw new InvalidOperationException($"Задача сканирования с ID {scanTask.Id} уже существует.");
        }

        return scanTask.Id;
    }

    public ScanTask? GetById(Guid id)
    {
        _scanTasks.TryGetValue(id, out var scanTask);

        return scanTask;
    }

    public bool Update(ScanTask scanTask)
    {
        if (!_scanTasks.ContainsKey(scanTask.Id)) return false;

        _scanTasks[scanTask.Id] = scanTask;
        
        return true;
    }
}
