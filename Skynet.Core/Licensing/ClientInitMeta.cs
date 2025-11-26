using System.Collections.ObjectModel;

namespace Skynet.Core.Licensing;

/// <summary>
/// Metadaten zur Initialisierung eines Clients oder Mandanten.
/// Diese Daten werden bei der Lizenzanfrage übermittelt, um den Lizenznehmer zu identifizieren.
/// </summary>
public sealed record ClientInitMeta
{
    /// <summary>
    /// Name der Organisation, Firma oder des Geräts (Pflichtfeld).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Optionale Beschreibung (z. B. Abteilung, Standort).
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Optionale Anschrift.
    /// </summary>
    public string? Address { get; }

    /// <summary>
    /// Name des technischen oder organisatorischen Ansprechpartners.
    /// </summary>
    public string? ContactName { get; }

    /// <summary>
    /// E-Mail-Adresse für Benachrichtigungen (wird grob validiert).
    /// </summary>
    public string? ContactEmail { get; }

    /// <summary>
    /// Telefonnummer für Rückfragen (wird grob validiert).
    /// </summary>
    public string? ContactPhone { get; }

    /// <summary>
    /// Eindeutige Client-ID (z. B. App-Instanz-ID), falls vorhanden.
    /// </summary>
    public string? ClientId { get; }

    /// <summary>
    /// Hardware- oder Geräte-ID, falls lizensierung an Hardware gebunden ist.
    /// </summary>
    public string? DeviceId { get; }

    /// <summary>
    /// Zusätzliche Metadaten oder Tags (nie null).
    /// </summary>
    public IReadOnlyDictionary<string, string> Tags { get; }

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
            throw new ArgumentException($"ContactEmail '{contactEmail}' does not look like a valid email.", nameof(contactEmail));

        if (!string.IsNullOrWhiteSpace(contactPhone) && !IsLikelyPhone(contactPhone))
            throw new ArgumentException($"ContactPhone '{contactPhone}' does not look like a valid phone number.", nameof(contactPhone));

        Name = name;
        Description = description;
        Address = address;
        ContactName = contactName;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        ClientId = clientId;
        DeviceId = deviceId;
        
        // Verbesserung: Nie null zurückgeben, spart Null-Checks beim Konsumenten
        Tags = tags ?? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
    }

    /// <summary>
    /// Prüft grob auf E-Mail-Format (muss '@' und '.' enthalten).
    /// </summary>
    private static bool IsLikelyEmail(string s)
    {
        // Einfache Heuristik: Mindestens ein @, danach ein Punkt.
        // Verhindert grobe Tippfehler, erlaubt aber auch ungewöhnliche gültige Adressen.
        int atIndex = s.IndexOf('@');
        int lastDotIndex = s.LastIndexOf('.');
        
        return atIndex > 0              // @ nicht am Anfang
            && lastDotIndex > atIndex + 1 // Punkt muss nach @ kommen (mit mind. 1 Zeichen Abstand)
            && lastDotIndex < s.Length - 1; // Punkt nicht am Ende
    }

    /// <summary>
    /// Prüft grob auf Telefonnummer (mind. 6 Ziffern, erlaubte Sonderzeichen).
    /// </summary>
    private static bool IsLikelyPhone(string s)
    {
        int digits = 0;
        foreach (var ch in s)
        {
            if (char.IsDigit(ch)) 
            {
                digits++;
            }
            else if (ch is '+' or '-' or ' ' or '(' or ')' or '/' or '.') 
            { 
                /* erlaubte Trennzeichen */ 
            }
            else 
            { 
                return false; // Ungültiges Zeichen gefunden
            }
        }
        return digits >= 6;
    }
}