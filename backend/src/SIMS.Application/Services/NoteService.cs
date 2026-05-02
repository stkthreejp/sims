using SIMS.Application.Common;
using SIMS.Application.DTOs.Notes;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace SIMS.Application.Services;

public class NoteService : INoteService
{
    private readonly IServiceProvider _sp;
    private Microsoft.EntityFrameworkCore.DbContext Db =>
        (Microsoft.EntityFrameworkCore.DbContext)_sp.GetService(typeof(Microsoft.EntityFrameworkCore.DbContext))!;

    public NoteService(IServiceProvider sp) => _sp = sp;

    public async Task<IEnumerable<NoteDto>> GetByQuoteAsync(Guid quoteId)
    {
        var notes = await Db.Set<Note>()
            .Include(n => n.CreatedBy)
            .Where(n => n.QuoteId == quoteId && !n.IsDeleted)
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync();

        return notes.Select(MapToDto);
    }

    public async Task<Result<NoteDto>> GetByIdAsync(Guid quoteId, Guid id)
    {
        var note = await Db.Set<Note>()
            .Include(n => n.CreatedBy)
            .FirstOrDefaultAsync(n => n.Id == id && n.QuoteId == quoteId && !n.IsDeleted);

        return note == null
            ? Result<NoteDto>.Failure("NOT_FOUND", "Note not found.")
            : Result<NoteDto>.Success(MapToDto(note));
    }

    public async Task<Result<NoteDto>> CreateAsync(Guid quoteId, NoteCreateDto dto, Guid userId)
    {
        var note = new Note
        {
            QuoteId = quoteId,
            Subject = dto.Subject,
            Body = dto.Body,
            CreatedById = userId
        };

        Db.Set<Note>().Add(note);
        await Db.SaveChangesAsync();

        await Db.Entry(note).Reference(n => n.CreatedBy).LoadAsync();
        return Result<NoteDto>.Success(MapToDto(note));
    }

    public async Task<Result<NoteDto>> UpdateAsync(Guid quoteId, Guid id, NoteUpdateDto dto, Guid userId)
    {
        var note = await Db.Set<Note>().Include(n => n.CreatedBy)
            .FirstOrDefaultAsync(n => n.Id == id && n.QuoteId == quoteId && !n.IsDeleted);
        if (note == null) return Result<NoteDto>.Failure("NOT_FOUND", "Note not found.");

        note.Subject = dto.Subject;
        note.Body = dto.Body;
        note.UpdatedById = userId;
        note.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();

        return Result<NoteDto>.Success(MapToDto(note));
    }

    public async Task<Result> DeleteAsync(Guid quoteId, Guid id, Guid userId)
    {
        var note = await Db.Set<Note>()
            .FirstOrDefaultAsync(n => n.Id == id && n.QuoteId == quoteId && !n.IsDeleted);
        if (note == null) return Result.Failure("NOT_FOUND", "Note not found.");

        note.IsDeleted = true;
        note.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<NoteDto>> TogglePinAsync(Guid quoteId, Guid id, Guid userId)
    {
        var note = await Db.Set<Note>().Include(n => n.CreatedBy)
            .FirstOrDefaultAsync(n => n.Id == id && n.QuoteId == quoteId && !n.IsDeleted);
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
