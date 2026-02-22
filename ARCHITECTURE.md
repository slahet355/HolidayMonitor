# Holiday Monitor - System Architecture

---

## 1. Tech Stack

| Layer | Technology | Purpose |
|-------|-----------|---------|
| **Frontend** | React 18, TypeScript, Vite, SignalR, TailwindCSS | Dashboard UI with real-time notifications |
| **Backend Services** | .NET 8, ASP.NET Core, NServiceBus | Microservices (HolidayScraper, UserPref, Notifier) |
| **Message Broker** | RabbitMQ | Event-driven messaging between services |
| **Database** | MongoDB | User subscriptions storage |
| **Observability** | OpenTelemetry (OTEL), Jaeger | Distributed tracing and monitoring |
| **External API** | Nager.Date API | Public holidays data (180+ countries, free) |
| **Deployment** | Docker, Docker Compose | Containerization |

---

## 2. System Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         HOLIDAY MONITOR ECOSYSTEM                           │
└─────────────────────────────────────────────────────────────────────────────┘

                              ┌─────────────────┐
                              │  Dashboard.UI   │
                              │  (React/Vite)   │
                              │  Port: 5173     │
                              └────────┬────────┘
                                       │
                        ┌──────────────┼──────────────┐
                        │              │              │
                        │ HTTP REST    │ WebSocket    │
                        │ (API calls)  │ (SignalR)    │
                        ▼              ▼              ▼
                    
        ┌──────────────────┐     ┌──────────────────┐
        │  Notifier.Api    │     │  UserPref.Api    │
        │  (SignalR Hub)   │     │  (REST API)      │
        │  Port: 5000      │     │  Port: 5001      │
        └────────┬─────────┘     └─────────┬────────┘
                 │                         │
        ┌────────┴────────┐        ┌────────┴────────┐
        │                 │        │                 │
        │   Handles       │        │   Stores/       │
        │   Messages      │        │   Manages       │
        │   (RabbitMQ)    │        │   Subs          │
        │                 │        │   (MongoDB)     │
        └────────┬────────┘        └────────┬────────┘
                 │                         │
                 │                         │
        ┌────────▼────────┬────────────────┘
        │                 │
        │   NServiceBus   │
        │   Message Bus   │
        │   (RabbitMQ)    │
        │                 │
        └────────▲────────┘
                 │
                 │
                 │   Publishes
                 │   Events
                 │
        ┌────────┴────────┐
        │                 │
        │ HolidayScraper  │
        │ (Polling        │
        │  Worker)        │
        │ Port: 5002      │
        └─────────────────┘
                 │
                 ▼
        Nager.Date API
        (External)
```

---

## 2. Data Flow Sequence: Holiday Detection to Real-Time Notification

### Complete End-to-End Flow

```
STEP 1: HOLIDAY POLLING
═════════════════════════════════════════════════════════════════════════════

    HolidayScraper.Api
    ├─ Runs every 1 hour (BackgroundService)
    ├─ Calls NagerDateClient
    ├─ GET https://date.nager.at/api/v3/publicholidays/{currentYear}/{countryCode}
    └─ Compares: Is today a holiday?
            YES ──→ Publish Event
            NO ───→ Skip


STEP 2: EVENT PUBLICATION
═════════════════════════════════════════════════════════════════════════════

    HolidayPollingWorker publishes:
    ┌────────────────────────────────────────────┐
    │ PublicHolidayDetected Event                │
    ├────────────────────────────────────────────┤
    │ - CountryCode: "US"                        │
    │ - CountryName: "United States"             │
    │ - Date: 2026-12-25                         │
    │ - LocalName: "Christmas Day"               │
    │ - Name: "Christmas"                        │
    │ - DetectedAtUtc: 2026-02-21T10:30:00Z     │
    └────────────────────────────────────────────┘
            │
            │ RabbitMQ Topic Exchange
            │ Routing Key: PublicHolidayDetected
            ▼


STEP 3: SUBSCRIPTION PROCESSING
═════════════════════════════════════════════════════════════════════════════

    UserPref.Api (Subscriber) receives PublicHolidayDetected
    │
    ├─ Deserialize event into handler
    ├─ Query MongoDB (Subscriptions Collection)
    │  SELECT * FROM subscriptions WHERE countryCode = "US"
    │
    └─ For each user subscribed to "US":
        ┌─────────────────────────────────────────────┐
        │ Create NotifyUsersCommand                   │
        ├─────────────────────────────────────────────┤
        │ - UserIds: ["user1", "user2", "user3"]     │
        │ - CountryCode: "US"                         │
        │ - CountryName: "United States"              │
        │ - Date: 2026-12-25                          │
        │ - LocalName: "Christmas Day"                │
        │ - Name: "Christmas"                         │
        │ - DetectedAtUtc: 2026-02-21T10:30:00Z      │
        └─────────────────────────────────────────────┘
                │
                │ Send via NServiceBus
                │ Routing: Notifier.Api endpoint
                ▼


