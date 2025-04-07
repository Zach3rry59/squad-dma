using System.Text.Json.Serialization;
using System.Text.Json;

namespace squad_dma
{
    public class Config
    {
        [JsonPropertyName("defaultZoom")]
        public int DefaultZoom { get; set; } = 100;

        [JsonPropertyName("enemyCount")]
        public bool EnemyCount { get; set; } = false;

        [JsonPropertyName("font")]
        public int Font { get; set; } = 0;

        [JsonPropertyName("fontSize")]
        public int FontSize { get; set; } = 13;

        [JsonPropertyName("paintColors")]
        public Dictionary<string, PaintColor.Colors> PaintColors { get; set; }

        [JsonPropertyName("playerAimLine")]
        public int PlayerAimLineLength { get; set; } = 1000;

        [JsonPropertyName("uiScale")]
        public int UIScale { get; set; } = 100;

        [JsonPropertyName("techMarkerScale")]
        public int TechMarkerScale { get; set; } = 100;

        [JsonPropertyName("zoomInKey")]
        public Keys ZoomInKey { get; set; } = Keys.Up;

        [JsonPropertyName("zoomOutKey")]
        public Keys ZoomOutKey { get; set; } = Keys.Down;

        [JsonPropertyName("zoomStep")]
        public int ZoomStep { get; set; } = 1;

        [JsonPropertyName("vsync")]
        public bool VSync { get; set; } = false;

        // ESP
        [JsonPropertyName("espFontSize")]
        public float ESPFontSize { get; set; }

        [JsonPropertyName("espShowDistance")]
        public bool EspShowDistance { get; set; }

        [JsonPropertyName("espShowHealth")]
        public bool EspShowHealth { get; set; }

        [JsonPropertyName("espShowBox")]
        public bool EspShowBox { get; set; }

        [JsonPropertyName("showNames")]
        public bool ShowNames { get; set; }
        [JsonPropertyName("espMaxDistance")]
        public float EspMaxDistance { get; set; }

        [JsonPropertyName("espTextColor")]
        public PaintColor.Colors EspTextColor { get; set; }
      
        [JsonPropertyName("showEnemyDistance")]
        public bool ShowEnemyDistance { get; set; } = true;

        [JsonPropertyName("enableEsp")]
        public bool EnableEsp { get; set; }

        [JsonPropertyName("espBones")]
        public bool EspBones { get; set; }

        [JsonPropertyName("espShowAllies")]
        public bool EspShowAllies { get; set; }

        // ESP Scope Magnifications
        [JsonPropertyName("firstScopeMagnification")]
        public float FirstScopeMagnification { get; set; }

        [JsonPropertyName("secondScopeMagnification")]
        public float SecondScopeMagnification { get; set; }

        [JsonPropertyName("thirdScopeMagnification")]
        public float ThirdScopeMagnification { get; set; }

        // Writes :

        [JsonPropertyName("noRecoil")]
        public bool NoRecoil { get; set; }

        [JsonPropertyName("noSway")]
        public bool NoSway { get; set; }

        [JsonPropertyName("noCameraShake")]
        public bool NoCameraShake { get; set; }


        // Local Soldier Features
        [JsonPropertyName("disableSuppression")]
        public bool DisableSuppression { get; set; } = false;

        [JsonPropertyName("setInteractionDistances")]
        public bool SetInteractionDistances { get; set; } = false;

        [JsonPropertyName("allowShootingInMainBase")]
        public bool AllowShootingInMainBase { get; set; } = false;

        [JsonPropertyName("setSpeedHack")]
        public bool SetSpeedHack { get; set; } = false;

        [JsonPropertyName("setAirStuck")]
        public bool SetAirStuck { get; set; } = false;

        [JsonPropertyName("setHideActor")]
        public bool SetHideActor { get; set; } = false;

        [JsonPropertyName("disableCollision")]
        public bool DisableCollision { get; set; } = false;

        [JsonPropertyName("quickZoom")]
        public bool QuickZoom { get; set; } = false;

        [JsonPropertyName("rapidFire")]
        public bool RapidFire { get; set; } = false;

        [JsonPropertyName("infiniteAmmo")]
        public bool InfiniteAmmo { get; set; } = false;

        [JsonPropertyName("quickSwap")]
        public bool QuickSwap { get; set; } = false;

        [JsonPropertyName("keybindSpeedHack")]
        public Keys KeybindSpeedHack { get; set; } = Keys.None;

