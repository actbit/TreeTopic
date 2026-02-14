using Microsoft.EntityFrameworkCore;
using FileModel = TreeTopic.Models.File;

namespace TreeTopic.Repositories;

public class FileRepository : BaseRepository<FileModel>, IFileRepository
{
    public FileRepository(ApplicationDbContext context) : base(context)
    {
    }

    public Task<List<FileModel>> GetByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Where(f => f.MessageId == messageId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<FileModel?> GetLatestForMessageAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Where(f => f.MessageId == messageId && f.IsLatest)
            .OrderByDescending(f => f.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<FileModel>> GetHistoryBySourceAsync(Guid sourceFileId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Where(f => f.SourceFileId == sourceFileId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