STEP 4: COMMAND HANDLING & SIGNALR BROADCAST
═════════════════════════════════════════════════════════════════════════════

    Notifier.Api (NotifyUsersCommandHandler) receives:
    │
    ├─ Extract payload:
    │  {
    │    "type": "HolidayDetected",
    │    "countryCode": "US",
    │    "countryName": "United States",
    │    "date": "2026-12-25T00:00:00Z",
    │    "localName": "Christmas Day",
    │    "name": "Christmas",
    │    "detectedAtUtc": "2026-02-21T10:30:00Z"
    │  }
    │
    ├─ For each userId in message.UserIds:
    │  └─ _hubContext.Clients.Group(userId)
    │     .SendAsync("HolidayDetected", payload)
    │
    └─ Connection established via SignalR Hub
        ┌─────────────────────────────────────────────┐
        │ NotificationHub                             │
        ├─────────────────────────────────────────────┤
        │ Group Management:                           │
        │ - OnConnectedAsync: Add to group by userId  │
        │ - SetUserId: Reassign to group              │
        │ - OnDisconnectedAsync: Handle disconnect    │
        └─────────────────────────────────────────────┘
                │
                │ WebSocket Message
                │ Path: /hubs/notifications
                ▼


STEP 5: REAL-TIME UI UPDATE
═════════════════════════════════════════════════════════════════════════════

    Dashboard.UI (React Component)
    │
    ├─ Has active SignalR connection
    ├─ Listens to Hub method: "HolidayDetected"
    │
    ├─ On message received:
    │  ├─ Update React state
    │  ├─ Display toast notification
    │  ├─ Refresh holidays list
    │  └─ Animate new holiday card
    │
    └─ User sees real-time notification!

```

---

## 3. Component Diagram

```
┌────────────────────────────────────────────────────┬─────────────────────────────────────────────────┐
│ FRONTEND LAYER                                     │ BACKEND SERVICES LAYER                          │
├────────────────────────────────────────────────────┼─────────────────────────────────────────────────┤
│                                                    │                                                 │
│  Dashboard.UI (React 18 + TypeScript)              │  HolidayScraper.Api (.NET 8)                    │
│  ├─ App.tsx                                        │  ├─ HolidayPollingWorker (BackgroundService)    │
│  ├─ Vite Dev Server (5173)                         │  ├─ NagerDateClient (HTTP Client)               │
│  ├─ SignalR Connection                             │  ├─ Published Events:                           │
│  ├─ React Hooks                                    │  │   └─ PublicHolidayDetected                    │
│  ├─ Components/                                    │  ├─ NServiceBus Endpoint                        │
│  │  ├─ Holiday List                                │  └─ OpenTelemetry Instrumentation               │
│  │  ├─ Notifications Toast                         │                                                 │
│  │  └─ Subscription Manager                        │                                                 │
│  └─ TailwindCSS Styling                            │  UserPref.Api (.NET 8)                          │
│                                                    │  ├─ REST Controllers                             │
│                                                    │  ├─ PublicHolidayDetectedHandler                │
│                                                    │  ├─ SubscriptionRepository (MongoDB)            │
│                                                    │  ├─ Sends: NotifyUsersCommand                   │
│                                                    │  └─ OpenTelemetry Instrumentation               │
│                                                    │                                                 │
│                                                    │  Notifier.Api (.NET 8)                          │
│                                                    │  ├─ NotificationHub (SignalR)                   │
│                                                    │  ├─ NotifyUsersCommandHandler                   │
│                                                    │  ├─ Group-based broadcasting                    │
│                                                    │  └─ OpenTelemetry Instrumentation               │
│                                                    │                                                 │
│                                                    │  HolidayMonitor.Contracts                       │
│                                                    │  ├─ NotifyUsersCommand (ICommand)               │
│                                                    │  ├─ PublicHolidayDetected (IEvent)              │
│                                                    │  └─ Shared DTOs                                 │
│                                                    │                                                 │
└────────────────────────────────────────────────────┴─────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ INFRASTRUCTURE LAYER                                                                               │
├────────────────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                                    │
│  Message Broker          │  Database              │  Observability         │  External APIs       │
│  ───────────────────────┼──────────────────────┼────────────────────────┼─────────────────────  │
│  RabbitMQ               │  MongoDB              │  OpenTelemetry (OTEL)  │  Nager.Date API      │
│  ├─ Queues              │  ├─ Subscriptions    │  ├─ OTLP Collector     │  ├─ Public Holidays  │
│  ├─ Topic Exchanges     │  │   Collection      │  ├─ Jaeger (Tracing)   │  ├─ 180+ Countries   │
│  ├─ Message Format: XML │  ├─ Indexes          │  ├─ Prometheus         │  └─ Free, No Auth    │
│  └─ URI: localhost:5672 │  └─ URI:             │  └─                     │                      │
│                         │     localhost:27017  │                         │                      │
│                         │                      │                         │                      │
└────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Complete Tech Stack

### **Frontend**
| Technology | Purpose | Version |
|-----------|---------|---------|
| **React** | UI Framework | 18.x |
| **TypeScript** | Type Safety | Latest |
| **Vite** | Build Tool & Dev Server | Latest |
| **SignalR Client** | WebSocket Real-time Communication | Latest |
| **TailwindCSS** | Styling | Latest |
| **PostCSS** | CSS Processing | Latest |

