using HolidayMonitor.Contracts;
using Notifier.Api.Hubs;
using Notifier.Api.Handlers;
using NServiceBus;
using NServiceBus.Transport.RabbitMQ;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Notifier.Api"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation()
            .AddSource("Notifier")
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317");
            });
    });

builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration["Cors:Origins"]?.Split(',') ?? new[] { "http://localhost:5173", "http://localhost:3000" })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Host.UseNServiceBus(hostContext =>
{
    var config = hostContext.Configuration;
    var endpointConfiguration = new EndpointConfiguration("Notifier.Api");
    endpointConfiguration.UseSerialization<XmlSerializer>();
    endpointConfiguration.UseTransport<RabbitMQTransport>()
        .ConnectionString(config.GetConnectionString("RabbitMQ") ?? "host=localhost")
        .UseConventionalRoutingTopology(QueueType.Classic);
    endpointConfiguration.EnableInstallers();
    return endpointConfiguration;
});

var app = builder.Build();

app.UseCors();
app.UseRouting();
app.MapHub<NotificationHub>("/hubs/notifications");

await app.RunAsync();
