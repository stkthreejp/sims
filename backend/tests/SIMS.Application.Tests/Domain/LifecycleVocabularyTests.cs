using SIMS.Domain.Enums;
using Xunit;

namespace SIMS.Application.Tests.Domain;

public class LifecycleVocabularyTests
{
    [Fact]
    public void PolicyTransactionStatus_UsesSpecificLifecycleStates()
    {
        var names = Enum.GetNames<PolicyTransactionStatus>();

        Assert.DoesNotContain("Pending", names);
        Assert.Contains("Submitted", names);
        Assert.Contains("InReview", names);
        Assert.Contains("Referred", names);
        Assert.Contains("Approved", names);
        Assert.Contains("Quoted", names);
        Assert.Contains("Accepted", names);
        Assert.Contains("Bound", names);
        Assert.Contains("NoticePending", names);
        Assert.Contains("NoticeSent", names);
        Assert.Contains("PendingEffectiveDate", names);
        Assert.Contains("Issued", names);
        Assert.Contains("Completed", names);
        Assert.Contains("Declined", names);
        Assert.Contains("Withdrawn", names);
        Assert.Contains("Voided", names);
    }

    [Fact]
    public void PolicyTransactionStatus_PreservesExistingDatabaseValues()
    {
        Assert.Equal(1, (int)Enum.Parse<PolicyTransactionStatus>("Submitted"));
        Assert.Equal(2, (int)Enum.Parse<PolicyTransactionStatus>("Issued"));
    }

    [Fact]
    public void TransactionType_IncludesFullPolicyLifecycleActions()
    {
        var names = Enum.GetNames<TransactionType>();

        Assert.Contains("NewBusiness", names);
        Assert.Contains("Endorsement", names);
        Assert.Contains("Renewal", names);
        Assert.Contains("Cancellation", names);
        Assert.Contains("Reinstatement", names);
        Assert.Contains("Audit", names);
        Assert.Contains("NonRenewal", names);
        Assert.Contains("Rewrite", names);
    }
}
