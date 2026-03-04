using System.Text.Json;

namespace TestPlanManager.Data
{
    public interface IDefaultVersionStore
    {
        int? GetDefaultSprintId();
        void SetDefaultSprintId(int? sprintId);
    }

    public class FileDefaultVersionStore : IDefaultVersionStore
    {
        private readonly string _filePath;

        public FileDefaultVersionStore(string contentRootPath)
        {
            var dataDir = Path.Combine(contentRootPath, "App_Data");
            Directory.CreateDirectory(dataDir);
            _filePath = Path.Combine(dataDir, "default-version.json");
        }

        public int? GetDefaultSprintId()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    return null;
                }

                var json = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                var model = JsonSerializer.Deserialize<DefaultVersionFileModel>(json);
                return model?.DefaultSprintId;
            }
            catch
            {
                return null;
            }
        }

        public void SetDefaultSprintId(int? sprintId)
        {
            var model = new DefaultVersionFileModel
            {
                DefaultSprintId = sprintId
            };

            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_filePath, json);
        }

        private class DefaultVersionFileModel
        {
            public int? DefaultSprintId { get; set; }
        }
    }
}
