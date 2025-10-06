namespace Skynet.Core.Licensing;

public sealed record ClientInitMeta
{
    public string Name { get; }
    public string? Description { get; }
    public string? Address { get; }
    public string? ContactName { get; }   // Ansprechpartner
    public string? ContactEmail { get; }  // E-Mail
    public string? ContactPhone { get; }  // Telefon
    public string? ClientId { get; }      // optionale eindeutige Client-/Geräte-ID
    public string? DeviceId { get; }      // optionale Hardware/Device-ID
    public IReadOnlyDictionary<string, string>? Tags { get; }

    public ClientInitMeta(
        string name,
        string? description = null,
        string? address = null,
        string? contactName = null,
        string? contactEmail = null,
        string? contactPhone = null,
        string? clientId = null,
        string? deviceId = null,
        IReadOnlyDictionary<string, string>? tags = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must not be empty.", nameof(name));

        if (!string.IsNullOrWhiteSpace(contactEmail) && !IsLikelyEmail(contactEmail))
            throw new ArgumentException("ContactEmail is not a valid email-like string.", nameof(contactEmail));

        if (!string.IsNullOrWhiteSpace(contactPhone) && !IsLikelyPhone(contactPhone))
            throw new ArgumentException("ContactPhone is not a valid phone-like string.", nameof(contactPhone));

        Name = name;
        Description = description;
        Address = address;
        ContactName = contactName;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        ClientId = clientId;
        DeviceId = deviceId;
        Tags = tags;
    }

    private static bool IsLikelyEmail(string s)
        => s.Contains('@') && s.Contains('.') && s.IndexOf('@') > 0 && s.LastIndexOf('.') > s.IndexOf('@') + 1;

    private static bool IsLikelyPhone(string s)
    {
        int digits = 0;
        foreach (var ch in s)
        {
            if (char.IsDigit(ch)) digits++;
            else if (ch is '+' or '-' or ' ' or '(' or ')') { /* allow */ }
            else return false;
        }
        return digits >= 6;
    }
}