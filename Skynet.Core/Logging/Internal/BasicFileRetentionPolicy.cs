namespace Skynet.Core.Logging;

public sealed class BasicRetentionPolicy : IFileRetentionPolicy
{
    private readonly int _maxFiles;
    private readonly long _maxTotalBytes;

    /// <summary>
    /// Konfiguriert die Retention. 0 oder Infinite bedeutet "deaktiviert" für den jeweiligen Parameter.
    /// </summary>
    public BasicRetentionPolicy(int maxFiles = 5, long maxTotalBytes = 10 * 1024 * 1024)
    {
        _maxFiles = maxFiles;
        _maxTotalBytes = maxTotalBytes;
    }

    public void Apply(string directoryPath, string searchPattern)
    {
        if (!Directory.Exists(directoryPath)) return;
        try
        {
            var dirInfo = new DirectoryInfo(directoryPath);
            var files = dirInfo.GetFiles(searchPattern)
                               .OrderByDescending(f => f.LastWriteTimeUtc) // Neueste zuerst
                               .ToList();

            if (files.Count == 0) return;

            var filesToDelete = new HashSet<string>();
            long currentTotalBytes = 0;
            var now = DateTime.UtcNow;

            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                bool delete = false;

                // 2. Anzahl checken
                if (_maxFiles > 0 && i >= _maxFiles)
                {
                    delete = true;
                }

                // 3. Größe kumulieren (nur für Dateien, die wir behalten wollen)
                if (!delete)
                {
                    // Wenn das Hinzufügen dieser Datei das Limit sprengt, und es nicht die allererste ist -> löschen
                    if (_maxTotalBytes > 0 && (currentTotalBytes + file.Length) > _maxTotalBytes && i > 0)
                    {
                        delete = true;
                    }
                    else
                    {
                        currentTotalBytes += file.Length;
                    }
                }

                if (delete)
                {
                    filesToDelete.Add(file.FullName);
                }
            }

            // Löschen durchführen
            foreach (var path in filesToDelete)
            {
                try 
                { 
                    File.Delete(path); 
                } 
                catch 
                { 
                    // Ignorieren, Datei ist evtl. in Benutzung (z.B. von uns selbst, falls Logic fehlerhaft)
                }
            }
        }
        catch (Exception)
        {
            // Retention sollte niemals den App-Start crashen
        }
    }
}