### **Backend Services**
| Component | Language | Framework | Key Libraries |
|-----------|----------|-----------|----------------|
| **HolidayScraper.Api** | C# | .NET 8 | `NServiceBus`, `HttpClient`, `BackgroundService` |
| **UserPref.Api** | C# | .NET 8 | `NServiceBus`, `MongoDB.Driver`, `ASP.NET Core` |
| **Notifier.Api** | C# | .NET 8 | `NServiceBus`, `SignalR`, `ASP.NET Core` |
| **Contracts** | C# | Class Library | NServiceBus interfaces (`ICommand`, `IEvent`) |

### **Message Transport & Serialization**
| Component | Technology | Details |
|-----------|-----------|---------|
| **Message Bus** | RabbitMQ | AMQP Protocol, Topic-based routing |
| **NServiceBus** | Distributed Messaging | Handles commands & events, retry policies, DLQ |
| **Serialization** | XmlSerializer | Built-in, no external JSON formatter |
| **Routing** | Conventional Routing | Topology: Classic (RabbitMQ) |

### **Data Storage**
| Component | Technology | Purpose |
|-----------|-----------|---------|
| **Primary DB** | MongoDB | User subscriptions, flexible schema |
| **Collections** | User Preferences | Country subscriptions per user |
| **Indexing** | Compound Indexes | Fast queries by (UserId, CountryCode) |

### **Observability & Monitoring**
| Component | Technology | Details |
|-----------|-----------|---------|
| **Distributed Tracing** | OpenTelemetry (OTEL) | Industry standard, vendor-agnostic |
| **Trace Exporter** | OTLP (gRPC) | Protocol: http://localhost:4317 |
| **Backends** | Jaeger, Prometheus | Configurable via OTEL_EXPORTER_OTLP_ENDPOINT |
| **Instrumentation** | Auto-instrumentation | HTTP, AspNetCore, SignalR, MongoDB |

### **External APIs**
| Service | Provider | Purpose | Endpoint |
|---------|----------|---------|----------|
| **Public Holidays** | Nager.Date (Free) | 180+ countries, no authentication | https://date.nager.at/api/v3 |

### **Deployment & Orchestration**
| Component | Technology |
|-----------|-----------|
| **Containerization** | Docker |
| **Orchestration** | Docker Compose |
| **Port Mapping** | (See Quick Start) |

---

## 5. Holiday Detection & SignalR Flow (Detailed)

### **5.1 SignalR Connection Lifecycle**

```csharp
/// FRONTEND: Dashboard.UI
const connection = new HubConnectionBuilder()
    .withUrl("http://localhost:5000/hubs/notifications", {
        accessTokenFactory: () => getUserToken(),
        skipNegotiation: true,
        transport: HttpTransportType.WebSockets
    })
    .withAutomaticReconnect()
    .build();

connection.start();

/// Listen for holiday notifications
connection.on("HolidayDetected", (payload) => {
    console.log("🎉 Holiday Detected:", payload);
    updateUI(payload);
    showNotificationToast(payload);
});

connection.on("disconnect", () => {
    console.warn("Disconnected from Notifier service");
    attemptReconnect();
});
```

### **5.2 Server-Side SignalR Hub**

```csharp
[AllowAnonymous]
public class NotificationHub : Hub
{
    /// OnConnectedAsync: Called when client connects
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.Identity?.Name 
                  ?? Context.GetHttpContext()?.Request.Query["userId"]
                  ?? Context.ConnectionId;
        
        /// Add this connection to a group identified by userId
        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        
        _logger.LogInformation("Client {ConnectionId} joined group {UserId}", 
            Context.ConnectionId, userId);
        
        await base.OnConnectedAsync();
    }

    /// SetUserId: Client can dynamically set/change userId
    public async Task SetUserId(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
    }

    /// OnDisconnectedAsync: Called when client disconnects
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client {ConnectionId} disconnected", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
```

### **5.3 Command Handler Broadcasting**

```csharp
public class NotifyUsersCommandHandler : IHandleMessages<NotifyUsersCommand>
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotifyUsersCommandHandler> _logger;

    public async Task Handle(NotifyUsersCommand message, IMessageHandlerContext context)
    {
        using var activity = ActivitySource.StartActivity("NotifyUsers");
        activity?.SetTag("userCount", message.UserIds.Count);
        activity?.SetTag("country", message.CountryCode);

        /// Construct payload
        var payload = new
        {
            type = "HolidayDetected",
            countryCode = message.CountryCode,
            countryName = message.CountryName,
            date = message.Date,
            localName = message.LocalName,
            name = message.Name,
            detectedAtUtc = message.DetectedAtUtc
        };

        /// Broadcast to each user's group
        foreach (var userId in message.UserIds)
        {
            _logger.LogInformation("Sending HolidayDetected to group: {UserId}", userId);
            
            /// This reaches ALL connections in the "userId" group
            await _hubContext.Clients
                .Group(userId)
                .SendAsync("HolidayDetected", payload, context.CancellationToken);
        }

        _logger.LogInformation("Pushed notification to {Count} users", 
            message.UserIds.Count);
    }
}
```

### **5.4 Message Path Visualization**

