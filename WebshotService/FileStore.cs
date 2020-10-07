using Newtonsoft.Json;
using System.IO;

namespace WebshotService
{
    public interface IObjectStore<T>
    {
        bool Exists { get; }

        void Save(T obj);

        T Load();
    }

    public class FileStore<T> : IObjectStore<T> where T : class
    {
        public string FilePath { get; set; }
        public bool Exists => File.Exists(FilePath);

        public FileStore(string filePath)
        {
            FilePath = filePath;
        }

        public T Load()
        {
            if (!File.Exists(FilePath))
            {
                throw new FileNotFoundException("File not found.", FilePath);
            }
            var serializedContents = File.ReadAllText(FilePath);
            return JsonConvert.DeserializeObject<T>(serializedContents, JsonSettings)
                ?? throw new JsonSerializationException();
        }

        private static JsonSerializerSettings JsonSettings =>
                new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                };

        public void Save(T obj)
        {
            var dir = Path.GetDirectoryName(FilePath);

            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var serializedContents = JsonConvert.SerializeObject(obj, JsonSettings);
            File.WriteAllText(FilePath, serializedContents);
        }
    }
}