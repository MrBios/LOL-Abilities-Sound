using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace LOL_abilities_sound
{
    internal static class Config
    {
        #region Constants
        private const string CONFIG_FILE_PATH = "config.json";
        private const string SOUNDS_CONFIG_FILE_PATH = "sounds_config.json";
        private const string IMAGES_NAMESPACE = "LOL_abilities_sound.Images";
        private const int DEFAULT_MICROPHONE_INDEX = 0;
        private const int DEFAULT_DELAY_SECONDS = 0;
        #endregion

        #region Fields
        private static Dictionary<string, string> _config;
        private static Dictionary<string, string> _soundsConfig;
        #endregion

        #region Constructor
        static Config()
        {
            try
            {
                LoadConfiguration();
                System.Diagnostics.Debug.WriteLine("Configuration loaded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading configuration: {ex.Message}");
                InitializeDefaultConfiguration();
            }
        }
        #endregion

        #region Properties
        public static string LolDirectoryPath => GetConfigValue("LolDirectoryPath");
        
        public static int findGameDelay
        {
            get
            {
                try
                {
                    return int.Parse(GetConfigValue("findGameDelayS")) * 1000;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing findGameDelay: {ex.Message}");
                    return 5000; // Default 5 seconds
                }
            }
        }
        #endregion

        #region Public Methods - Volume Settings
        public static float getSoundVolume()
        {
            try
            {
                return float.Parse(GetConfigValue("soundVolume"), CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting sound volume: {ex.Message}");
                return 1.0f; // Default volume
            }
        }

        public static void setSoundVolume(float volume)
        {
            try
            {
                if (volume < 0 || volume > 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(volume), "Volume must be between 0 and 1");
                }

                SetConfigValue("soundVolume", volume.ToString(CultureInfo.InvariantCulture));
                SaveConfiguration();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting sound volume: {ex.Message}");
            }
        }

        public static float getSelfSoundVolume()
        {
            try
            {
                return float.Parse(GetConfigValue("selfSoundVolume"), CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting self sound volume: {ex.Message}");
                return 1.0f; // Default volume
            }
        }

        public static void setSelfSoundVolume(float volume)
        {
            try
            {
                if (volume < 0 || volume > 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(volume), "Volume must be between 0 and 1");
                }

                SetConfigValue("selfSoundVolume", volume.ToString(CultureInfo.InvariantCulture));
                SaveConfiguration();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting self sound volume: {ex.Message}");
            }
        }
        #endregion

        #region Public Methods - Microphone Settings
        public static int getMicrophoneIndex()
        {
            try
            {
                if (_config.ContainsKey("microphoneIndex"))
                {
                    return int.Parse(GetConfigValue("microphoneIndex"));
                }
                return DEFAULT_MICROPHONE_INDEX;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting microphone index: {ex.Message}");
                return DEFAULT_MICROPHONE_INDEX;
            }
        }

        public static void setMicrophoneIndex(int index)
        {
            try
            {
                if (index < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), "Microphone index cannot be negative");
                }

                SetConfigValue("microphoneIndex", index.ToString());
                SaveConfiguration();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting microphone index: {ex.Message}");
            }
        }
        #endregion

        #region Public Methods - Image Resources
        public static BitmapImage getAbilitySource(int abilityIndex)
        {
            try
            {
                string abilityName = GetAbilityName(abilityIndex);
                return LoadImageResource($"{abilityName}_square.png");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting ability source for index {abilityIndex}: {ex.Message}");
                return new BitmapImage();
            }
        }

        public static BitmapImage saveIcon()
        {
            return LoadImageResource("saveIcon.png");
        }

        public static BitmapImage deleteIcon()
        {
            return LoadImageResource("deleteIcon.png");
        }

        public static BitmapImage logo()
        {
            return LoadImageResource("logo.png");
        }

        public static BitmapImage backIcon()
        {
            return LoadImageResource("backIcon.png");
        }

        public static BitmapImage notInGameSplashSource()
        {
            return LoadImageResource("notInGameSplash.jpg");
        }

        public static BitmapImage noPhotoSplashSource()
        {
            return LoadImageResource("noPhotoSplash.jpg");
        }

        public static BitmapImage noIconSource()
        {
            return LoadImageResource("noIcon.png");
        }
        #endregion

        #region Public Methods - Ability Sound Management
        public static string getAbilitySound(int championId, int abilityIndex)
        {
            try
            {
                ValidateAbilityParameters(championId, abilityIndex);
                string key = CreateSoundKey(championId, abilityIndex);
                
                return _soundsConfig.ContainsKey(key) ? _soundsConfig[key] : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting ability sound: {ex.Message}");
                return null;
            }
        }

        public static int getAbilityDelaySeconds(int championId, int abilityIndex)
        {
            try
            {
                ValidateAbilityParameters(championId, abilityIndex);
                string key = CreateDelayKey(championId, abilityIndex);
                
                if (_soundsConfig.ContainsKey(key))
                {
                    return int.Parse(_soundsConfig[key]);
                }
                return DEFAULT_DELAY_SECONDS;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting ability delay: {ex.Message}");
                return DEFAULT_DELAY_SECONDS;
            }
        }

        public static void setAbilitySound(int championId, int abilityIndex, string soundPath)
        {
            try
            {
                ValidateAbilityParameters(championId, abilityIndex);
                
                if (string.IsNullOrWhiteSpace(soundPath))
                {
                    throw new ArgumentException("Sound path cannot be null or empty", nameof(soundPath));
                }

                string key = CreateSoundKey(championId, abilityIndex);
                _soundsConfig[key] = soundPath;
                SaveSoundsConfiguration();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting ability sound: {ex.Message}");
            }
        }

        public static void removeAbilitySound(int championId, int abilityIndex)
        {
            try
            {
                ValidateAbilityParameters(championId, abilityIndex);
                string key = CreateSoundKey(championId, abilityIndex);
                
                if (_soundsConfig.ContainsKey(key))
                {
                    _soundsConfig.Remove(key);
                    SaveSoundsConfiguration();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing ability sound: {ex.Message}");
            }
        }

        public static void setAbilityDelaySeconds(int championId, int abilityIndex, int delaySeconds)
        {
            try
            {
                ValidateAbilityParameters(championId, abilityIndex);
                
                if (delaySeconds < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(delaySeconds), "Delay cannot be negative");
                }

                if (delaySeconds == 0)
                {
                    removeAbilityDelaySeconds(championId, abilityIndex);
                    return;
                }

                string key = CreateDelayKey(championId, abilityIndex);
                _soundsConfig[key] = delaySeconds.ToString();
                SaveSoundsConfiguration();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting ability delay: {ex.Message}");
            }
        }

        public static void removeAbilityDelaySeconds(int championId, int abilityIndex)
        {
            try
            {
                ValidateAbilityParameters(championId, abilityIndex);
                string key = CreateDelayKey(championId, abilityIndex);
                
                if (_soundsConfig.ContainsKey(key))
                {
                    _soundsConfig.Remove(key);
                    SaveSoundsConfiguration();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing ability delay: {ex.Message}");
            }
        }
        #endregion

        #region Private Methods - Configuration Management
        private static void LoadConfiguration()
        {
            _config = LoadJsonFile(CONFIG_FILE_PATH);
            _soundsConfig = LoadJsonFile(SOUNDS_CONFIG_FILE_PATH);
        }

        private static Dictionary<string, string> LoadJsonFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string jsonContent = File.ReadAllText(filePath);
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent) ?? new Dictionary<string, string>();
                }
                return new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading JSON file {filePath}: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }

        private static void InitializeDefaultConfiguration()
        {
            _config = new Dictionary<string, string>();
            _soundsConfig = new Dictionary<string, string>();
        }

        private static string GetConfigValue(string key)
        {
            if (_config?.ContainsKey(key) == true)
            {
                return _config[key];
            }
            throw new KeyNotFoundException($"Configuration key '{key}' not found");
        }

        private static void SetConfigValue(string key, string value)
        {
            if (_config == null)
            {
                _config = new Dictionary<string, string>();
            }
            _config[key] = value;
        }

        private static void SaveConfiguration()
        {
            try
            {
                SaveJsonFile(CONFIG_FILE_PATH, _config);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving configuration: {ex.Message}");
            }
        }

        private static void SaveSoundsConfiguration()
        {
            try
            {
                SaveJsonFile(SOUNDS_CONFIG_FILE_PATH, _soundsConfig);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving sounds configuration: {ex.Message}");
            }
        }

        private static void SaveJsonFile(string filePath, Dictionary<string, string> data)
        {
            string jsonContent = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(filePath, jsonContent);
        }
        #endregion

        #region Private Methods - Image Loading
        private static BitmapImage LoadImageResource(string resourceName)
        {
            try
            {
                string resourcePath = $"{IMAGES_NAMESPACE}.{resourceName}";
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath);
                
                if (stream == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Image resource not found: {resourcePath}");
                    return new BitmapImage();
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();

                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading image resource {resourceName}: {ex.Message}");
                return new BitmapImage();
            }
        }
        #endregion

        #region Private Methods - Ability Management
        private static string GetAbilityName(int abilityIndex)
        {
            return abilityIndex switch
            {
                1 => "Q",
                2 => "W",
                3 => "E",
                4 => "R",
                _ => throw new ArgumentException($"Invalid ability index: {abilityIndex}", nameof(abilityIndex))
            };
        }

        private static void ValidateAbilityParameters(int championId, int abilityIndex)
        {
            if (championId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(championId), "Champion ID must be positive");
            }

            if (abilityIndex < 1 || abilityIndex > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(abilityIndex), "Ability index must be between 1 and 4");
            }
        }

        private static string CreateSoundKey(int championId, int abilityIndex)
        {
            return $"sound_{championId}_{abilityIndex}";
        }

        private static string CreateDelayKey(int championId, int abilityIndex)
        {
            return $"delay_{championId}_{abilityIndex}";
        }
        #endregion
    }
}