```
┌────────────────────────────────────────────────────────────────┐
│ Holiday Detected: Christmas in USA - 2026-12-25               │
└──────────────┬───────────────────────────────────────────────┘
               │
               ▼
┌────────────────────────────────────────────────────────────────┐
│ RabbitMQ Topic Exchange: PublicHolidayDetected                │
│ - Exchange Type: Topic                                         │
│ - Routing Key: "PublicHolidayDetected"                        │
└──────────────┬───────────────────────────────────────────────┘
               │
    ┌──────────┴──────────┐
    │                     │
    ▼ (Queue 1)           ▼ (Queue 2)
HolidayScraper        UserPref
(skips, already      Listener
published)           
                       │ (Filtered)
                       ▼
                  PublicHolidayDetectedHandler
                       │
                       ├─ Query MongoDB
                       │  SELECT users WHERE country="US"
                       │
                       ├─ Result: ["alice", "bob", "charlie"]
                       │
                       └─ Send NotifyUsersCommand
                          {
                            UserIds: ["alice", "bob", "charlie"],
                            CountryCode: "US",
                            ...
                          }
                          │
                          ▼ (RabbitMQ → Notifier Queue)
                       Notifier.Api
                          │
                          ▼
                  NotifyUsersCommandHandler
                          │
                 ┌────────┼────────┐
                 │        │        │
                 ▼        ▼        ▼
          SignalR Group SignalR Group SignalR Group
          "alice"        "bob"        "charlie"
                 │        │        │
                 └────────┼────────┘
                          ▼
                   WebSocket Message
                   "HolidayDetected"
                          │
                          ▼
                   Dashboard.UI
                   (React Component)
                          │
                          ▼
                   📱 User Notification
                   "🎉 Christmas detected
                    in United States!"
```

---

## 6. Message & Command Flow

### **6.1 PublicHolidayDetected Event**
**Published by:** `HolidayScraper.Api`  
**Consumed by:** `UserPref.Api`  
**Payload:**
```csharp
public class PublicHolidayDetected : IEvent
{
    public string CountryCode { get; set; }      // "US"
    public string CountryName { get; set; }      // "United States"
    public DateTime Date { get; set; }           // 2026-12-25
    public string LocalName { get; set; }        // "Christmas Day"
    public string Name { get; set; }             // "Christmas"
    public DateTime DetectedAtUtc { get; set; }  // 2026-02-21T10:30:00Z
}
```

### **6.2 NotifyUsersCommand**
**Sent by:** `UserPref.Api.PublicHolidayDetectedHandler`  
**Consumed by:** `Notifier.Api.NotifyUsersCommandHandler`  
**Payload:**
```csharp
public class NotifyUsersCommand : ICommand
{
    public List<string> UserIds { get; set; }     // ["alice", "bob", "charlie"]
    public string CountryCode { get; set; }       // "US"
    public string CountryName { get; set; }       // "United States"
    public DateTime Date { get; set; }            // 2026-12-25
    public string LocalName { get; set; }         // "Christmas Day"
    public string Name { get; set; }              // "Christmas"
    public DateTime DetectedAtUtc { get; set; }   // 2026-02-21T10:30:00Z
}
```

### **6.3 NServiceBus Routing Configuration**

```csharp
/// UserPref.Api routing
var routing = transport.Routing();
routing.RouteToEndpoint(typeof(NotifyUsersCommand), "Notifier.Api");

/// HolidayScraper.Api
endpointConfiguration.SendOnly();  // Only publishes events, doesn't receive

/// Notifier.Api
endpointConfiguration.UseEndpoint();  // Receives & handles commands
```

---

## 7. Polling Strategy & Frequency

```
HolidayPollingWorker (BackgroundService)
│
├─ Default Interval: 1 hour (configurable)
├─ Execution: Fire & forget (non-blocking)
│
├─ For each country in monitored list:
│  ├─ GET https://date.nager.at/api/v3/publicholidays/{year}/{countryCode}
│  │
│  └─ Compare: Is today in the response?
│     ├─ YES ──→ Publish PublicHolidayDetected event
│     └─ NO ───→ (Do nothing, wait next interval)
│
├─ Error Handling:
│  ├─ HTTP Timeout → Log error, retry next cycle
│  ├─ Invalid Response → Log & skip
│  └─ Network Down → Continue on next interval
│
└─ Observability:
   ├─ OpenTelemetry spans: "PollPublicHolidays"
   ├─ Trace tags: country, holiday count
   └─ Logs: INFO level for each poll cycle
```

---

## 8. Database Schema (MongoDB)

### **Collections**

#### 1. Subscriptions Collection
```json
{
  "_id": ObjectId("507f1f77bcf86cd799439011"),
  "userId": "alice@example.com",
  "subscribedCountries": [
    { "code": "US", "name": "United States", "subscribedAtUtc": "2025-01-01T00:00:00Z" },
    { "code": "GB", "name": "United Kingdom", "subscribedAtUtc": "2025-01-05T10:30:00Z" },
    { "code": "DE", "name": "Germany", "subscribedAtUtc": "2025-01-10T14:15:00Z" }
  ],
  "createdAtUtc": "2025-01-01T00:00:00Z",
  "updatedAtUtc": "2026-02-21T10:30:00Z"
}
```

