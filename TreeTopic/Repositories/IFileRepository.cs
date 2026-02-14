using FileModel = TreeTopic.Models.File;

namespace TreeTopic.Repositories;

public interface IFileRepository : IBaseRepository<FileModel>
{
    Task<List<FileModel>> GetByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task<FileModel?> GetLatestForMessageAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task<List<FileModel>> GetHistoryBySourceAsync(Guid sourceFileId, CancellationToken cancellationToken = default);
}
