using HolidayMonitor.Contracts;
using HolidayScraper.Api;
using NServiceBus;
using NServiceBus.Transport.RabbitMQ;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var host = Host.CreateDefaultBuilder(args)
    .UseNServiceBus(hostContext =>
    {
        var config = hostContext.Configuration;
        var endpointConfiguration = new EndpointConfiguration("HolidayScraper.Api");
        endpointConfiguration.UseTransport<RabbitMQTransport>()
            .ConnectionString(config.GetConnectionString("RabbitMQ") ?? "host=localhost")
            .UseConventionalRoutingTopology(QueueType.Classic);
        endpointConfiguration.EnableInstallers();
        endpointConfiguration.SendOnly();
        return endpointConfiguration;
    })
    .ConfigureServices((context, services) =>
    {
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("HolidayScraper.Api"))
            .WithTracing(t =>
            {
                t.AddSource("HolidayScraper")
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o =>
                    {
                        o.Endpoint = new Uri(context.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317");
                    });
            });
        services.AddHttpClient<NagerDateClient>();
        services.AddHostedService<HolidayPollingWorker>();
    })
    .Build();

await host.RunAsync();
