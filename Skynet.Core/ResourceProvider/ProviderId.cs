namespace Skynet.Core.ResourceProvider;

/// <summary>Guid-based tenant identifier (value object).</summary>
public readonly struct ProviderId : IEquatable<ProviderId>
{
    public Guid Value { get; }
    public ProviderId(Guid value) => Value = value;

    public static ProviderId New() => new ProviderId(Guid.NewGuid());

    public bool Equals(ProviderId other) => Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is ProviderId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(ProviderId left, ProviderId right) => left.Equals(right);
    public static bool operator !=(ProviderId left, ProviderId right) => !left.Equals(right);

    public override string ToString() => Value.ToString();
    public static implicit operator Guid(ProviderId id) => id.Value;
    public static explicit operator ProviderId(Guid value) => new ProviderId(value);
}