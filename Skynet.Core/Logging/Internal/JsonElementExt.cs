using System.Text.Json;

namespace Skynet.Core.Logging;

static class JsonElementExt
{
    public static JsonElement? GetPropertyOrDefault(this JsonElement el, string name)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var prop) ? prop : null;
}