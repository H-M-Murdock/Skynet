namespace Skynet.Core.Tenant;

/// <summary>
/// Identifiziert einen Mandanten eindeutig im System (Strongly Typed ID).
/// Kapselt eine <see cref="Guid"/>, um Verwechslungen mit anderen IDs zu vermeiden.
/// </summary>
public readonly struct TenantId : IEquatable<TenantId>, IComparable<TenantId>, IComparable
{
    public Guid Value { get; }

    /// <summary>
    /// Repräsentiert einen leeren/nicht gesetzten Tenant (Guid.Empty).
    /// </summary>
    public static readonly TenantId Empty = new(Guid.Empty);

    public TenantId(Guid value) => Value = value;

    /// <summary>
    /// Erzeugt eine neue, zufällige TenantId.
    /// </summary>
    public static TenantId New() => new(Guid.NewGuid());

    // --- Parsing ---

    public static TenantId Parse(string input) 
        => new(Guid.Parse(input));

    public static bool TryParse(string? input, out TenantId result)
    {
        if (Guid.TryParse(input, out var guid))
        {
            result = new TenantId(guid);
            return true;
        }
        result = Empty;
        return false;
    }

    // --- Equality ---

    public bool Equals(TenantId other) => Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is TenantId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(TenantId left, TenantId right) => left.Equals(right);
    public static bool operator !=(TenantId left, TenantId right) => !left.Equals(right);

    // --- Comparable (Sortierung) ---

    public int CompareTo(TenantId other) => Value.CompareTo(other.Value);
    
    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        return obj is TenantId other 
            ? CompareTo(other) 
            : throw new ArgumentException($"Object must be of type {nameof(TenantId)}");
    }
    
    public static bool operator <(TenantId left, TenantId right) => left.CompareTo(right) < 0;
    public static bool operator <=(TenantId left, TenantId right) => left.CompareTo(right) <= 0;
    public static bool operator >(TenantId left, TenantId right) => left.CompareTo(right) > 0;
    public static bool operator >=(TenantId left, TenantId right) => left.CompareTo(right) >= 0;

    // --- Formatting & Conversion ---

    public override string ToString() => Value.ToString();
    
    /// <summary>Delegiert an Guid.ToString(format).</summary>
    public string ToString(string? format) => Value.ToString(format);

    public static implicit operator Guid(TenantId id) => id.Value;
    public static explicit operator TenantId(Guid value) => new TenantId(value);
}