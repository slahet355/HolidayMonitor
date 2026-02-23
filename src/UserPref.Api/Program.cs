using HolidayMonitor.Contracts;
using MongoDB.Driver;
using NServiceBus;
using NServiceBus.Transport.RabbitMQ;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using UserPref.Api;
using UserPref.Api.Handlers;
using UserPref.Api.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("UserPref.Api"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation()
            .AddSource("UserPref")
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317");
            });
    });

var mongoConnection = builder.Configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017";
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnection));
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Host.UseNServiceBus(hostContext =>
{
    var config = hostContext.Configuration;
    var endpointConfiguration = new EndpointConfiguration("UserPref.Api");
    endpointConfiguration.UseSerialization<XmlSerializer>();
    var transport = endpointConfiguration.UseTransport<RabbitMQTransport>();
    transport.ConnectionString(config.GetConnectionString("RabbitMQ") ?? "host=localhost")
        .UseConventionalRoutingTopology(QueueType.Classic);
    endpointConfiguration.EnableInstallers();
    var routing = transport.Routing();
    routing.RouteToEndpoint(typeof(NotifyUsersCommand), "Notifier.Api");
    return endpointConfiguration;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

await app.RunAsync();
