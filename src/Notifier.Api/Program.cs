using HolidayMonitor.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Notifier.Api.Hubs;
using Notifier.Api.Handlers;
using NServiceBus;
using NServiceBus.Transport.RabbitMQ;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Text;

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

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is required");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "HolidayMonitor",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "HolidayMonitor",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
        };
        // SignalR cannot send Authorization headers over WebSocket,
        // so we extract the token from the "access_token" query parameter.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    context.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration["Cors:Origins"]?.Split(',') ?? ["http://localhost:5173", "http://localhost:3000"])
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
app.UseAuthentication();
app.UseAuthorization();
app.UseRouting();

app.MapHub<NotificationHub>("/hubs/notifications");

// Dev endpoint only available in Development environment
if (app.Environment.IsDevelopment())
{
    app.MapControllers();
}

await app.RunAsync();
