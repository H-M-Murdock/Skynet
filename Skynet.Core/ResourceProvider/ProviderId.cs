namespace Skynet.Core.ResourceProvider;

/// <summary>
/// Guid-based provider identifier (value object).
/// </summary>
public readonly struct ProviderId : IEquatable<ProviderId>, IComparable<ProviderId>
{
    public Guid Value { get; }

    public static readonly ProviderId Empty = new(Guid.Empty);

    public ProviderId(Guid value) => Value = value;

    public static ProviderId New() => new(Guid.NewGuid());
    
    public static ProviderId Parse(string input) => new(Guid.Parse(input));
    
    public static bool TryParse(string? input, out ProviderId result)
    {
        if (Guid.TryParse(input, out var guid))
        {
            result = new ProviderId(guid);
            return true;
        }
        result = Empty;
        return false;
    }

    public bool Equals(ProviderId other) => Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is ProviderId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    
    // Ermöglicht Sortierung (z.B. in Listen oder Logs)
    public int CompareTo(ProviderId other) => Value.CompareTo(other.Value);

    public static bool operator ==(ProviderId left, ProviderId right) => left.Equals(right);
    public static bool operator !=(ProviderId left, ProviderId right) => !left.Equals(right);
    
    // Damit man IDs auch vergleichen kann (kleiner/größer)
    public static bool operator <(ProviderId left, ProviderId right) => left.CompareTo(right) < 0;
    public static bool operator >(ProviderId left, ProviderId right) => left.CompareTo(right) > 0;

    public override string ToString() => Value.ToString();
    
    public static implicit operator Guid(ProviderId id) => id.Value;
    public static explicit operator ProviderId(Guid value) => new ProviderId(value);
}