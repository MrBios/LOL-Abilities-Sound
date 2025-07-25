using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace LOL_abilities_sound.Pages
{
    /// <summary>
    /// Логика взаимодействия для settingsPage.xaml
    /// </summary>
    public partial class settingsPage : Page
    {
        #region Constants
        private const string PERCENTAGE_FORMAT = "{0}%";
        private const string ERROR_TITLE = "Ошибка";
        private const string MICROPHONE_ERROR_MESSAGE = "Ошибка при смене микрофона: {0}";
        private const string FALLBACK_MICROPHONE_NAME = "Error: Cannot load microphones";
        #endregion

        #region Fields
        private readonly MainWindow _mainWindowInstance;
        #endregion

        #region Constructor
        public settingsPage(MainWindow mainWindowInstance)
        {
            if (mainWindowInstance == null)
            {
                throw new ArgumentNullException(nameof(mainWindowInstance), "MainWindow instance cannot be null");
            }

            InitializeComponent();
            _mainWindowInstance = mainWindowInstance;
            
            InitializePageComponents();
        }
        #endregion

        #region Private Methods
        private void InitializePageComponents()
        {
            try
            {
                SetBackIcon();
                InitializeSettings();
                System.Diagnostics.Debug.WriteLine("Settings page initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing settings page: {ex.Message}");
            }
        }

        private void SetBackIcon()
        {
            try
            {
                if (back != null)
                {
                    back.Source = Config.backIcon();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting back icon: {ex.Message}");
            }
        }

        private void InitializeSettings()
        {
            try
            {
                InitializeVolumeControls();
                UpdateVolumeDisplayText();
                InitializeMicrophoneList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing settings: {ex.Message}");
            }
        }

        private void InitializeVolumeControls()
        {
            try
            {
                if (outputVolumeSlider != null)
                {
                    outputVolumeSlider.Value = Config.getSoundVolume();
                }
                
                if (selfVolumeSlider != null)
                {
                    selfVolumeSlider.Value = Config.getSelfSoundVolume();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing volume controls: {ex.Message}");
            }
        }

        private void InitializeMicrophoneList()
        {
            try
            {
                var microphones = VoiceProcessor.GetAvailableMicrophones();
                
                if (microphoneComboBox != null)
                {
                    microphoneComboBox.ItemsSource = null;
                    microphoneComboBox.ItemsSource = microphones;
                    
                    System.Diagnostics.Debug.WriteLine($"Found {microphones.Count} microphones");
                    foreach (var mic in microphones)
                    {
                        System.Diagnostics.Debug.WriteLine($"Microphone {mic.Index}: {mic.Name}");
                    }
                    
                    SelectCurrentMicrophone(microphones);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing microphones: {ex.Message}");
                SetFallbackMicrophoneList();
            }
        }

        private void SelectCurrentMicrophone(List<MicrophoneItem> microphones)
        {
            if (microphones == null || !microphones.Any() || microphoneComboBox == null)
            {
                return;
            }

            try
            {
                int selectedMicrophoneIndex = Config.getMicrophoneIndex();
                var selectedMicrophone = microphones.FirstOrDefault(m => m.Index == selectedMicrophoneIndex);
                
                if (selectedMicrophone != null)
                {
                    microphoneComboBox.SelectedItem = selectedMicrophone;
                    System.Diagnostics.Debug.WriteLine($"Selected microphone: {selectedMicrophone.Name}");
                }
                else
                {
                    microphoneComboBox.SelectedIndex = 0;
                    System.Diagnostics.Debug.WriteLine($"Selected first microphone: {microphones[0].Name}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting fallback microphone list: {ex.Message}");
            }
        }

        private void SetFallbackMicrophoneList()
        {
            try
            {
                if (microphoneComboBox != null)
                {
                    var fallbackMicrophoneList = new List<MicrophoneItem>
                    {
                        new MicrophoneItem { Index = 0, Name = FALLBACK_MICROPHONE_NAME }
                    };
                    
                    microphoneComboBox.ItemsSource = fallbackMicrophoneList;
                    microphoneComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting fallback microphone list: {ex.Message}");
            }
        }

        private void UpdateVolumeDisplayText()
        {
            try
            {
                UpdateOutputVolumeText();
                UpdateSelfVolumeText();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating volume text: {ex.Message}");
            }
        }

        private void UpdateOutputVolumeText()
        {
            try
            {
                if (outputVolumeText != null && outputVolumeSlider != null)
                {
                    int volumePercentage = (int)(outputVolumeSlider.Value * 100);
                    outputVolumeText.Text = string.Format(PERCENTAGE_FORMAT, volumePercentage);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating output volume text: {ex.Message}");
            }
        }

        private void UpdateSelfVolumeText()
        {
            try
            {
                if (selfVolumeText != null && selfVolumeSlider != null)
                {
                    int volumePercentage = (int)(selfVolumeSlider.Value * 100);
                    selfVolumeText.Text = string.Format(PERCENTAGE_FORMAT, volumePercentage);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating self volume text: {ex.Message}");
            }
        }

        private void HandleMicrophoneChange(MicrophoneItem selectedMicrophone)
        {
            if (selectedMicrophone == null || _mainWindowInstance?.voice == null)
            {
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"Changing to microphone: {selectedMicrophone.Name} (Index: {selectedMicrophone.Index})");
                
                Config.setMicrophoneIndex(selectedMicrophone.Index);
                _mainWindowInstance.voice.ChangeMicrophone(selectedMicrophone.Index);
                
                System.Diagnostics.Debug.WriteLine($"Successfully switched to microphone: {selectedMicrophone.Name}");
            }
            catch (Exception ex)
            {
                string errorMessage = string.Format(MICROPHONE_ERROR_MESSAGE, ex.Message);
                System.Diagnostics.Debug.WriteLine(errorMessage);
                
                System.Windows.Forms.MessageBox.Show(
                    errorMessage, 
                    ERROR_TITLE,
                    System.Windows.Forms.MessageBoxButtons.OK, 
                    System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
        #endregion

        #region Event Handlers
        private void outputVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (outputVolumeText != null && e?.NewValue != null)
                {
                    int volumePercentage = (int)(e.NewValue * 100);
                    outputVolumeText.Text = string.Format(PERCENTAGE_FORMAT, volumePercentage);
                    Config.setSoundVolume((float)e.NewValue);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling output volume change: {ex.Message}");
            }
        }

        private void selfVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (selfVolumeText != null && e?.NewValue != null)
                {
                    int volumePercentage = (int)(e.NewValue * 100);
                    selfVolumeText.Text = string.Format(PERCENTAGE_FORMAT, volumePercentage);
                    Config.setSelfSoundVolume((float)e.NewValue);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling self volume change: {ex.Message}");
            }
        }

        private void microphoneComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (microphoneComboBox?.SelectedItem is MicrophoneItem selectedMicrophone)
                {
                    HandleMicrophoneChange(selectedMicrophone);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling microphone selection change: {ex.Message}");
            }
        }

        private void back_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                NavigationService?.GoBack();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error navigating back: {ex.Message}");
            }
        }
        #endregion
    }
}