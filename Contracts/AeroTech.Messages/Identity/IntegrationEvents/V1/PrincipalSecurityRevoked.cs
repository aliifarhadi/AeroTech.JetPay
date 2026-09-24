using AeroTech.Messages.Aegis.Enums;

namespace AeroTech.Messages.Identity.IntegrationEvents.V1
{
    public sealed record PrincipalSecurityRevoked(
        PrincipalType PrincipalType,
        string? SubjectId,
        long? MachinePrincipalId,
        long? SessionId,
        long? CredentialId,
        long SecurityVersion,
        string ReasonCode,
        DateTimeOffset EffectiveAt) : BaseIntegrationEvent;
}
