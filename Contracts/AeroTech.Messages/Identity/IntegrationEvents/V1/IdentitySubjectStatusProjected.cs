using AeroTech.Messages.Identity.Enums;

namespace AeroTech.Messages.Identity.IntegrationEvents.V1
{
    public sealed record IdentitySubjectStatusProjected(
        string SubjectId,
        UserStatus Status,
        long SecurityVersion,
        DateTimeOffset OccurredAt) : BaseIntegrationEvent;
}
