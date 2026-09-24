using AeroTech.JetPay.Mock.Faults;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AeroTech.JetPay.Application.AcceptanceTests.Mock;

public sealed class SimulatedTransportTimeoutTests
{
    private readonly TransportFaultInjector _faults = new();
    private int _executions;

    [Fact]
    public async Task An_armed_timeout_lets_the_mutation_commit_but_hides_its_outcome()
    {
        _faults.ArmTransportTimeouts(1);
        var context = Request(HttpMethods.Post, "/Service/v1/Payment-Intents/1001/Confirm");

        await Middleware().InvokeAsync(context);

        Assert.Equal(1, _executions);
        Assert.Equal(StatusCodes.Status504GatewayTimeout, context.Response.StatusCode);
        Assert.DoesNotContain("Captured", Body(context));
    }

    [Fact]
    public async Task Only_the_armed_number_of_mutations_time_out()
    {
        _faults.ArmTransportTimeouts(1);
        await Middleware().InvokeAsync(Request(HttpMethods.Post, "/Service/v1/Payment-Intents"));

        var next = Request(HttpMethods.Post, "/Service/v1/Payment-Intents");
        await Middleware().InvokeAsync(next);

        Assert.Equal(StatusCodes.Status200OK, next.Response.StatusCode);
        Assert.Equal(0, _faults.PendingTimeouts);
    }

    [Fact]
    public async Task Reads_and_mock_controls_are_never_timed_out()
    {
        _faults.ArmTransportTimeouts(1);
        var read = Request(HttpMethods.Get, "/Service/v1/Payment-Intents/1001");
        var control = Request(HttpMethods.Post, "/Mock/v1/Clock/Advance");

        await Middleware().InvokeAsync(read);
        await Middleware().InvokeAsync(control);

        Assert.Equal(StatusCodes.Status200OK, read.Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, control.Response.StatusCode);
        Assert.Equal(1, _faults.PendingTimeouts);
    }

    private SimulatedTransportTimeoutMiddleware Middleware()
        => new(async context =>
        {
            _executions++;
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync("{\"data\":{\"status\":\"Captured\"}}");
        }, _faults);

    private static DefaultHttpContext Request(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string Body(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return new StreamReader(context.Response.Body).ReadToEnd();
    }
}
