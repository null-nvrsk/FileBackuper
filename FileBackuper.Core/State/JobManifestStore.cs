using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileBackuper.Core;

public sealed class JobManifestStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string manifestsDirectory;

    public JobManifestStore(string stateDirectory)
    {
        ThrowIfNullOrWhiteSpace(stateDirectory, nameof(stateDirectory));
        manifestsDirectory = Path.Combine(Path.GetFullPath(stateDirectory), "volumes");
        Directory.CreateDirectory(manifestsDirectory);
    }

    public JobManifest? Read(string volumeId)
    {
        string manifestPath = GetManifestPath(volumeId);
        if (!File.Exists(manifestPath))
            return null;

        try
        {
            string json = File.ReadAllText(manifestPath);
            return JsonSerializer.Deserialize<JobManifest>(json, SerializerOptions)
                ?? throw new InvalidOperationException($"Манифест {manifestPath} не содержит данных.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Не удалось прочитать манифест {manifestPath}.", exception);
        }
    }

    public void Save(JobManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ThrowIfNullOrWhiteSpace(manifest.VolumeId, nameof(manifest.VolumeId));

        string manifestPath = GetManifestPath(manifest.VolumeId);
        string temporaryPath = manifestPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            string json = JsonSerializer.Serialize(manifest, SerializerOptions);
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, manifestPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public void DeleteCompleted()
    {
        foreach (string manifestPath in Directory.EnumerateFiles(manifestsDirectory, "*.json"))
        {
            try
            {
                string json = File.ReadAllText(manifestPath);
                JobManifest? manifest = JsonSerializer.Deserialize<JobManifest>(json, SerializerOptions);
                if (manifest?.Status == JobStatus.Completed)
                    File.Delete(manifestPath);
            }
            catch (IOException)
            {
                // Манифест может одновременно обновляться другим процессом.
            }
            catch (JsonException)
            {
                // Повреждённый манифест сохраняем для диагностики.
            }
        }
    }

    private string GetManifestPath(string volumeId)
    {
        ThrowIfNullOrWhiteSpace(volumeId, nameof(volumeId));
        string fileName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(volumeId))) + ".json";
        return Path.Combine(manifestsDirectory, fileName);
    }

    private static void ThrowIfNullOrWhiteSpace(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Значение не может быть пустым.", parameterName);
    }
}
