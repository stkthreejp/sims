using SIMS.Application.Common;
using SIMS.Application.DTOs.Notes;

namespace SIMS.Application.Interfaces.Services;

public interface INoteService
{
    Task<IEnumerable<NoteDto>> GetByQuoteAsync(Guid quoteId);
    Task<Result<NoteDto>> GetByIdAsync(Guid quoteId, Guid id);
    Task<Result<NoteDto>> CreateAsync(Guid quoteId, NoteCreateDto dto, Guid userId);
    Task<Result<NoteDto>> UpdateAsync(Guid quoteId, Guid id, NoteUpdateDto dto, Guid userId);
    Task<Result> DeleteAsync(Guid quoteId, Guid id, Guid userId);
    Task<Result<NoteDto>> TogglePinAsync(Guid quoteId, Guid id, Guid userId);
}
