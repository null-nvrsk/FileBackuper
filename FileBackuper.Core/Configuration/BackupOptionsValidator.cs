namespace FileBackuper.Core;

public static class BackupOptionsValidator
{
    public static void Validate(BackupOptions options, string destinationDirectory)
    {
        if (!Enum.IsDefined(typeof(CloudFileMode), options.CloudFileMode))
            throw new InvalidOperationException(
                "Указан неизвестный режим обработки облачных файлов. Допустимые значения: FastSkip, Precise.");

        if (options.SkipDirectoryNames is null || options.SkipDirectoryNames.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("SkipDirectoryNames must not contain empty directory names.");
        }

        ValidateFileCategories(options);
        ValidateFileSizeGroups(options);

        if (options.DrivePollingIntervalSeconds <= 0)
        {
            throw new InvalidOperationException(
                "Интервал проверки дисков должен быть больше нуля.");
        }

        if (string.IsNullOrWhiteSpace(options.ScanDirectory))
            return;

        string scanDirectory = Path.GetFullPath(options.ScanDirectory);
        string normalizedDestinationDirectory = Path.GetFullPath(destinationDirectory);

        if (IsSameOrSubdirectory(scanDirectory, normalizedDestinationDirectory))
        {
            throw new InvalidOperationException(
                "Папка ScanDirectory не может совпадать с DestinationDirectory или находиться внутри неё.");
        }

        if (IsSameOrSubdirectory(normalizedDestinationDirectory, scanDirectory))
        {
            throw new InvalidOperationException(
                "Папка DestinationDirectory не может совпадать с ScanDirectory или находиться внутри неё.");
        }
    }

    private static void ValidateFileCategories(BackupOptions options)
    {
        if (options.FileCategories is null || options.FileCategories.Count == 0)
            throw new InvalidOperationException("At least one file category must be configured.");

        HashSet<string> categoryNames = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> allExtensions = new(StringComparer.OrdinalIgnoreCase);
        bool hasEnabledFileType = false;

        for (int index = 0; index < options.FileCategories.Count; index++)
        {
            FileCategoryOptions category = options.FileCategories[index]
                ?? throw new InvalidOperationException($"File category #{index + 1} is null.");
            if (string.IsNullOrWhiteSpace(category.Name))
                throw new InvalidOperationException($"File category #{index + 1} has an empty name.");
            if (!categoryNames.Add(category.Name))
                throw new InvalidOperationException($"File category name '{category.Name}' is duplicated.");
            if (category.Kind == MediaKind.Unknown || !Enum.IsDefined(typeof(MediaKind), category.Kind))
                throw new InvalidOperationException($"File category '{category.Name}' has an invalid kind.");
            if (category.Extensions is null || category.Extensions.Count == 0)
                throw new InvalidOperationException($"File category '{category.Name}' has no extensions.");
            if (category.EnabledExtensions is null)
                throw new InvalidOperationException($"File category '{category.Name}' has null EnabledExtensions.");

            HashSet<string> categoryExtensions = new(StringComparer.OrdinalIgnoreCase);
            foreach (string extension in category.Extensions)
            {
                string normalized = ValidateAndNormalizeExtension(extension, category.Name);
                if (!categoryExtensions.Add(normalized))
                    throw new InvalidOperationException(
                        $"Extension '{extension}' is duplicated in file category '{category.Name}'.");
                if (!allExtensions.Add(normalized))
                    throw new InvalidOperationException(
                        $"Extension '{extension}' is assigned to more than one file category.");
            }

            foreach (string extension in category.EnabledExtensions)
            {
                string normalized = ValidateAndNormalizeExtension(extension, category.Name);
                if (!categoryExtensions.Contains(normalized))
                    throw new InvalidOperationException(
                        $"Enabled extension '{extension}' is not listed in file category '{category.Name}'.");
            }

            hasEnabledFileType |= category.Enabled || category.EnabledExtensions.Count > 0;
        }

        if (!hasEnabledFileType)
            throw new InvalidOperationException("At least one file category or individual extension must be enabled.");
    }

    private static string ValidateAndNormalizeExtension(string extension, string categoryName)
    {
        string normalized = FileTypeSelection.NormalizeExtension(extension);
        if (normalized.Length <= 1 || normalized[1..].Any(character => !char.IsLetterOrDigit(character)))
            throw new InvalidOperationException(
                $"File category '{categoryName}' contains invalid extension '{extension}'.");
        return normalized;
    }

    private static void ValidateFileSizeGroups(BackupOptions options)
    {
        if (options.MinFileSizeBytes < 0 || options.MaxFileSizeBytes < options.MinFileSizeBytes)
            throw new InvalidOperationException("The configured minimum and maximum file sizes are invalid.");

        if (options.FileSizeGroups is null || options.FileSizeGroups.Count == 0)
            throw new InvalidOperationException("At least one file size group must be configured.");

        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < options.FileSizeGroups.Count; index++)
        {
            FileSizeGroupOptions group = options.FileSizeGroups[index]
                ?? throw new InvalidOperationException($"File size group #{index + 1} is null.");

            if (string.IsNullOrWhiteSpace(group.Name))
                throw new InvalidOperationException($"File size group #{index + 1} has an empty name.");
            if (!names.Add(group.Name))
                throw new InvalidOperationException($"File size group name '{group.Name}' is duplicated.");
            if (group.MinBytes < 0 || group.MaxBytes < group.MinBytes)
                throw new InvalidOperationException($"File size group '{group.Name}' has an invalid range.");

            if (index == 0)
            {
                if (group.MinBytes != options.MinFileSizeBytes)
                    throw new InvalidOperationException(
                        "The first file size group must start at MinFileSizeBytes.");
            }
            else
            {
                FileSizeGroupOptions previous = options.FileSizeGroups[index - 1];
                if (previous.MaxBytes == long.MaxValue || group.MinBytes != previous.MaxBytes + 1)
                    throw new InvalidOperationException(
                        $"File size groups '{previous.Name}' and '{group.Name}' must be contiguous and non-overlapping.");
            }
        }

        if (options.FileSizeGroups[^1].MaxBytes != options.MaxFileSizeBytes)
            throw new InvalidOperationException("The last file size group must end at MaxFileSizeBytes.");
    }

    private static bool IsSameOrSubdirectory(string path, string possibleParentDirectory)
    {
        string normalizedPath = Path.TrimEndingDirectorySeparator(path);
        string normalizedParentDirectory = Path.TrimEndingDirectorySeparator(possibleParentDirectory);

        return string.Equals(normalizedPath, normalizedParentDirectory, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(normalizedParentDirectory + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }
}
