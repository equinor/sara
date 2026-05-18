# api.Tests

xUnit v3 tests for the SARA API. Tests use real PostgreSQL via Testcontainers
and recording fakes for external I/O (MQTT, HTTP, email).

## Running

```bash
dotnet test                                        # all tests
dotnet test --filter "FullyQualifiedName~MyTest"   # one test
```

Docker must be running.

## Writing a new test

Copy an existing file in `Services/` as a template. Minimum skeleton:

```csharp
public class MyServiceTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private TestWebApplicationFactory<Program> _factory = null!;
    private SaraDbContext _context = null!;
    private DatabaseUtilities _db = null!;

    public async ValueTask InitializeAsync()
    {
        (_container, string cs) = await TestSetupHelpers.ConfigurePostgreSqlDatabase();
        _factory = TestSetupHelpers.ConfigureWebApplicationFactory(cs);
        _ = _factory.Services;
        _context = TestSetupHelpers.ConfigurePostgreSqlContext(cs);
        _db = new DatabaseUtilities(_context);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _factory.DisposeAsync();
        await _container.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task DoThing_ValidInput_UpdatesStatus()
    {
        var workflow = await _db.NewWorkflow(...);

        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMyService>();
        await service.DoThing(workflow.Id);

        await _context.Entry(workflow).ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowStatus.Succeeded, workflow.Status);
    }
}
```

Three rules:

1. Seed via `_db` (`DatabaseUtilities`) — never construct entities by hand.
2. Resolve services from `_factory.Services.CreateScope()` — never `new` them.
3. If your test needs a workflow or analysis config, use the existing entries
   in `appsettings.Test.json` or add a new one there.

## Recording fakes

| Fake | What it records |
|---|---|
| `_factory.MqttPublisher` | Published MQTT messages |
| `_factory.ArgoHttpHandler` | Outbound HTTP requests; settable response |
| `_factory.EmailService` | Sent emails; `ThrowOnSend` to simulate failure |

Assert against these directly — no DB roundtrip needed.

## When to reload entities

The test's `_context` and the service's context are two different
`SaraDbContext` instances pointing at the same database, and EF caches
entities per context. If the service writes to an entity, the test's local
reference goes stale.

**Rule:** reload before asserting on a changed DB object.

```csharp
await _context.Entry(workflow).ReloadAsync(TestContext.Current.CancellationToken);
Assert.Equal(WorkflowStatus.Failed, workflow.Status);
```

You don't need to reload when asserting on recording fakes, on a thrown
exception, or on an entity the service didn't write to.
