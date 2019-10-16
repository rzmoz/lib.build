using DotNet.Basics.Diagnostics;
using DotNet.Basics.IO;
using DotNet.Basics.Sys;
using DotNet.Basics.Sys.Text;

namespace Lib.Build
{
    public class ArgsFilesProvider
    {
        private static readonly DirPath _settingsDir = typeof(ArgsFilesProvider).Assembly.Location.ToFile().Directory;

        private readonly ILogDispatcher _log;

        public ArgsFilesProvider(ILogDispatcher log)
        {
            _log = log ?? LogDispatcher.NullLogger;
        }

        public FilePath SettingsPath(string name) => _settingsDir.ToFile($"appsettings.{name}.json");

        public void Save(string name, BuildArgs args)
        {
            var settingsFile = SettingsPath(name);
            var json = args.SerializeToJson(true);
            _log.Debug($"{nameof(BuildArgs)} initialized with:");
            _log.Verbose(json);

            settingsFile.WriteAllText(json);

            _log.Debug($"Args saved to: {settingsFile.FullName()}");
        }
        public BuildArgs Load(string name)
        {
            var settingsFile = SettingsPath(name);
            return settingsFile.Exists()
                ? settingsFile.ReadAllText().DeserializeJson<BuildArgs>()
                : null;
        }
    }
}
