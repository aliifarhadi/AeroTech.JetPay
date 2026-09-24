namespace AeroTech.Messages.Identity.IntegrationEvents.V1
{
    public sealed record IdentitySubjectRegistered(
        string SubjectId,
        string? DisplayName,
        DateTimeOffset RegisteredAt) : BaseIntegrationEvent;
}
