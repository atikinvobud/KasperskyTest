
using Backend.Models;

namespace Backend.Repositories;

public interface IScanRepository
{
    Guid Add(ScanTask scanTask);

    ScanTask? GetById(Guid id);

    bool Update(ScanTask scanTask);
}
