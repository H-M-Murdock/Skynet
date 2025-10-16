namespace Skynet.Core.Logging;

// Formatter für menschenlesbare Zeilen (Console/File), unabhängig vom Transport-Encoder.
public interface ILogTextFormatter
{
    // Formatiert ein Event als einzelne Textzeile (ohne Newline).
    string Format(ILogEvent evt);
}
