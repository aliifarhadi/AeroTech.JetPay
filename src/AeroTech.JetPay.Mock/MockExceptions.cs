using AeroTech.Framework.Core.Domain.Exceptions;

namespace AeroTech.JetPay.Mock
{
    public static class MockExceptions
    {
        // Mock-only: 8900-8999
        public static BusinessException ScenarioNotApplicable(params object?[] args) =>
            new(8900, "Mock scenario {0} does not apply to tender type {1}.", args) { HttpStatus = 409 };
    }
}
