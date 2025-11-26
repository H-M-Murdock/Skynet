using Skynet.Core.ResourceProvider;
using Skynet.Core.Tenant;

namespace Skynet.Tests.ResourceProvider;

public class EnvironmentResourceReaderTests
{
    private readonly TenantId _tenant = TenantId.New();

    // Hilfsmethode um einen "isolierten" Reader zu bauen
    private EnvironmentResourceReader CreateReader(
        Dictionary<string, string> envVars, 
        string prefix = "SKYNET", 
        EnvScope scope = EnvScope.Process)
    {
        return new EnvironmentResourceReader(
            scope, 
            10, 
            prefix,
            // Mock Getter
            (key, s) => envVars.TryGetValue(key, out var val) ? val : null,
            // Mock Lister
            (s) => envVars.Keys
        );
    }

    [Fact]
    public async Task TryGetAsync_Should_Resolve_Config_From_Normalized_Key()
    {
        // Arrange
        // Wir erwarten, dass der Reader aus Key "db/connection" -> "SKYNET_CONFIG__TENANTGUID__DB_CONNECTION" macht
        var tenantStr = _tenant.ToString().ToUpperInvariant();
        var expectedEnvKey = $"SKYNET_CONFIG__{tenantStr}__DB_CONNECTION";

        var env = new Dictionary<string, string>
        {
            { expectedEnvKey, "{ \"host\": \"localhost\" }" }
        };

        var reader = CreateReader(env);
        var req = new ResourceRequest(_tenant, "db/connection", ResourceKind.Config);

        // Act
        var result = await reader.TryGetAsync(req);

        // Assert
        Assert.Equal(ResourceLookupStatus.Found, result.Status);
        Assert.Equal("application/json", result.Resource!.ContentType); // Default für Config
        
        using var readerStream = new StreamReader(result.Resource.Content);
        var content = await readerStream.ReadToEndAsync();
        Assert.Equal("{ \"host\": \"localhost\" }", content);
    }

    [Fact]
    public async Task TryGetAsync_Should_Return_NotFound_For_Missing_Key()
    {
        var reader = CreateReader(new Dictionary<string, string>());
        var req = new ResourceRequest(_tenant, "missing", ResourceKind.Config);

        var result = await reader.TryGetAsync(req);

        Assert.Equal(ResourceLookupStatus.NotFound, result.Status);
    }
    
    [Fact]
    public async Task TryGetAsync_Should_Reject_Binary_ResourceKind()
    {
        // Asset ist per Policy nicht im Env erlaubt (siehe CanHandle)
        var reader = CreateReader(new Dictionary<string, string>());
        var req = new ResourceRequest(_tenant, "logo.png", ResourceKind.Asset);

        var result = await reader.TryGetAsync(req);

        // Entweder NotFound (mit Message) oder False bei CanHandle
        // Da TryGetAsync CanHandle aufruft:
        Assert.Equal(ResourceLookupStatus.NotFound, result.Status);
        Assert.Contains("Unsupported", result.Reason ?? "");
    }

    [Fact]
    public async Task ListKeysAsync_Should_Filter_And_Denormalize()
    {
        // Arrange
        var t = _tenant.ToString().ToUpperInvariant();
        var env = new Dictionary<string, string>
        {
            // Relevante Keys
            { $"SKYNET_CONFIG__{t}__APP_NAME", "MyApp" },
            { $"SKYNET_CONFIG__{t}__DB_HOST", "localhost" },
            { $"SKYNET_CONFIG__{t}__SUB_ITEM", "123" },
            
            // Irrelevante Keys (anderer Tenant, anderer Typ)
            { $"SKYNET_CONFIG__{Guid.NewGuid()}__OTHER", "x" },
            { $"SKYNET_SECRET__{t}__SECRET1", "s" },
            { "PATH", "/bin" }
        };

        var reader = CreateReader(env);
        var req = new ResourceRequest(_tenant, "", ResourceKind.Config);

        // Act
        var (keys, token) = await reader.ListKeysAsync(req);

        // Assert
        Assert.Null(token);
        Assert.Equal(3, keys.Count);
        
        // Beachte: NormalizeKey macht aus '/' ein '_', aber Denormalize fallbackt auf '/'
        // "APP_NAME" -> "APP/NAME"? Nein, "APP_NAME" bleibt "APP_NAME" wenn keine Unterstriche escaped wurden.
        // Die Logik war: NormalizeKey: '/' -> '_'.
        // Denormalize: '_' -> '/'.
        // Also wird aus "APP_NAME" -> "APP/NAME".
        // Das ist ein Seiteneffekt des "Best Effort" Denormalizers für ENV.
        
        Assert.Contains("APP/NAME", keys); 
        Assert.Contains("DB/HOST", keys);
        Assert.Contains("SUB/ITEM", keys);
    }
    
        // ... bestehende Tests ...

    [Fact]
    public async Task Constructor_Should_Respect_Custom_AppPrefix()
    {
        // Arrange
        var customPrefix = "MYAPP";
        var tenantStr = _tenant.ToString().ToUpperInvariant();
        var expectedKey = $"MYAPP_CONFIG__{tenantStr}__KEY";
        
        var env = new Dictionary<string, string> { { expectedKey, "value" } };
        
        // Reader mit Custom Prefix erstellen
        var reader = CreateReader(env, prefix: customPrefix);
        var req = new ResourceRequest(_tenant, "key", ResourceKind.Config);

        // Act
        var result = await reader.TryGetAsync(req);

        // Assert
        Assert.Equal(ResourceLookupStatus.Found, result.Status);
    }

    [Fact]
    public async Task TryGetAsync_Should_Pass_Correct_Scope_To_Getter()
    {
        // Arrange
        var capturedScope = (EnvScope?)null;
        
        var reader = new EnvironmentResourceReader(
            EnvScope.User, // Wir testen User-Scope
            10,
            "TEST",
            envGetter: (k, s) => 
            {
                capturedScope = s; // Scope abfangen
                return null; 
            },
            envLister: s => Array.Empty<string>()
        );

        var req = new ResourceRequest(_tenant, "key", ResourceKind.Config);

        // Act
        await reader.TryGetAsync(req);

        // Assert
        Assert.Equal(EnvScope.User, capturedScope);
    }

    [Fact]
    public async Task NormalizeKey_Should_Handle_Duplicate_Separators_Robustly()
    {
        // Arrange: Input mit doppelten Punkten/Slashes
        // Logik: Normalize macht aus JEDEM Trenner einen '_'. 
        // "a..b//c" -> "A__B__C"
        
        var tenantStr = _tenant.ToString().ToUpperInvariant();
        // Erwartet wird, dass Sonderzeichen 1:1 in _ umgewandelt werden
        var expectedEnvKey = $"SKYNET_CONFIG__{tenantStr}__A__B__C"; 

        var env = new Dictionary<string, string> { { expectedEnvKey, "val" } };
        var reader = CreateReader(env);
        
        // Act
        var req = new ResourceRequest(_tenant, "a..b//c", ResourceKind.Config);
        var result = await reader.TryGetAsync(req);

        // Assert
        Assert.Equal(ResourceLookupStatus.Found, result.Status);
    }
}