#### 2. Indexes
```javascript
db.subscriptions.createIndex({ "userId": 1 })
db.subscriptions.createIndex({ "subscribedCountries.code": 1 })
db.subscriptions.createIndex({ "userId": 1, "subscribedCountries.code": 1 })
```

---

## 9. OpenTelemetry Observability

### **Instrumentation Points**

```
HolidayScraper.Api
├─ ActivitySource: "HolidayScraper"
├─ Spans:
│  ├─ "PollPublicHolidays" (per cycle)
│  ├─ HTTP calls to Nager.Date
│  └─ Events published
└─ Metrics: Polling frequency, API latency

UserPref.Api
├─ ActivitySource: "UserPref"
├─ Spans:
│  ├─ API Controller actions
│  ├─ MongoDB queries
│  └─ Event handler execution
└─ Metrics: Query latency, subscription operations

Notifier.Api
├─ ActivitySource: "Notifier"
├─ Spans:
│  ├─ "NotifyUsers" (per command)
│  ├─ SignalR broadcasts
│  └─ Message handler
└─ Metrics: Notification latency, broadcast count

All Services
├─ OTLP Exporter: gRPC to http://localhost:4317
├─ Trace Context: Propagated via W3C Trace Context
└─ Service Resource: Labeled by service name
```

---

## 10. Error Handling & Resilience

### **Failure Scenarios**

| Scenario | Component | Handling | Result |
|----------|-----------|----------|--------|
| **Nager.Date API Down** | HolidayScraper.Api | Logs error, retries next cycle | No false positives, eventual consistency |
| **RabbitMQ Down** | All services | NServiceBus reconnection logic | Messages persist until broker recovers |
| **MongoDB Down** | UserPref.Api | HTTP 500 on API calls, can't fetch subscriptions | No notifications sent, log error |
| **SignalR Hub Unavailable** | Notifier.Api | .NET exception logged, async send fails gracefully | Users don't receive notification (until UI reconnects) |
| **User Disconnected** | Dashboard.UI | Automatic reconnection with backoff | Misses notification during disconnect, queuing possible |
| **Invalid Command** | Notifier.Api | Logs & continues, can configure DLQ | System doesn't crash |

---

## 11. Performance Characteristics

```
Operation                          | Latency    | Throughput | Notes
──────────────────────────────────┼────────────┼────────────┼─────────────────────────
Nager.Date API call                | 200-500ms  | 1 req/h    | External, rate limited
PublicHolidayDetected publish      | <50ms      | ~1 evt/h   | In-memory, async
MongoDB query (1 country)          | 5-20ms     | N/A        | Depends on index
NotifyUsersCommand send            | <10ms      | N/A        | RabbitMQ async
SignalR broadcast (100 users)      | 50-200ms   | N/A        | WebSocket parallel send
UI update (React render)           | 16-33ms    | 60fps      | DOM diff/patch
```

---

## 12. Architecture Improvement Suggestions

### **🚀 HIGH PRIORITY - Quick Wins**

#### **12.1 Event Sourcing for Audit Trail**
```
CURRENT: Event published → Handler immediately processes → State updated
PROPOSED: Event published → Append to Event Log → Handler processes → State updated

Benefits:
✓ Complete audit history of all holidays detected
✓ Replay capability for debugging
✓ Temporal queries ("What holidays were detected on date X?")
✓ Recovery from event handler failures

Implementation:
- Add EventLog collection to MongoDB
- Append all PublicHolidayDetected events before processing
- Create replay service for historical data
- Estimated effort: 2-3 days
```

#### **12.2 Implement Request Deduplication**
```
CURRENT: Multiple connections from same user = multiple SignalR messages
PROPOSED: Deduplicate notifications per user per holiday within time window

Benefits:
✓ Prevent duplicate notifications in UI
✓ Reduce unnecessary SignalR traffic
✓ Improve perceived UX

Implementation:
- Add cache (Redis): Key = userId:countryCode:date
- Check before sending NotifyUsersCommand
- TTL = 1 hour
- Estimated effort: 1 day
```

#### **12.3 Add Health Checks**
```
Implement: Readiness & Liveness probes
GET /health/ready  → All dependencies (RabbitMQ, MongoDB, Nager)
GET /health/live   → Service is running

Benefits:
✓ Kubernetes-ready
✓ Better debugging in production
✓ Load balancer can route traffic correctly

Implementation:
- HealthCheck middleware in all APIs
- Check RabbitMQ connection, MongoDB connection, Nager.Date
- Estimated effort: 1 day
```

---

### **🔧 MEDIUM PRIORITY - Scale & Robustness**

#### **12.4 Implement Message Retry Policy**
```
CURRENT: NServiceBus default retry (rough 5x with linear backoff)
PROPOSED: Explicit retry policy with circuit breaker

Benefits:
✓ Graceful handling of transient failures
✓ Prevent cascade failures
✓ Better visibility into retry counts

Implementation:
var recoverability = endpointConfiguration.Recoverability();
recoverability.Delayed(
    customizations: delayed =>
    {
        delayed.NumberOfRetries(3);
        delayed.TimeIncrease(TimeSpan.FromSeconds(5));
    }
);

Estimated effort: 1 day
```

