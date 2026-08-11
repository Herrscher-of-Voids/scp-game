using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Scp.Domain;

namespace Scp.Application
{
    public sealed class ScpContentLoader
    {
        private readonly JsonSerializerSettings _settings;

        public ScpContentLoader()
        {
            _settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };
            _settings.Converters.Add(new StringEnumConverter());
        }

        public ScpDefinition[] LoadDirectory(string directory)
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException(directory);
            }

            var definitions = new List<ScpDefinition>();
            var files = Directory.GetFiles(directory, "*.json");
            Array.Sort(files, StringComparer.Ordinal);
            foreach (var file in files)
            {
                var definition = JsonConvert.DeserializeObject<ScpDefinition>(File.ReadAllText(file), _settings);
                definitions.Add(definition ?? throw new JsonSerializationException($"Empty SCP definition: {file}"));
            }

            ConfigValidator.Validate(definitions.ToArray());
            return definitions.ToArray();
        }
    }
}
