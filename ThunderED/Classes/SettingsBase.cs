#region License Information (GPL v3)

/*
    Copyright (c) Jaex

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using ThunderED.Helpers;

namespace ThunderED.Classes
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
            T setting = LoadInternal(filePath, true);

            if (setting != null)
            {
                setting.FilePath = filePath;
            }

          /*  if (setting == null)
            {
                setting = new T {FilePath = filePath};
                setting.Save();
            }*/

            return setting;
        }

        private static bool SaveInternal(object obj, string filePath, bool createBackup)
        {
            string typeName = obj.GetType().Name;
            Console.WriteLine("{0} save started: {1}", typeName, filePath);

            bool isSuccess = false;

            try
            {
                if (!string.IsNullOrEmpty(filePath))
                {
                    lock (obj)
                    {
                        if (!string.IsNullOrEmpty(filePath) && !Directory.Exists(filePath))
                        {
                            Directory.CreateDirectory(filePath);
                        }

                        string tempFilePath = filePath + ".temp";

                        using (FileStream fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                        using (StreamWriter streamWriter = new StreamWriter(fileStream))
                        using (JsonTextWriter jsonWriter = new JsonTextWriter(streamWriter))
                        {
                            jsonWriter.Formatting = Formatting.Indented;
                            JsonSerializer serializer = new JsonSerializer();
                            serializer.ContractResolver = new WritablePropertiesOnlyResolver();
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
                Console.WriteLine("{0} save {1}: {2}", typeName, isSuccess ? "successful" : "failed", filePath);
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

        private static T LoadInternal(string filePath, bool checkBackup)
        {
            string typeName = typeof(T).Name;

            if (!string.IsNullOrEmpty(filePath))
            {
                //Console.WriteLine("{0} load started: {1}", typeName, filePath);
                if (File.Exists(filePath))
                {
                    using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (fileStream.Length > 0)
                        {
                            T settings;

                            var listErrors = new StringBuilder();
                            using (var streamReader = new StreamReader(fileStream))
                            using (var jsonReader = new JsonTextReader(streamReader))
                            {
                                var serializer = new JsonSerializer();
                                serializer.Converters.Add(new StringEnumConverter());
                                serializer.ObjectCreationHandling = ObjectCreationHandling.Replace;
                                serializer.Error += (sender, e) =>
                                {                                        
                                    e.ErrorContext.Handled = true;
                                    listErrors.Append(e.ErrorContext.Error.Message);
                                    listErrors.Append("\n");
                                };
                                settings = serializer.Deserialize<T>(jsonReader);

                                if(settings == null)
                                    throw new Exception($"Config file errors: \n{listErrors}");
                            }

                            return settings;
                        }
                    }
                }
            }
            LogHelper.LogError($"Failed to load configuration for type {typeName} from settings.json (File not found?). Default config loaded.").GetAwaiter().GetResult();
            return new T();
        }
    }
}