#### **12.5 Add Redis Caching Layer**
```
CURRENT: MongoDB queried fresh for every PublicHolidayDetected event
PROPOSED: Cache subscription lists with Redis (TTL: 1 hour)

Benefits:
✓ Reduce MongoDB load by 90%
✓ Faster subscription lookups
✓ Enable offline fallback

Implementation:
- Cache key: country:{countryCode}:subscribers
- TTL: 3600 seconds
- Invalidation on subscription change via message
- Estimated effort: 1-2 days
```

#### **12.6 Implement Circuit Breaker for Nager.Date**
```
CURRENT: Timeout on every Nager.Date call if down
PROPOSED: Circuit breaker pattern (Open → Half-Open → Closed)

Benefits:
✓ Fail fast instead of wasting resources
✓ Prevent cascade failures
✓ Better observability

Libraries:
- Polly (Resilience library for .NET)
- Use: await _circuitBreaker.ExecuteAsync(() => NagerClient.GetAsync(...))

Estimated effort: 1 day
```

---

### **⭐ NICE-TO-HAVE - Advanced Features**

#### **12.7 Implement Dead Letter Queue (DLQ)**
```
CURRENT: Failed messages might be lost
PROPOSED: Route failed messages to DLQ for manual intervention

Benefits:
✓ No lost messages
✓ Replay failed notifications
✓ Root cause analysis

Implementation:
- Configure NServiceBus DLQ endpoint
- Add DLQ consumer to Notifier.Api
- Admin console to view/replay DLQ messages
- Estimated effort: 2 days
```

#### **12.8 Add Batch Processing**
```
CURRENT: One-by-one SignalR sends for each user
PROPOSED: Batch all users in same group → one SendAsync

Benefits:
✓ Reduce WebSocket overhead
✓ Better throughput for large subscriber count

Implementation:
await _hubContext.Clients
    .Groups(message.UserIds)  // SendAsync with multiple groups
    .SendAsync("HolidayDetected", payload);

Estimated effort: 1 day
```

#### **12.9 Multi-Region Deployment**
```
CURRENT: Single instance per service
PROPOSED: Load-balanced multi-instance with auto-scaling

Benefits:
✓ High availability
✓ Geographic distribution
✓ Auto-recovery from instance failure

Technologies:
- Kubernetes or Azure Container Instances
- RabbitMQ clustering for message broker HA
- MongoDB replica set for data HA
- Estimated effort: 5+ days
```

#### **12.10 Implement Subscription Preferences**
```
CURRENT: Binary subscribe/unsubscribe
PROPOSED: Granular notification settings

Examples:
- Only notify for "major" holidays
- Quiet hours (9 PM - 8 AM)
- Daily digest instead of real-time
- Notification channel preference (Email, SMS, Push)

Implementation:
- Extend Subscriptions schema with NotificationPreferences
- Add rules engine in NotifyUsersCommandHandler
- Estimated effort: 3-4 days
```

---

### **🔐 SECURITY IMPROVEMENTS**

#### **12.11 Authentication & Authorization**
```
CURRENT: [AllowAnonymous] on SignalR hub
PROPOSED: JWT-based authentication

Implementation:
- Generate JWT on Dashboard login
- Validate JWT in NotificationHub.OnConnectedAsync
- Claim-based authorization: Only receive notifications for own user
- Estimated effort: 2 days

Code:
public override async Task OnConnectedAsync()
{
    var token = Context.GetHttpContext()?.Request.Query["access_token"];
    var userId = ValidateAndExtractUserId(token);
    await Groups.AddToGroupAsync(Context.ConnectionId, userId);
}
```

#### **12.12 Input Validation & Rate Limiting**
```
CURRENT: No validation on subscription endpoints
PROPOSED: Add FluentValidation + rate limiting

Benefits:
✓ Prevent injection attacks
✓ Prevent abuse (excessive subscriptions)
✓ input validation

Implementation:
- FluentValidation for all DTOs
- Rate limiting middleware: X subscriptions per user per hour
- Estimated effort: 1-2 days
```

---

### **📊 OBSERVABILITY ENHANCEMENTS**

#### **12.13 Custom Metrics**
```
Add Prometheus metrics:
- holiday_detected_total (counter) - by country
- subscription_count (gauge) - by country
- notification_latency_ms (histogram)
- poll_duration_ms (histogram)

Benefits:
✓ Real-time dashboard (Grafana)
✓ Alerts on anomalies
✓ Capacity planning insights

Implementation:
services.AddMetrics();
var meter = new Meter("HolidayMonitor");
var holidayCounter = meter.CreateCounter<long>("holiday.detected");
holidayCounter.Add(1, new KeyValuePair<string, object?>("country", countryCode));

Estimated effort: 2 days
```

#### **12.14 Distributed Tracing - Sampling & Tail-Based Sampling**
```
CURRENT: Traces all requests
PROPOSED: Head-based sampling (1%) + tail-based sampling for errors

Benefits:
✓ Reduce storage costs
✓ Focus on important traces
✓ Faster query performance

Implementation:
- Jaeger tail sampling processor for error traces
- Keep 100% of PublicHolidayDetected traces (low volume)
- Estimated effort: 1 day
```

