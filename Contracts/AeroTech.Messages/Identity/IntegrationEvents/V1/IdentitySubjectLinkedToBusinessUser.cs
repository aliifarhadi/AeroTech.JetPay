using AeroTech.Messages.Identity.Enums;

namespace AeroTech.Messages.Identity.IntegrationEvents.V1
{
    public sealed record IdentitySubjectLinkedToBusinessUser(
        string SubjectId,
        BusinessActorType UserType,
        long UserId,
        DateTimeOffset OccurredAt) : BaseIntegrationEvent;
}
