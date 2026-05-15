using SIMS.Application.Common;
using SIMS.Domain.Entities;

namespace SIMS.Application.Interfaces.Services;

public interface IOutboundEmailSenderService
{
    Task<Result<OutboundEmailSendResult>> SendAsync(OutboundCommunication communication, CancellationToken cancellationToken = default);
}

public sealed record OutboundEmailSendResult(string MessageId, string? WebLink);
