using AeroTech.Framework.Presentation.Responses;
using Microsoft.AspNetCore.Http;

namespace AeroTech.JetPay.Mock.Faults
{
    /// <summary>
    /// Lets a service-to-service mutation run and commit, then drops its response and answers 504. The caller learns
    /// nothing about the business outcome and must read the intent back; no business status is invented.
    /// </summary>
    public sealed class SimulatedTransportTimeoutMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly TransportFaultInjector _faults;

        public SimulatedTransportTimeoutMiddleware(RequestDelegate next, TransportFaultInjector faults)
        {
            _next = next;
            _faults = faults;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!IsServiceMutation(context.Request) || !_faults.TryConsumeTransportTimeout())
            {
                await _next(context);
                return;
            }

            var responseBody = context.Response.Body;
            await using var discarded = new MemoryStream();
            context.Response.Body = discarded;

            try
            {
                await _next(context);
            }
            finally
            {
                context.Response.Body = responseBody;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            await context.Response.WriteAsJsonAsync(new ApiResult
            {
                Errors = [new ApiErrorItem { Title = "Simulated transport timeout. Read the payment intent back to learn its state." }]
            });
        }

        private static bool IsServiceMutation(HttpRequest request)
            => HttpMethods.IsPost(request.Method)
               && request.Path.StartsWithSegments("/Service", StringComparison.OrdinalIgnoreCase);
    }
}
