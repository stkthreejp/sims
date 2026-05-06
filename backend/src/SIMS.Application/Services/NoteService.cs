using SIMS.Application.Common;
using SIMS.Application.DTOs.Notes;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace SIMS.Application.Services;

public class NoteService : INoteService
{
    private readonly IServiceProvider _sp;
    private Microsoft.EntityFrameworkCore.DbContext Db =>
        (Microsoft.EntityFrameworkCore.DbContext)_sp.GetService(typeof(Microsoft.EntityFrameworkCore.DbContext))!;

    public NoteService(IServiceProvider sp) => _sp = sp;

    public async Task<IEnumerable<NoteDto>> GetByQuoteAsync(Guid quoteId, UserAccessScope access)
    {
        var notes = await Db.Set<Note>()
            .Include(n => n.Quote).ThenInclude(q => q.Submission)
            .Include(n => n.CreatedBy)
            .Where(n => n.QuoteId == quoteId && !n.IsDeleted)
            .ForAccessScope(access)
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync();

        return notes.Select(MapToDto);
    }

    public async Task<Result<NoteDto>> GetByIdAsync(Guid quoteId, Guid id, UserAccessScope access)
    {
        var note = await Db.Set<Note>()
            .Include(n => n.Quote).ThenInclude(q => q.Submission)
            .Include(n => n.CreatedBy)
            .Where(n => n.Id == id && n.QuoteId == quoteId && !n.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();

        return note == null
            ? Result<NoteDto>.Failure("NOT_FOUND", "Note not found.")
            : Result<NoteDto>.Success(MapToDto(note));
    }

    public async Task<Result<NoteDto>> CreateAsync(Guid quoteId, NoteCreateDto dto, UserAccessScope access)
    {
        var canAccessQuote = await Db.Set<Quote>()
            .Where(q => q.Id == quoteId && !q.IsDeleted)
            .ForAccessScope(access)
            .AnyAsync();
        if (!canAccessQuote)
            return Result<NoteDto>.Failure(BusinessDataAccess.AccessDeniedCode, BusinessDataAccess.AccessDeniedMessage);

        var note = new Note
        {
            QuoteId = quoteId,
            Subject = dto.Subject,
            Body = dto.Body,
            CreatedById = access.UserId
        };

        Db.Set<Note>().Add(note);
        await Db.SaveChangesAsync();

        await Db.Entry(note).Reference(n => n.CreatedBy).LoadAsync();
        return Result<NoteDto>.Success(MapToDto(note));
    }

    public async Task<Result<NoteDto>> UpdateAsync(Guid quoteId, Guid id, NoteUpdateDto dto, UserAccessScope access)
    {
        var note = await Db.Set<Note>()
            .Include(n => n.Quote).ThenInclude(q => q.Submission)
            .Include(n => n.CreatedBy)
            .Where(n => n.Id == id && n.QuoteId == quoteId && !n.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();
        if (note == null) return Result<NoteDto>.Failure("NOT_FOUND", "Note not found.");

        note.Subject = dto.Subject;
        note.Body = dto.Body;
        note.UpdatedById = access.UserId;
        note.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();

        return Result<NoteDto>.Success(MapToDto(note));
    }

    public async Task<Result> DeleteAsync(Guid quoteId, Guid id, UserAccessScope access)
    {
        var note = await Db.Set<Note>()
            .Include(n => n.Quote).ThenInclude(q => q.Submission)
            .Where(n => n.Id == id && n.QuoteId == quoteId && !n.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();
        if (note == null) return Result.Failure("NOT_FOUND", "Note not found.");

        note.IsDeleted = true;
        note.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<NoteDto>> TogglePinAsync(Guid quoteId, Guid id, UserAccessScope access)
    {
        var note = await Db.Set<Note>()
            .Include(n => n.Quote).ThenInclude(q => q.Submission)
            .Include(n => n.CreatedBy)
            .Where(n => n.Id == id && n.QuoteId == quoteId && !n.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();
        if (note == null) return Result<NoteDto>.Failure("NOT_FOUND", "Note not found.");

        note.IsPinned = !note.IsPinned;
        note.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();

        return Result<NoteDto>.Success(MapToDto(note));
    }

    private static NoteDto MapToDto(Note n) => new()
    {
        Id = n.Id,
        QuoteId = n.QuoteId,
        Subject = n.Subject,
        Body = n.Body,
        IsPinned = n.IsPinned,
        CreatedById = n.CreatedById,
        CreatedByName = n.CreatedBy?.FullName ?? "",
        CreatedAt = n.CreatedAt,
        UpdatedAt = n.UpdatedAt
    };
}
