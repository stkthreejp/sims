using SIMS.Domain.Entities;

namespace SIMS.Application.Security;

public readonly record struct UserAccessScope(Guid UserId, bool CanAccessAllBusinessData)
{
    public static UserAccessScope All(Guid userId) => new(userId, true);
}

public static class BusinessDataAccess
{
    public const string AccessDeniedCode = "ACCESS_DENIED";
    public const string AccessDeniedMessage = "You do not have access to this record.";

    public static IQueryable<Submission> ForAccessScope(this IQueryable<Submission> query, UserAccessScope scope)
    {
        if (scope.CanAccessAllBusinessData)
            return query;

        return query.Where(s =>
            s.CreatedById == scope.UserId ||
            s.UnderwriterId == scope.UserId ||
            s.AssistantUWId == scope.UserId);
    }

    public static IQueryable<Quote> ForAccessScope(this IQueryable<Quote> query, UserAccessScope scope)
    {
        if (scope.CanAccessAllBusinessData)
            return query;

        return query.Where(q =>
            q.CreatedById == scope.UserId ||
            q.Submission.CreatedById == scope.UserId ||
            q.Submission.UnderwriterId == scope.UserId ||
            q.Submission.AssistantUWId == scope.UserId);
    }

    public static IQueryable<Policy> ForAccessScope(this IQueryable<Policy> query, UserAccessScope scope)
    {
        if (scope.CanAccessAllBusinessData)
            return query;

        return query.Where(p =>
            p.Submission.CreatedById == scope.UserId ||
            p.Submission.UnderwriterId == scope.UserId ||
            p.Submission.AssistantUWId == scope.UserId ||
            p.BoundQuote.CreatedById == scope.UserId);
    }

    public static IQueryable<Note> ForAccessScope(this IQueryable<Note> query, UserAccessScope scope)
    {
        if (scope.CanAccessAllBusinessData)
            return query;

        return query.Where(n =>
            n.CreatedById == scope.UserId ||
            n.Quote.CreatedById == scope.UserId ||
            n.Quote.Submission.CreatedById == scope.UserId ||
            n.Quote.Submission.UnderwriterId == scope.UserId ||
            n.Quote.Submission.AssistantUWId == scope.UserId);
    }
}
