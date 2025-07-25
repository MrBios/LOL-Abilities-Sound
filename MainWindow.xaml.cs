using Gma.System.MouseKeyHook;
using LOL_abilities_sound.Pages;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;
using Keys = System.Windows.Forms.Keys;

namespace LOL_abilities_sound
{
    public partial class MainWindow : Window
    {
        #region Constants
        private const string INITIALIZATION_ERROR_TITLE = "Initialization Error";
        private const string VOICEPROCESSOR_ERROR_MESSAGE = "Error initializing VoiceProcessor: {0}";
        #endregion

        #region Properties
        public VoiceProcessor voice { get; private set; }
        public bool isRunning { get; private set; } = false;
        public bool isInGame { get; private set; } = false;
        public int nowPlayingChampionId { get; private set; } = 0;
        
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly LcuApi _lcuApi = new LcuApi();
        private readonly IKeyboardMouseEvents _globalHook = Hook.GlobalEvents();
        private readonly Dictionary<(int championId, int abilityId), DateTime> _abilityCooldowns = new Dictionary<(int championId, int abilityId), DateTime>();
        #endregion

        #region Properties (Public Access)
        public LcuApi lcuApi => _lcuApi;
        #endregion

        #region Constructor
        public MainWindow()
        {
            try
            {
                InitializeComponent();
                InitializeApplicationComponents();
                System.Diagnostics.Debug.WriteLine("MainWindow initialized successfully");
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error initializing application: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(errorMessage);
                MessageBox.Show(errorMessage, INITIALIZATION_ERROR_TITLE, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Private Methods
        private void InitializeApplicationComponents()
        {
            SetDefaultImage();
            InitializeVoiceProcessor();
            StartGameDetection();
            SetupGlobalKeyboardHook();
            InitializeMainPage();
        }

        private void SetDefaultImage()
        {
            try
            {
                if (image != null)
                {
                    image.Source = Config.notInGameSplashSource();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting default image: {ex.Message}");
            }
        }

        private void InitializeVoiceProcessor()
        {
            try
            {
                int microphoneIndex = Config.getMicrophoneIndex();
                voice = new VoiceProcessor(microphoneIndex);
                voice.Start();
                System.Diagnostics.Debug.WriteLine("VoiceProcessor started successfully");
            }
            catch (Exception ex)
            {
                string errorMessage = string.Format(VOICEPROCESSOR_ERROR_MESSAGE, ex.Message);
                System.Diagnostics.Debug.WriteLine(errorMessage);
                
                try
                {
                    voice = new VoiceProcessor(0);
                    voice.Start();
                    System.Diagnostics.Debug.WriteLine("VoiceProcessor started with default settings");
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to initialize VoiceProcessor with fallback: {fallbackEx.Message}");
                }
            }
        }

        private void StartGameDetection()
        {
            try
            {
                _ = Task.Run(() => FindGameAsync(_cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting game detection: {ex.Message}");
            }
        }

        private void SetupGlobalKeyboardHook()
        {
            try
            {
                _globalHook.KeyDown += HandleGlobalKeyDown;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting up global keyboard hook: {ex.Message}");
            }
        }

        private void InitializeMainPage()
        {
            try
            {
                if (frame != null)
                {
                    frame.Content = new mainPage(this);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing main page: {ex.Message}");
            }
        }

        private async Task FindGameAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessGameStatusAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in game detection loop: {ex.Message}");
                }

                try
                {
                    await Task.Delay(Config.findGameDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ProcessGameStatusAsync()
        {
            try
            {
                if (_lcuApi.Connect())
                {
                    await HandleConnectedStateAsync();
                }
                else
                {
                    HandleDisconnectedState();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing game status: {ex.Message}");
                HandleDisconnectedState();
            }
        }

        private async Task HandleConnectedStateAsync()
        {
            var (inGame, championId, playerInfo) = await _lcuApi.GetCurrentGameStatusAsync();
            
            if (inGame)
            {
                await HandleInGameStateAsync(championId, playerInfo);
            }
            else
            {
                HandleNotInGameState();
            }
        }

        private async Task HandleInGameStateAsync(int championId, (string Name, string Square, string Splash) playerInfo)
        {
            isInGame = true;
            nowPlayingChampionId = championId;
            
            try
            {
                var championImage = await LcuApi.LoadChampionImageFromWadAsync(playerInfo.Splash, true);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (image != null)
                    {
                        image.Source = championImage;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading champion image: {ex.Message}");
            }
        }

        private void HandleNotInGameState()
        {
            isInGame = false;
            nowPlayingChampionId = 0;
            
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (image != null)
                {
                    image.Source = Config.notInGameSplashSource();
                }
            });
        }

        private void HandleDisconnectedState()
        {
            isInGame = false;
            nowPlayingChampionId = 0;
            
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (image != null)
                {
                    image.Source = Config.notInGameSplashSource();
                }
            });
        }

        private async Task HandleAbilityActivationAsync(int championId, int abilityId)
        {
            try
            {
                if (IsAbilityOnCooldown(championId, abilityId))
                {
                    return;
                }

                var soundPath = Config.getAbilitySound(championId, abilityId);
                if (string.IsNullOrEmpty(soundPath))
                {
                    return;
                }

                PlayAbilitySound(soundPath);
                SetAbilityCooldown(championId, abilityId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling ability activation: {ex.Message}");
            }
        }

        private bool IsAbilityOnCooldown(int championId, int abilityId)
        {
            var cooldownKey = (championId, abilityId);
            
            if (_abilityCooldowns.ContainsKey(cooldownKey))
            {
                if (DateTime.Now < _abilityCooldowns[cooldownKey])
                {
                    return true;
                }
                else
                {
                    _abilityCooldowns.Remove(cooldownKey);
                }
            }
            
            return false;
        }

        private void PlayAbilitySound(string soundPath)
        {
            try
            {
                voice?.PlaySound(soundPath, Config.getSoundVolume());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing ability sound: {ex.Message}");
            }
        }

        private void SetAbilityCooldown(int championId, int abilityId)
        {
            try
            {
                int abilityDelaySeconds = Config.getAbilityDelaySeconds(championId, abilityId);
                if (abilityDelaySeconds > 0)
                {
                    _abilityCooldowns[(championId, abilityId)] = DateTime.Now.AddSeconds(abilityDelaySeconds);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting ability cooldown: {ex.Message}");
            }
        }

        private void ToggleApplicationRunningState(bool isRunning)
        {
            try
            {
                this.isRunning = isRunning;
                System.Diagnostics.Debug.WriteLine($"Application running state changed to: {isRunning}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error toggling application state: {ex.Message}");
            }
        }

        private void CleanupResources()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                voice?.Stop();
                voice?.Dispose();
                _globalHook?.Dispose();
                System.Diagnostics.Debug.WriteLine("Resources cleaned up successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }
        #endregion

        #region Event Handlers
        private void HandleGlobalKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (!ShouldProcessKeyPress())
                {
                    return;
                }

                var abilityId = GetAbilityIdFromKey(e.KeyCode);
                if (abilityId.HasValue)
                {
                    _ = Task.Run(() => HandleAbilityActivationAsync(nowPlayingChampionId, abilityId.Value));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling global key down: {ex.Message}");
            }
        }

        private bool ShouldProcessKeyPress()
        {
            return isInGame && isRunning && nowPlayingChampionId != 0;
        }

        private int? GetAbilityIdFromKey(Keys keyCode)
        {
            return keyCode switch
            {
                Keys.Q => 1,
                Keys.W => 2,
                Keys.E => 3,
                Keys.R => 4,
                _ => null
            };
        }

        private void startSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (startSwitch != null)
                {
                    ToggleApplicationRunningState(startSwitch.IsOn);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling start switch toggle: {ex.Message}");
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                CleanupResources();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during window closing: {ex.Message}");
            }
        }
        #endregion
    }
}