---

### **🏗️ ARCHITECTURAL EVOLUTION (Long-term)**

#### **12.15 CQRS Pattern**
```
CURRENT: Commands & queries mixed in services
PROPOSED: Separate read & write models

Benefits:
✓ Independent scaling of reads vs writes
✓ Optimized query model (ProjectionDB)
✓ Event-sourcing friendly

Implementation:
- Write model: Handle PublicHolidayDetected → Update MongoDB
- Read model: Projection service → Update read-optimized collection
- UI queries read model instead of write model
- Estimated effort: 5+ days
```

#### **12.16 Saga Pattern for Distributed Transactions**
```
CURRENT: Event handler is synchronous (all-or-nothing)
PROPOSED: Implement saga for complex workflows

Example: Holiday notification with email fallback
Step 1: Try SignalR push
Step 2: If fails after X seconds, send email
Step 3: Log to audit table

Implementation:
- NServiceBus Saga: Multi-step workflow
- Timeout handling between steps
- Compensating transaction on failure
- Estimated effort: 3-4 days
```

---

## 13. Security Architecture

```
┌─────────────────────────────────────────────────────────────┐
│ SECURITY LAYERS                                             │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│ Layer 1: Network                                            │
│ ├─ HTTPS/TLS for all API calls                             │
│ ├─ WSS (WebSocket Secure) for SignalR                      │
│ └─ RabbitMQ AMQPS encryption (future)                      │
│                                                             │
│ Layer 2: Authentication                                    │
│ ├─ JWT tokens for API endpoints                            │
│ ├─ SignalR: Token validation on connection                 │
│ └─ Service-to-service: mTLS (future)                       │
│                                                             │
│ Layer 3: Authorization                                     │
│ ├─ SignalR Group-based (current)                           │
│ ├─ Claim-based access control (future)                     │
│ └─ Row-level security in MongoDB (future)                  │
│                                                             │
│ Layer 4: Data Protection                                   │
│ ├─ MongoDB encryption at rest (future)                     │
│ ├─ Sensitive data masking in logs                          │
│ └─ Data classification & handling                          │
│                                                             │
│ Layer 5: API Security                                      │
│ ├─ Input validation (FluentValidation)                     │
│ ├─ Rate limiting per user                                  │
│ ├─ CORS policy validation                                  │
│ └─ SQL injection prevention (no SQL, using ODM)            │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 14. Deployment Architecture

### **Current: Docker Compose (Development)**
```yaml
version: '3.8'
services:
  rabbitmq:
    image: rabbitmq:3.12-management
    ports:
      - "5672:5672"      # AMQP
      - "15672:15672"    # Management UI
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: guest

  mongodb:
    image: mongo:6.0
    ports:
      - "27017:27017"
    volumes:
      - mongodb_data:/data/db

  otel-collector:
    image: otel/opentelemetry-collector:latest
    ports:
      - "4317:4317"      # OTLP gRPC
    volumes:
      - ./otel-collector-config.yaml:/etc/otel/config.yaml
    command: ["--config=/etc/otel/config.yaml"]

  # Services run locally via dotnet run
```

### **Recommended: Kubernetes Production**
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: notifier-api
spec:
  replicas: 3
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0
  template:
    spec:
      containers:
      - name: notifier-api
        image: holidaymonitor/notifier-api:latest
        ports:
        - containerPort: 5000
        livenessProbe:
          httpGet:
            path: /health/live
            port: 5000
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 5000
          initialDelaySeconds: 10
          periodSeconds: 5
        resources:
          requests:
            memory: "256Mi"
            cpu: "100m"
          limits:
            memory: "512Mi"
            cpu: "500m"
---
apiVersion: v1
kind: Service
metadata:
  name: notifier-api
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 5000
    protocol: TCP
  selector:
    app: notifier-api
```

---

## 15. Testing Strategy

### **Unit Tests**
```csharp
[TestClass]
public class NotifyUsersCommandHandlerTests
{
    [TestMethod]
    public async Task Handle_WithMultipleUsers_SendsToAllGroups()
    {
        // Arrange
        var handler = new NotifyUsersCommandHandler(mockHub, mockLogger);
        var command = new NotifyUsersCommand
        {
            UserIds = new List<string> { "alice", "bob", "charlie" },
            CountryCode = "US",
            // ... other properties
        };

        // Act
        await handler.Handle(command, mockContext);

        // Assert
        mockHub.Verify(h => h.Clients.Group("alice").SendAsync(...), Times.Once);
        mockHub.Verify(h => h.Clients.Group("bob").SendAsync(...), Times.Once);
        mockHub.Verify(h => h.Clients.Group("charlie").SendAsync(...), Times.Once);
    }
}
```

### **Integration Tests**
```csharp
[TestClass]
public class HolidayNotificationIntegrationTests
{
    [TestMethod]
    public async Task FullFlow_PublicHolidayDetected_ReachesSignalRClient()
    {
        // Start all services in-memory
        using var testHost = new ServiceHost();
        
        // Connect SignalR client
        var client = new HubConnectionBuilder()
            .WithUrl($"http://localhost:5000/hubs/notifications")
            .Build();
        await client.StartAsync();

        // Publish holiday event
        var publishEndpoint = testHost.Resolve<IPublishEndpoint>();
        await publishEndpoint.Publish(new PublicHolidayDetected
        {
            CountryCode = "US",
            // ... properties
        });

        // Assert SignalR received message
        var receivedMessage = await client.InvokeAsync<object>("WaitForHolidayDetected", TimeSpan.FromSeconds(5));
        Assert.IsNotNull(receivedMessage);
    }
}
```

