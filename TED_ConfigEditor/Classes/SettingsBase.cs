using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace TED_ConfigEditor.Classes
{
    public abstract class SettingsBase<T> where T : SettingsBase<T>, new()
    {
        [JsonIgnore]
        public string FilePath { get; private set; }

        public bool Save(string filePath)
        {
            FilePath = filePath;

            return SaveInternal(this, FilePath, true);
        }

        public bool Save()
        {
            return Save(FilePath);
        }

        public async Task SaveAsync(string filePath)
        {
            await Task.Run(() => Save(filePath));
        }

        public async Task SaveAsync()
        {
            await SaveAsync(FilePath);
        }

        public static T Load(string filePath)
        {
            T setting = LoadInternal(filePath);

            if (setting != null)
            {
                setting.FilePath = filePath;
            }

            return setting;
        }

        private static bool SaveInternal(object obj, string filePath, bool createBackup)
        {
            string typeName = obj.GetType().Name;
            Console.WriteLine(@"{0} save started: {1}", typeName, filePath);

            bool isSuccess = false;

            try
            {
                if (!string.IsNullOrEmpty(filePath))
                {
                    lock (obj)
                    {
                        if (!string.IsNullOrEmpty(filePath))
                        {
                            var dir = Path.GetDirectoryName(filePath);
                            if(!Directory.Exists(dir))
                                Directory.CreateDirectory(filePath);
                        }

                        string tempFilePath = filePath + ".temp";

                        using (FileStream fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                        using (StreamWriter streamWriter = new StreamWriter(fileStream))
                        using (JsonTextWriter jsonWriter = new JsonTextWriter(streamWriter))
                        {
                            jsonWriter.Formatting = Formatting.Indented;
                            JsonSerializer serializer = new JsonSerializer {ContractResolver = new WritablePropertiesOnlyResolver()};
                            serializer.Converters.Add(new StringEnumConverter());
                            serializer.Serialize(jsonWriter, obj);
                            jsonWriter.Flush();
                        }

                        if (File.Exists(filePath))
                        {
                            if (createBackup)
                            {
                                File.Copy(filePath, filePath + ".bak", true);
                            }

                            File.Delete(filePath);
                        }

                        File.Move(tempFilePath, filePath);

                        isSuccess = true;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                Console.WriteLine(@"{0} save {1}: {2}", typeName, isSuccess ? "successful" : "failed", filePath);
            }

            return isSuccess;
        }

        internal class WritablePropertiesOnlyResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                IList<JsonProperty> props = base.CreateProperties(type, memberSerialization);
                return props.Where(p => p.Writable).ToList();
            }
        }

        private static T LoadInternal(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                //Console.WriteLine("{0} load started: {1}", typeName, filePath);

                try
                {
                    if (File.Exists(filePath))
                    {
                        using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            if (fileStream.Length > 0)
                            {
                                T settings;

                                using (var streamReader = new StreamReader(fileStream))
                                using (var jsonReader = new JsonTextReader(streamReader))
                                {
                                    var serializer = new JsonSerializer();
                                    serializer.Converters.Add(new StringEnumConverter());
                                    serializer.ObjectCreationHandling = ObjectCreationHandling.Replace;
                                    serializer.Error += (sender, e) => e.ErrorContext.Handled = true;
                                    settings = serializer.Deserialize<T>(jsonReader);
                                }

                                return settings;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    return null;
                }
            }
            return null;
        }
    }
}