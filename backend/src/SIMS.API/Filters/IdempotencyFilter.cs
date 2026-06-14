using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SIMS.Domain.Entities;
using SIMS.Infrastructure.Data;
using System.Text.Json;

namespace SIMS.API.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class IdempotentAttribute : TypeFilterAttribute
{
    public IdempotentAttribute() : base(typeof(IdempotencyFilter)) { }
}

public sealed class IdempotencyFilter : IAsyncActionFilter
{
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly ApplicationDbContext _db;

    public IdempotencyFilter(ApplicationDbContext db) => _db = db;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var key = context.HttpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
        {
            await next();
            return;
        }

        if (key.Length > 200) key = key[..200];
        var path = context.HttpContext.Request.Path.Value ?? "";

        var existing = await _db.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.Key == key && r.RequestPath == path);

        if (existing != null)
        {
            context.Result = new ContentResult
            {
                StatusCode = existing.StatusCode,
                ContentType = "application/json",
                Content = existing.ResponseBody,
            };
            return;
        }

        var executed = await next();

        if (executed.Result is ObjectResult { StatusCode: >= 200 and < 300 } obj)
        {
            var body = JsonSerializer.Serialize(obj.Value, _jsonOpts);
            _db.IdempotencyRecords.Add(new IdempotencyRecord
            {
                Key = key,
                RequestPath = path,
                StatusCode = obj.StatusCode ?? 200,
                ResponseBody = body,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
            });
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateException) { } // concurrent identical request already stored it
        }
    }
}
