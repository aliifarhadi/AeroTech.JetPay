using AeroTech.Framework.Infrastructure;
using AeroTech.Framework.Presentation.Extensions;
using AeroTech.JetPay.Application;
using AeroTech.JetPay.Consumers;
using AeroTech.JetPay.Jobs;
using AeroTech.JetPay.Mock;
using AeroTech.JetPay.Persistence;
using AeroTech.JetPay.Providers;
using AeroTech.JetPay.Query;
using AeroTech.JetPay.ReferenceData;
using AeroTech.JetPay.RestApi;
using AeroTech.JetPay.Synchronizer;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext().WriteTo.Console());

builder.Services
    .AddFrameworkInfrastructure(builder.Configuration)
    .AddPersistence(builder.Configuration)
    .AddProviders(builder.Configuration)
    .AddQuery(builder.Configuration)
    .AddSynchronizer()
    .AddConsumers(builder.Configuration)
    .AddJobs(builder.Configuration)
    .AddApplication(builder.Configuration)
    .AddReferenceData(builder.Configuration)
    .AddPresentation(builder.Configuration, typeof(RestApiAssembly).Assembly, typeof(ReferenceDataAssembly).Assembly)
    .AddMockJetPay(builder.Configuration);

var app = builder.Build();

app.UseMockJetPay();
app.UsePresentation();

app.Run();

public partial class Program
{
}
