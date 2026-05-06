using SIMS.Application.Security;
using SIMS.Domain.Entities;
using Xunit;

namespace SIMS.Application.Tests.Security;

public class BusinessDataAccessTests
{
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Guid _assistant = Guid.NewGuid();
    private readonly Guid _other = Guid.NewGuid();

    [Fact]
    public void SubmissionScope_FiltersToCreatedAssignedOrAssistantRecords()
    {
        var visibleCreated = new Submission { Id = Guid.NewGuid(), CreatedById = _owner, UnderwriterId = _other };
        var visibleUnderwriter = new Submission { Id = Guid.NewGuid(), CreatedById = _other, UnderwriterId = _owner };
        var visibleAssistant = new Submission { Id = Guid.NewGuid(), CreatedById = _other, UnderwriterId = _other, AssistantUWId = _owner };
        var hidden = new Submission { Id = Guid.NewGuid(), CreatedById = _other, UnderwriterId = _other };

        var result = new[] { visibleCreated, visibleUnderwriter, visibleAssistant, hidden }
            .AsQueryable()
            .ForAccessScope(new UserAccessScope(_owner, false))
            .Select(s => s.Id)
            .ToList();

        Assert.Contains(visibleCreated.Id, result);
        Assert.Contains(visibleUnderwriter.Id, result);
        Assert.Contains(visibleAssistant.Id, result);
        Assert.DoesNotContain(hidden.Id, result);
    }

    [Fact]
    public void QuoteScope_FollowsSubmissionAccessAndQuoteCreator()
    {
        var visibleByQuoteCreator = QuoteFor(new Submission { CreatedById = _other, UnderwriterId = _other }, _owner);
        var visibleBySubmissionAccess = QuoteFor(new Submission { CreatedById = _other, UnderwriterId = _owner }, _other);
        var hidden = QuoteFor(new Submission { CreatedById = _other, UnderwriterId = _other }, _other);

        var result = new[] { visibleByQuoteCreator, visibleBySubmissionAccess, hidden }
            .AsQueryable()
            .ForAccessScope(new UserAccessScope(_owner, false))
            .Select(q => q.Id)
            .ToList();

        Assert.Contains(visibleByQuoteCreator.Id, result);
        Assert.Contains(visibleBySubmissionAccess.Id, result);
        Assert.DoesNotContain(hidden.Id, result);
    }

    [Fact]
    public void PolicyScope_FollowsSubmissionAccessAndBoundQuoteCreator()
    {
        var visibleBySubmissionAccess = PolicyFor(
            new Submission { CreatedById = _other, UnderwriterId = _owner },
            new Quote { CreatedById = _other });
        var visibleByBoundQuoteCreator = PolicyFor(
            new Submission { CreatedById = _other, UnderwriterId = _other },
            new Quote { CreatedById = _owner });
        var hidden = PolicyFor(
            new Submission { CreatedById = _other, UnderwriterId = _other },
            new Quote { CreatedById = _other });

        var result = new[] { visibleBySubmissionAccess, visibleByBoundQuoteCreator, hidden }
            .AsQueryable()
            .ForAccessScope(new UserAccessScope(_owner, false))
            .Select(p => p.Id)
            .ToList();

        Assert.Contains(visibleBySubmissionAccess.Id, result);
        Assert.Contains(visibleByBoundQuoteCreator.Id, result);
        Assert.DoesNotContain(hidden.Id, result);
    }

    [Fact]
    public void AccessAllScope_DoesNotFilterRecords()
    {
        var records = new[]
        {
            new Submission { Id = Guid.NewGuid(), CreatedById = _other, UnderwriterId = _other },
            new Submission { Id = Guid.NewGuid(), CreatedById = _owner, UnderwriterId = _other },
        };

        var result = records
            .AsQueryable()
            .ForAccessScope(UserAccessScope.All(_owner))
            .ToList();

        Assert.Equal(records.Length, result.Count);
    }

    private static Quote QuoteFor(Submission submission, Guid createdById) => new()
    {
        Id = Guid.NewGuid(),
        Submission = submission,
        CreatedById = createdById,
    };

    private static Policy PolicyFor(Submission submission, Quote boundQuote) => new()
    {
        Id = Guid.NewGuid(),
        Submission = submission,
        BoundQuote = boundQuote,
    };
}
