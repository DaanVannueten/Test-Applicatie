using System.Text.Json;

namespace TestPlanManager.Data
{
    /// <summary>
    /// ============================================================
    /// DEFAULT VERSION STORE INTERFACE
    /// ============================================================
    /// Contract for storing and retrieving the user's default sprint selection.
    /// Allows users to have their preferred test cycle remembered across sessions.
    /// 
    /// Methods:
    /// - GetDefaultSprintId(): Returns the user's selected sprint (null if not set)
    /// - SetDefaultSprintId(): Saves the user's selected sprint
    /// ============================================================
    /// </summary>
    public interface IDefaultVersionStore
    {
        /// <summary>
        /// Gets the id of the user's default sprint/version.
        /// 
        /// Returns:
        /// - SprintId if a default was previously set
        /// - null if no default is set
        /// </summary>
        int? GetDefaultSprintId();

        /// <summary>
        /// Sets the user's default sprint/version.
        /// 
        /// Parameters:
        /// - sprintId: The SprintId to set as default (null to clear default)
        /// </summary>
        void SetDefaultSprintId(int? sprintId);
    }

    /// <summary>
    /// ============================================================
    /// FILE-BASED DEFAULT VERSION STORE IMPLEMENTATION
    /// ============================================================
    /// Persists the default sprint selection to a JSON file on disk.
    /// Uses file-based storage instead of database for simple state management.
    /// 
    /// File Location: App_Data/default-version.json
    /// 
    /// Advantages:
    /// - No database round-trip needed
    /// - Simple, human-readable JSON format
    /// - Can be manually edited if needed
    /// - Works in stateless scenarios (containers, load-balanced environments)
    /// ============================================================
    /// </summary>
    public class FileDefaultVersionStore : IDefaultVersionStore
    {
        private readonly string _filePath;  // Path to default-version.json

        public FileDefaultVersionStore(string contentRootPath)
        {
            // Ensure App_Data directory exists
            var dataDir = Path.Combine(contentRootPath, "App_Data");
            Directory.CreateDirectory(dataDir);
            
            // Store path to default-version.json file
            _filePath = Path.Combine(dataDir, "default-version.json");
        }

        /// <summary>
        /// Reads the default sprint ID from the JSON file.
        /// Returns null if file doesn't exist or is empty.
        /// Gracefully handles errors by returning null.
        /// </summary>
        public int? GetDefaultSprintId()
        {
            try
            {
                // Return null if file doesn't exist
                if (!File.Exists(_filePath))
                {
                    return null;
                }

                // Read file contents
                var json = File.ReadAllText(_filePath);
                
                // Return null if file is empty
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                // Deserialize JSON to extract SprintId
                var model = JsonSerializer.Deserialize<DefaultVersionFileModel>(json);
                return model?.DefaultSprintId;
            }
            catch
            {
                // Silently return null on any error (file corruption, permission issues, etc.)
                return null;
            }
        }

        /// <summary>
        /// Writes the default sprint ID to the JSON file.
        /// Creates or overwrites the file with new value.
        /// Uses pretty-printed JSON for readability.
        /// </summary>
        public void SetDefaultSprintId(int? sprintId)
        {
            // Create model with the sprint ID
            var model = new DefaultVersionFileModel
            {
                DefaultSprintId = sprintId
            };

            // Serialize to human-readable JSON
            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions
            {
                WriteIndented = true  // Pretty-print JSON for readability
            });

            // Write to file (creates or overwrites existing)
            File.WriteAllText(_filePath, json);
        }

        /// <summary>
        /// Internal data model matching the JSON structure.
        /// Represents the contents of default-version.json file.
        /// </summary>
        private class DefaultVersionFileModel
        {
            /// <summary>
            /// The ID of the user's default sprint, or null if not set
            /// </summary>
            public int? DefaultSprintId { get; set; }
        }
    }
}
