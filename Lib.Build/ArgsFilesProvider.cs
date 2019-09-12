using DotNet.Basics.IO;
using DotNet.Basics.Sys;
using Newtonsoft.Json;

namespace Lib.Build
{
    public class ArgsFilesProvider
    {
        private static readonly DirPath _settingsDir = typeof(ArgsFilesProvider).Assembly.Location.ToFile().Directory;

        public FilePath SettingsPath(string name) => _settingsDir.ToFile($"appsettings.{name}.json");
        
        public void Save(string name, BuildArgs args)
        {
            var settingsFile = SettingsPath(name);
            var dto = BuildArgsDto.CreateDto(args);
            settingsFile.WriteAllText(JsonConvert.SerializeObject(dto, Formatting.Indented));
        }
        public BuildArgs Load(string name)
        {
            var settingsFile = SettingsPath(name);
            if (settingsFile.Exists() == false)
                return null;

            var dto = JsonConvert.DeserializeObject<BuildArgsDto>(settingsFile.ReadAllText());
            return dto.ToArgs();
        }
    }
}