---

## 16. Monitoring & Alerting

### **Key Metrics to Monitor**

```
┌─────────────────────────────────────────────────────┐
│ CRITICAL ALERTS                                     │
├─────────────────────────────────────────────────────┤
│                                                     │
│ 1. RabbitMQ Queue Depth > 1000 messages            │
│    Action: Scale up UserPref.Api or Notifier.Api  │
│                                                     │
│ 2. Nager.Date API Error Rate > 5%                   │
│    Action: Page on-call, investigate external API  │
│                                                     │
│ 3. SignalR Connection Drop Rate > 10%              │
│    Action: Check network, WebSocket proxy config   │
│                                                     │
│ 4. Notification Latency > 5 seconds                │
│    Action: Investigate RabbitMQ, MongoDB          │
│                                                     │
│ 5. MongoDB Disk Usage > 80%                        │
│    Action: Archive old data, add capacity          │
│                                                     │
└─────────────────────────────────────────────────────┘
```

### **Dashboard Panels**

```
┌──────────────────┬──────────────────┬──────────────────┐
│ Holidays/Hour    │ Subscriptions    │ Active Conn.     │
│ [▁▂▃▄▅▆▇█]       │ [▁▂▃▄▅▆▇█]       │ [▁▂▃▄▅▆▇█]       │
│ Avg: 2           │ Total: 5,234     │ Total: 1,234     │
└──────────────────┴──────────────────┴──────────────────┘
┌──────────────────┬──────────────────┬──────────────────┐
│ E2E Latency      │ Message Queue    │ Error Rate       │
│ [▁▂▃▄▅▆▇█]       │ [▁▂▃▄▅▆▇█]       │ [▁▂▃▄▅▆▇█]       │
│ p99: 1.2s        │ Depth: 42        │ 0.03%            │
└──────────────────┴──────────────────┴──────────────────┘
```

---

## 17. Summary of Recommendations

| Priority | Feature | Effort | Impact | Status |
|----------|---------|--------|--------|--------|
| 🔴 HIGH | Event Sourcing | 2-3d | 📊 Audit trail, debugging | Not Started |
| 🔴 HIGH | Deduplication Cache | 1d | ⚡ Reduced traffic, UX | Not Started |
| 🔴 HIGH | Health Checks | 1d | 🏥 Observable, K8s-ready | Not Started |
| 🟡 MEDIUM | Message Retry Policy | 1d | 🛡️ Resilience | Not Started |
| 🟡 MEDIUM | Redis Caching | 1-2d | ⚙️ 90% perf improvement | Not Started |
| 🟡 MEDIUM | Circuit Breaker | 1d | 🔌 Fail fast | Not Started |
| 🟢 NICE | DLQ Implementation | 2d | 📮 No lost messages | Not Started |
| 🟢 NICE | Batch Processing | 1d | 📦 Throughput | Not Started |
| 🟢 NICE | Multi-Region | 5+d | 🌍 HA, scaling | Not Started |
| 🔐 SEC | JWT Authentication | 2d | 🔐 Secure | Not Started |
| 🔐 SEC | Input Validation | 1-2d | 🛡️ Attack prevention | Not Started |
| 📊 OBS | Custom Metrics | 2d | 📈 Visibility | Not Started |
| 🏗️ ARCH | CQRS Pattern | 5+d | 📏 Scalability | Not Started |

---

## 18. Quick Reference: Service Ports

```
Service              | Port | Protocol | Purpose
─────────────────────┼──────┼──────────┼─────────────────────
Dashboard.UI         | 5173 | HTTP     | React app (dev)
Notifier.Api         | 5000 | HTTP+WS  | SignalR hub
UserPref.Api         | 5001 | HTTP+REST| Subscriptions API
HolidayScraper.Api   | 5002 | -        | Background worker
RabbitMQ AMQP        | 5672 | AMQP     | Message broker
RabbitMQ Management  | 15672| HTTP     | Admin console
MongoDB              | 27017| MongoDB  | Database
OTEL Collector       | 4317 | gRPC     | Trace export
Jaeger UI            | 16686| HTTP     | Trace viewer
```

---

## 19. Conclusion

The **Holiday Monitor** architecture is a well-designed, event-driven microservices system with:
- ✅ **Clear separation of concerns** (Polling → Processing → Notification)
- ✅ **Real-time capabilities** via SignalR WebSockets
- ✅ **Observable** with OpenTelemetry tracing
- ✅ **Scalable** message-driven design with RabbitMQ

**Next steps:**
1. Implement High-Priority improvements (Health checks, Deduplication)
2. Add JWT authentication for production readiness
3. Set up comprehensive monitoring/alerting
4. Plan multi-instance deployment with Kubernetes
5. Evaluate CQRS pattern for future scaling needs