        [JsonPropertyName("keybindAirStuck")]
        public Keys KeybindAirStuck { get; set; } = Keys.None;

        [JsonPropertyName("keybindHideActor")]
        public Keys KeybindHideActor { get; set; } = Keys.None;

        [JsonPropertyName("keybindQuickZoom")]
        public Keys KeybindQuickZoom { get; set; } = Keys.None;

        [JsonPropertyName("keybindToggleEnemyDistance")]
        public Keys KeybindToggleEnemyDistance { get; set; } = Keys.None;

        [JsonPropertyName("keybindToggleMap")]
        public Keys KeybindToggleMap { get; set; } = Keys.None;

        [JsonPropertyName("keybindToggleFullscreen")]
        public Keys KeybindToggleFullscreen { get; set; } = Keys.None;

        [JsonPropertyName("keybindDumpNames")]
        public Keys KeybindDumpNames { get; set; } = Keys.None;

        [JsonPropertyName("keybindZoomIn")]
        public Keys KeybindZoomIn { get; set; } = Keys.Up;

        [JsonPropertyName("keybindZoomOut")]
        public Keys KeybindZoomOut { get; set; } = Keys.Down;

        #region Json Ignore
        [JsonIgnore]
        public Dictionary<string, PaintColor.Colors> DefaultPaintColors = new Dictionary<string, PaintColor.Colors>()
        {
            ["Primary"] = new PaintColor.Colors { A = 255, R = 80, G = 80, B = 80 },
            ["PrimaryDark"] = new PaintColor.Colors { A = 255, R = 50, G = 50, B = 50 },
            ["PrimaryLight"] = new PaintColor.Colors { A = 255, R = 130, G = 130, B = 130 },
            ["Accent"] = new PaintColor.Colors { A = 255, R = 255, G = 128, B = 0 },
            ["EspText"] = new PaintColor.Colors { A = 255, R = 255, G = 255, B = 255 } // ESP
        };

        [JsonIgnore]
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonKeyEnumConverter() }
        };

        [JsonIgnore]
        private static readonly object _lock = new();

        [JsonIgnore]
        private const string SettingsDirectory = "Configuration";
        public Config()
        {
            ShowEnemyDistance = true;
            DefaultZoom = 100;
            EnemyCount = false;
            Font = 0;
            FontSize = 13;
            PaintColors = DefaultPaintColors;
            PlayerAimLineLength = 1000;
            ShowNames = false;
            UIScale = 100;
            VSync = false;

            // ESP

            ESPFontSize = 10f;
            EspShowDistance = true;
            EspShowHealth = false;
            EspShowBox = false;
            EspMaxDistance = 1000f; // 1000M is max
            EspTextColor = DefaultPaintColors["EspText"];
            EnableEsp = true;
            EspBones = true;
            EspShowAllies = false;
            FirstScopeMagnification = 4.0f;  // e.g., 4x scope
            SecondScopeMagnification = 6.0f; // e.g., 6x scope
            ThirdScopeMagnification = 12.0f;  // e.g., 12x scope

            // Writes

            NoRecoil = false;
            NoSway = false;
            NoCameraShake = false;
        }

        /// <summary>
        /// Attempt to load Config.json
        /// </summary>
        /// <param name="config">'Config' instance to populate.</param>
        /// <returns></returns>
        public static bool TryLoadConfig(out Config config)
        {
            lock (_lock)
            {
                try
                {
                    Directory.CreateDirectory(SettingsDirectory);
                    var path = Path.Combine(SettingsDirectory, "Settings.json");

                    if (!File.Exists(path))
                    {
                        config = new Config();
                        SaveConfig(config);
                        return true;
                    }

                    config = JsonSerializer.Deserialize<Config>(File.ReadAllText(path), _jsonOptions);
                    return true;
                }
                catch
                {
                    config = new Config();
                    return false;
                }
            }
        }

        public static void SaveConfig(Config config)
        {
            lock (_lock)
            {
                Directory.CreateDirectory(SettingsDirectory);
                File.WriteAllText(
                    Path.Combine(SettingsDirectory, "Settings.json"),
                    JsonSerializer.Serialize(config, _jsonOptions)
                );
            }
        }
    }

    public class JsonKeyEnumConverter : JsonConverter<Keys>
    {
        public override Keys Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => (Keys)reader.GetInt32();

        public override void Write(Utf8JsonWriter writer, Keys value, JsonSerializerOptions options)
            => writer.WriteNumberValue((int)value);
    }
    #endregion
}