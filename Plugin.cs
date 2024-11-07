using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;

namespace ProfitablePieces
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class ProfitablePiecesPlugin : BaseUnityPlugin
    {
        internal const string ModName = "ProfitablePieces";
        internal const string ModVersion = "1.0.4";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource ProfitablePiecesLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        internal static readonly ConfigSync ConfigSyncVar = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, new ConfigDescription("If on, the configuration is locked and can be changed by server admins only.", null, new ConfigurationManagerAttributes() { Order = 3 }));
            _ = ConfigSyncVar.AddLockingConfigEntry(_serverConfigLocked);

            ModEnabled = config("1 - General", "Mod Enabled", Toggle.On, new ConfigDescription("If turned off, the mod will not run the code change to return the resources used to build the piece. This affects both admins and non-admins. Basically, if you turn this off, the mod will do nothing.", null, new ConfigurationManagerAttributes() { Order = 2 }));
            AdminOnly = config("1 - General", "Admin Only", Toggle.Off, new ConfigDescription("If turned on, only admins will be able to see this mod's benefits of always giving back resources.", null, new ConfigurationManagerAttributes() { Order = 1 }));


            AlwaysDropResources = config("2 - Building", "Always Drop Resources", Toggle.On, "When destroying a building piece, setting this to true will ensure it always drops full resources.");
            AlwaysDropExcludedResources = config("2 - Building", "Always Drop Excluded Resources", Toggle.On, "When destroying a building piece, setting this to true will ensure it always drops pieces that the Valheim devs have marked as \"do not drop\".");

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                ProfitablePiecesLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                ProfitablePiecesLogger.LogError($"There was an issue loading your {ConfigFileName}");
                ProfitablePiecesLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        internal static ConfigEntry<Toggle> ModEnabled = null!;
        internal static ConfigEntry<Toggle> AdminOnly = null!;
        internal static ConfigEntry<Toggle> AlwaysDropResources = null!;
        internal static ConfigEntry<Toggle> AlwaysDropExcludedResources = null!;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription = new(description.Description + (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"), description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSyncVar.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order;
            [UsedImplicitly] public bool? Browsable;
            [UsedImplicitly] public string? Category;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        }

        #endregion
    }
}