using SIMS.Application.Common;
using SIMS.Application.DTOs.Notes;
using SIMS.Application.Security;

namespace SIMS.Application.Interfaces.Services;

public interface INoteService
{
    Task<IEnumerable<NoteDto>> GetByQuoteAsync(Guid quoteId, UserAccessScope access);
    Task<Result<NoteDto>> GetByIdAsync(Guid quoteId, Guid id, UserAccessScope access);
    Task<Result<NoteDto>> CreateAsync(Guid quoteId, NoteCreateDto dto, UserAccessScope access);
    Task<Result<NoteDto>> UpdateAsync(Guid quoteId, Guid id, NoteUpdateDto dto, UserAccessScope access);
    Task<Result> DeleteAsync(Guid quoteId, Guid id, UserAccessScope access);
    Task<Result<NoteDto>> TogglePinAsync(Guid quoteId, Guid id, UserAccessScope access);
}
