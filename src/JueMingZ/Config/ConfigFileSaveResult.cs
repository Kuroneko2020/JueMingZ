using System;

namespace JueMingZ.Config
{
    public sealed class ConfigFileSaveResult
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool Succeeded { get; set; }
        public string Error { get; set; }

        public ConfigFileSaveResult()
        {
            Name = string.Empty;
            Path = string.Empty;
            Error = string.Empty;
        }

        public static ConfigFileSaveResult Success(string name, string path)
        {
            return new ConfigFileSaveResult
            {
                Name = name ?? string.Empty,
                Path = path ?? string.Empty,
                Succeeded = true,
                Error = string.Empty
            };
        }

        public static ConfigFileSaveResult Failure(string name, string path, string error)
        {
            return new ConfigFileSaveResult
            {
                Name = name ?? string.Empty,
                Path = path ?? string.Empty,
                Succeeded = false,
                Error = error ?? string.Empty
            };
        }
    }
}
