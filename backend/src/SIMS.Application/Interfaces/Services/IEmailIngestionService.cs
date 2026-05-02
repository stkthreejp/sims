namespace SIMS.Application.Interfaces.Services;

public interface IEmailIngestionService
{
    Task IngestNewEmailsAsync(CancellationToken cancellationToken = default);
}
