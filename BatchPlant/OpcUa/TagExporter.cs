namespace BatchPlant.OpcUa
{
    /// <summary>
    /// Exports the OPC UA address space as a filesystem folder structure.
    /// Each folder in the address space becomes a directory, each tag becomes a file
    /// containing its metadata (data type, unit, description, initial value).
    /// </summary>
    public static class TagExporter
    {
        public static void ExportToDirectory(TagRegistry registry, string rootPath)
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
            Directory.CreateDirectory(rootPath);

            int folderCount = 0;
            int tagCount = 0;

            foreach (var tag in registry.GetAllDefinitions())
            {
                // Build the full directory path from the tag's folder path
                var folderParts = tag.FolderPath.Split('/');
                var fullDir = Path.Combine(new[] { rootPath }.Concat(folderParts).ToArray());

                if (!Directory.Exists(fullDir))
                {
                    Directory.CreateDirectory(fullDir);
                    folderCount++;
                }

                // Sanitize filename (replace characters that are invalid on Windows filesystems)
                var safeName = SanitizeFileName(tag.Name);
                var filePath = Path.Combine(fullDir, safeName + ".tag");

                var content = BuildTagFileContent(tag);
                File.WriteAllText(filePath, content);
                tagCount++;
            }

            Console.WriteLine($"[EXPORT] Wrote {tagCount} tags across folder tree at: {rootPath}");
        }

        private static string SanitizeFileName(string name)
        {
            // Windows filesystem doesn't allow : < > \ / | ? * in filenames
            // Our tag names use - _ which are fine. But replace / just in case.
            var invalid = Path.GetInvalidFileNameChars();
            var result = name;
            foreach (var c in invalid)
            {
                result = result.Replace(c, '_');
            }
            return result;
        }

        private static string BuildTagFileContent(TagDefinition tag)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Tag Name    : {tag.Name}");
            sb.AppendLine($"Data Type   : {tag.DataType}");
            sb.AppendLine($"Folder Path : {tag.FolderPath}");
            if (!string.IsNullOrEmpty(tag.Unit))
                sb.AppendLine($"Unit        : {tag.Unit}");
            if (!string.IsNullOrEmpty(tag.Description))
                sb.AppendLine($"Description : {tag.Description}");
            if (tag.InitialValue != null)
                sb.AppendLine($"Initial     : {tag.InitialValue}");
            sb.AppendLine($"Writable    : {tag.Writable}");
            return sb.ToString();
        }
    }
}
