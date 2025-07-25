using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace LOL_abilities_sound.Pages
{
    /// <summary>
    /// Логика взаимодействия для mainPage.xaml
    /// </summary>
    public partial class mainPage : Page
    {
        #region Constants
        private const string NAVIGATION_ERROR_TITLE = "Navigation Error";
        private const string LCU_API_UNAVAILABLE_MESSAGE = "LCU API is not available";
        private const string MAINWINDOW_UNAVAILABLE_MESSAGE = "MainWindow instance is not available";
        #endregion

        #region Fields
        private readonly MainWindow _mainWindowInstance;
        #endregion

        #region Constructor
        public mainPage(MainWindow mainWindowInstance)
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
                SetLogoImage();
                System.Diagnostics.Debug.WriteLine("Main page initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing main page: {ex.Message}");
            }
        }

        private void SetLogoImage()
        {
            try
            {
                if (logo != null)
                {
                    var logoSource = Config.logo();
                    if (logoSource != null)
                    {
                        logo.Source = logoSource;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting logo image: {ex.Message}");
            }
        }

        private void NavigateToAbilitiesPage()
        {
            try
            {
                if (_mainWindowInstance?.lcuApi == null)
                {
                    System.Diagnostics.Debug.WriteLine(LCU_API_UNAVAILABLE_MESSAGE);
                    return;
                }

                var setAbilitiesPage = new setAbilitiesSoundsPage(_mainWindowInstance.lcuApi);
                NavigationService?.Navigate(setAbilitiesPage);
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error navigating to abilities page: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(errorMessage);
                ShowErrorMessage(errorMessage);
            }
        }

        private void NavigateToSettingsPage()
        {
            try
            {
                if (_mainWindowInstance == null)
                {
                    System.Diagnostics.Debug.WriteLine(MAINWINDOW_UNAVAILABLE_MESSAGE);
                    return;
                }

                var settingsPageInstance = new settingsPage(_mainWindowInstance);
                NavigationService?.Navigate(settingsPageInstance);
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error navigating to settings page: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(errorMessage);
                ShowErrorMessage(errorMessage);
            }
        }

        private void ShowErrorMessage(string message)
        {
            try
            {
                MessageBox.Show(message, NAVIGATION_ERROR_TITLE, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing error message: {ex.Message}");
            }
        }
        #endregion

        #region Event Handlers
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigateToAbilitiesPage();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in abilities button click handler: {ex.Message}");
            }
        }

        private void Settings_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigateToSettingsPage();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in settings button click handler: {ex.Message}");
            }
        }
        #endregion
    }
}
