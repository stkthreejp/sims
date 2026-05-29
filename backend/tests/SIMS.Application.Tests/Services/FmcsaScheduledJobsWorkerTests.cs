using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SIMS.Application.Common;
using SIMS.Application.Configuration;
using SIMS.Application.DTOs.Fmcsa;
using SIMS.Application.Interfaces.Services;
using SIMS.Infrastructure.Workers;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class FmcsaScheduledJobsWorkerTests
{
    [Fact]
    public async Task RunDailyJobsAsync_DoesNotMarkRunDateWhenAnalyticsFails()
    {
        var analytics = new RecordingFmcsaAnalyticsService
        {
            ImportedCarrierResult = Result<FmcsaAnalyticsRefreshDto>.Failure("FMCSA_DOWN", "FMCSA unavailable"),
        };
        var worker = CreateWorker(analytics, new RecordingInspectionEnrichmentService(), new FmcsaJobSettings
        {
            Enabled = true,
            RunImportedCarrierAnalytics = true,
            RunInspectionEnrichment = false,
        });
        var runDate = new DateOnly(2026, 5, 29);

        await InvokePrivateAsync(worker, "RunDailyJobsAsync", runDate, CancellationToken.None);

        Assert.Equal(1, analytics.ImportedCarrierCalls);
        Assert.Null(GetPrivateField<DateOnly?>(worker, "_lastDailyRun"));
    }

    [Fact]
    public async Task RunMonthlySmsImportAsync_DoesNotMarkSnapshotMonthWhenImportFails()
    {
        var analytics = new RecordingFmcsaAnalyticsService
        {
            OfficialSmsResult = Result<FmcsaAnalyticsRefreshDto>.Failure("FMCSA_DOWN", "FMCSA unavailable"),
        };
        var worker = CreateWorker(analytics, new RecordingInspectionEnrichmentService(), new FmcsaJobSettings
        {
            Enabled = true,
            RunOfficialSmsPeerImport = true,
        });

        await InvokePrivateAsync(worker, "RunMonthlySmsImportAsync", "2026-05", CancellationToken.None);

        Assert.Equal(1, analytics.OfficialSmsCalls);
        Assert.Null(GetPrivateField<string?>(worker, "_lastMonthlySmsRun"));
    }

    private static FmcsaScheduledJobsWorker CreateWorker(
        IFmcsaSafetyAnalyticsService analytics,
        IFmcsaInspectionEnrichmentService enrichment,
        FmcsaJobSettings settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton(analytics);
        services.AddSingleton(enrichment);
        var provider = services.BuildServiceProvider();

        return new FmcsaScheduledJobsWorker(
            new StaticScopeFactory(provider),
            Options.Create(settings),
            NullLogger<FmcsaScheduledJobsWorker>.Instance);
    }

    private static async Task InvokePrivateAsync(FmcsaScheduledJobsWorker worker, string methodName, params object[] args)
    {
        var method = typeof(FmcsaScheduledJobsWorker).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = (Task)method.Invoke(worker, args)!;
        await task;
    }

    private static T GetPrivateField<T>(FmcsaScheduledJobsWorker worker, string fieldName)
    {
        var field = typeof(FmcsaScheduledJobsWorker).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (T)field.GetValue(worker)!;
    }

    private sealed class StaticScopeFactory(IServiceProvider provider) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new StaticScope(provider);
    }

    private sealed class StaticScope(IServiceProvider provider) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = provider;
        public void Dispose()
        {
        }
    }

    private sealed class RecordingFmcsaAnalyticsService : IFmcsaSafetyAnalyticsService
    {
        public int ImportedCarrierCalls { get; private set; }
        public int OfficialSmsCalls { get; private set; }
        public Result<FmcsaAnalyticsRefreshDto> ImportedCarrierResult { get; init; } = Result<FmcsaAnalyticsRefreshDto>.Success(new FmcsaAnalyticsRefreshDto());
        public Result<FmcsaAnalyticsRefreshDto> OfficialSmsResult { get; init; } = Result<FmcsaAnalyticsRefreshDto>.Success(new FmcsaAnalyticsRefreshDto());

        public Task<Result<FmcsaAnalyticsStatusDto>> GetStatusAsync(CancellationToken ct = default) =>
            Task.FromResult(Result<FmcsaAnalyticsStatusDto>.Success(new FmcsaAnalyticsStatusDto()));

        public Task<Result<FmcsaAnalyticsRefreshDto>> RefreshImportedCarrierAnalyticsAsync(string? snapshotMonth = null, CancellationToken ct = default)
        {
            ImportedCarrierCalls++;
            return Task.FromResult(ImportedCarrierResult);
        }

        public Task<Result<FmcsaAnalyticsRefreshDto>> RefreshOfficialSmsPeerAnalyticsAsync(string? snapshotMonth = null, int? maxRowsPerDataset = null, CancellationToken ct = default)
        {
            OfficialSmsCalls++;
            return Task.FromResult(OfficialSmsResult);
        }
    }

    private sealed class RecordingInspectionEnrichmentService : IFmcsaInspectionEnrichmentService
    {
        public Task<Result<FmcsaInspectionEnrichmentDto>> EnrichRecentInspectionsAsync(int maxInspections = 50, CancellationToken ct = default) =>
            Task.FromResult(Result<FmcsaInspectionEnrichmentDto>.Success(new FmcsaInspectionEnrichmentDto()));
    }
}
