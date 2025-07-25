using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using Label = System.Windows.Controls.Label;
using MessageBox = System.Windows.Forms.MessageBox;
using TextBox = System.Windows.Controls.TextBox;

namespace LOL_abilities_sound.Pages
{
    /// <summary>
    /// Логика взаимодействия для setAbilitiesSoundsPage.xaml
    /// </summary>
    public partial class setAbilitiesSoundsPage : Page
    {
        #region Constants
        private const int BATCH_SIZE = 5;
        private const int MAX_ABILITY_COUNT = 4;
        private const int SKIP_FIRST_CHAMPION = 1;
        private const string NO_SOUND_TEXT = "нет звука";
        private const string AUDIO_FILE_FILTER = "Audio Files|*.mp3;*.wav;*.ogg";
        private const string FILE_DIALOG_TITLE = "Выберите звук для умения";
        private const string DELAY_ERROR_MESSAGE = "Введите корректное значение задержки в секундах.";
        private const string DELAY_SUCCESS_MESSAGE = "Задержка успешно установлена на {0} секунд.";
        #endregion

        #region Fields
        private readonly LcuApi _lcuApi;
        private Dictionary<string, (string Name, string Square, string Splash)> _championsInfo;
        private CancellationTokenSource _cancellationTokenSource;
        
        // Cache for ability icons
        private static readonly Dictionary<int, BitmapImage> _abilityIconsCache = new Dictionary<int, BitmapImage>();
        private static BitmapImage _deleteIconCache;
        private static BitmapImage _saveIconCache;
        #endregion

        #region Constructor
        public setAbilitiesSoundsPage(LcuApi api)
        {
            if (api == null)
            {
                throw new ArgumentNullException(nameof(api), "LCU API cannot be null");
            }

            InitializeComponent();
            _lcuApi = api;
            
            InitializePageComponents();
        }
        #endregion

        #region Private Methods
        private void InitializePageComponents()
        {
            try
            {
                SetBackIcon();
                PreloadIcons();
                _ = InitializePageAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing page components: {ex.Message}");
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

        private void PreloadIcons()
        {
            Task.Run(() =>
            {
                try
                {
                    PreloadAbilityIcons();
                    PreloadActionIcons();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error preloading icons: {ex.Message}");
                }
            });
        }

        private static void PreloadAbilityIcons()
        {
            for (int i = 1; i <= MAX_ABILITY_COUNT; i++)
            {
                if (!_abilityIconsCache.ContainsKey(i))
                {
                    try
                    {
                        _abilityIconsCache[i] = Config.getAbilitySource(i);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error preloading ability icon {i}: {ex.Message}");
                        _abilityIconsCache[i] = new BitmapImage();
                    }
                }
            }
        }

        private static void PreloadActionIcons()
        {
            try
            {
                if (_deleteIconCache == null)
                    _deleteIconCache = Config.deleteIcon();
                
                if (_saveIconCache == null)
                    _saveIconCache = Config.saveIcon();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error preloading action icons: {ex.Message}");
            }
        }

        private async Task InitializePageAsync()
        {
            try
            {
                await LoadChampionsData();
                await LoadContentAsync();
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка при инициализации страницы: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(errorMessage);
                MessageBox.Show(errorMessage, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadChampionsData()
        {
            if (_lcuApi == null)
            {
                throw new InvalidOperationException("LCU API is not available");
            }

            _championsInfo = await _lcuApi.GetAllChampionsAsync();
            
            if (_championsInfo == null)
            {
                throw new InvalidOperationException("Failed to load champions data");
            }
        }

        private async Task LoadContentAsync()
        {
            if (_championsInfo == null)
            {
                System.Diagnostics.Debug.WriteLine("Champions data is not available");
                return;
            }

            CancelPreviousOperation();
            ClearContent();

            var champions = _championsInfo.ToList();
            await ProcessChampionsAsync(champions);
        }

        private void CancelPreviousOperation()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        private void ClearContent()
        {
            try
            {
                content?.Children.Clear();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing content: {ex.Message}");
            }
        }

        private async Task ProcessChampionsAsync(List<KeyValuePair<string, (string Name, string Square, string Splash)>> champions)
        {
            var cancellationToken = _cancellationTokenSource.Token;
            
            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessChampionsBatch(champions, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine("Champion processing was cancelled");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing champions: {ex.Message}");
                }
            }, cancellationToken);
        }

        private async Task ProcessChampionsBatch(
            List<KeyValuePair<string, (string Name, string Square, string Splash)>> champions,
            CancellationToken cancellationToken)
        {
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount);
            var completedBorders = new List<Border>();
            var lockObject = new object();
            var tasks = new List<Task>();

            for (int i = SKIP_FIRST_CHAMPION; i < champions.Count && !cancellationToken.IsCancellationRequested; i++)
            {
                var championPair = champions[i];
                var task = ProcessSingleChampionTask(championPair, semaphore, completedBorders, lockObject, cancellationToken);
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
            await AddRemainingBordersToUI(completedBorders, cancellationToken);
        }

        private Task ProcessSingleChampionTask(
            KeyValuePair<string, (string Name, string Square, string Splash)> championPair,
            SemaphoreSlim semaphore,
            List<Border> completedBorders,
            object lockObject,
            CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    var border = await ProcessSingleChampionAsync(championPair.Key, championPair.Value, cancellationToken);
                    if (border != null && !cancellationToken.IsCancellationRequested)
                    {
                        HandleCompletedBorder(border, completedBorders, lockObject, cancellationToken);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);
        }

        private void HandleCompletedBorder(Border border, List<Border> completedBorders, object lockObject, CancellationToken cancellationToken)
        {
            lock (lockObject)
            {
                completedBorders.Add(border);
            }

            if (completedBorders.Count >= BATCH_SIZE)
            {
                List<Border> bordersToAdd;
                lock (lockObject)
                {
                    bordersToAdd = new List<Border>(completedBorders);
                    completedBorders.Clear();
                }

                AddBordersToUIAsync(bordersToAdd, cancellationToken);
            }
        }

        private async Task AddBordersToUIAsync(List<Border> borders, CancellationToken cancellationToken)
        {
            await Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (!cancellationToken.IsCancellationRequested && content != null)
                    {
                        foreach (var border in borders)
                        {
                            content.Children.Add(border);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error adding borders to UI: {ex.Message}");
                }
            }), DispatcherPriority.Background);
        }

        private async Task AddRemainingBordersToUI(List<Border> completedBorders, CancellationToken cancellationToken)
        {
            if (completedBorders.Count > 0 && !cancellationToken.IsCancellationRequested)
            {
                await AddBordersToUIAsync(completedBorders, cancellationToken);
            }
        }

        private async Task<Border> ProcessSingleChampionAsync(string championId, (string Name, string Square, string Splash) championInfo, CancellationToken cancellationToken)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                    return null;

                var championImage = await LoadChampionImageAsync(championInfo.Square);
                
                if (cancellationToken.IsCancellationRequested)
                    return null;

                return await CreateChampionBorderAsync(championId, championInfo, championImage, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing champion {championId}: {ex.Message}");
                return null;
            }
        }

        private async Task<BitmapImage> LoadChampionImageAsync(string squarePath)
        {
            try
            {
                return await LcuApi.LoadChampionImageFromWadAsync(squarePath, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading champion image: {ex.Message}");
                return Config.noIconSource();
            }
        }

        private async Task<Border> CreateChampionBorderAsync(string championId, (string Name, string Square, string Splash) championInfo, BitmapImage championImage, CancellationToken cancellationToken)
        {
            Border border = null;
            var resetEvent = new ManualResetEventSlim(false);
            
            await Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        border = CreateAndSetupChampionBorder(championId, championInfo, championImage);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating champion border for {championId}: {ex.Message}");
                }
                finally
                {
                    resetEvent.Set();
                }
            }), DispatcherPriority.Background);
            
            resetEvent.Wait(cancellationToken);
            return border;
        }

        private Border CreateAndSetupChampionBorder(string championId, (string Name, string Square, string Splash) championInfo, BitmapImage championImage)
        {
            try
            {
                var border = CreateBorderFromTemplate(championId);
                SetupChampionHeader(border, championInfo, championImage);
                SetupChampionAbilities(border, championId);
                return border;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating champion border for {championId}: {ex.Message}");
                return null;
            }
        }

        private Border CreateBorderFromTemplate(string championId)
        {
            if (Resources["championBorder"] is not DataTemplate template)
            {
                throw new InvalidOperationException("Champion border template not found");
            }

            var border = (Border)template.LoadContent();
            border.Tag = championId;
            return border;
        }

        private void SetupChampionHeader(Border border, (string Name, string Square, string Splash) championInfo, BitmapImage championImage)
        {
            var headerPanel = GetHeaderPanel(border);
            SetChampionImage(headerPanel, championImage);
            SetChampionLabel(headerPanel, championInfo.Name);
            SetupHeaderClickHandler(headerPanel);
        }

        private StackPanel GetHeaderPanel(Border border)
        {
            return (StackPanel)((StackPanel)border.Child).Children[0];
        }

        private void SetChampionImage(StackPanel headerPanel, BitmapImage championImage)
        {
            var championImageControl = (Image)headerPanel.Children[0];
            championImageControl.Source = championImage ?? Config.noIconSource();
        }

        private void SetChampionLabel(StackPanel headerPanel, string championName)
        {
            var championLabel = (Label)headerPanel.Children[1];
            championLabel.Content = championName;
        }

        private void SetupHeaderClickHandler(StackPanel headerPanel)
        {
            headerPanel.MouseLeftButtonDown += (s, e) =>
            {
                try
                {
                    ToggleChampionVisibility(headerPanel);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error toggling champion visibility: {ex.Message}");
                }
            };
        }

        private void ToggleChampionVisibility(StackPanel headerPanel)
        {
            var abilitiesPanel = ((StackPanel)headerPanel.Parent).Children[1] as StackPanel;
            if (abilitiesPanel != null)
            {
                abilitiesPanel.Visibility = abilitiesPanel.Visibility == Visibility.Visible 
                    ? Visibility.Collapsed 
                    : Visibility.Visible;
            }
        }

        private void SetupChampionAbilities(Border border, string championId)
        {
            var abilitiesPanel = GetAbilitiesPanel(border);
            
            for (int i = 0; i < MAX_ABILITY_COUNT; i++)
            {
                SetupSingleAbility(abilitiesPanel, i, championId);
            }
        }

        private StackPanel GetAbilitiesPanel(Border border)
        {
            return (StackPanel)((StackPanel)border.Child).Children[1];
        }

        private void SetupSingleAbility(StackPanel abilitiesPanel, int abilityIndex, string championId)
        {
            try
            {
                var abilityPanel = abilitiesPanel.Children[abilityIndex] as StackPanel;
                if (abilityPanel == null) return;

                int currentAbilityIndex = abilityIndex + 1;
                int currentChampionId = int.Parse(championId);

                abilityPanel.Tag = currentAbilityIndex;
                
                SetupAbilityIcon(abilityPanel, currentAbilityIndex);
                SetupAbilitySoundControls(abilityPanel, currentChampionId, currentAbilityIndex);
                SetupAbilityDelayControls(abilityPanel, currentChampionId, currentAbilityIndex);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting up ability {abilityIndex + 1}: {ex.Message}");
            }
        }

        private void SetupAbilityIcon(StackPanel abilityPanel, int abilityIndex)
        {
            try
            {
                var abilityImage = (Image)abilityPanel.Children[0];
                
                if (_abilityIconsCache.TryGetValue(abilityIndex, out var abilityIcon))
                {
                    abilityImage.Source = abilityIcon;
                }
                else
                {
                    abilityImage.Source = new BitmapImage();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting up ability icon for ability {abilityIndex}: {ex.Message}");
            }
        }

        private void SetupAbilitySoundControls(StackPanel abilityPanel, int championId, int abilityIndex)
        {
            try
            {
                var soundControlsPanel = (StackPanel)((StackPanel)abilityPanel.Children[1]).Children[0];
                var soundTextBox = (TextBox)soundControlsPanel.Children[1];
                var deleteButton = (Image)soundControlsPanel.Children[2];

                // Setup sound text box
                string abilitySound = Config.getAbilitySound(championId, abilityIndex);
                soundTextBox.Text = string.IsNullOrEmpty(abilitySound) ? NO_SOUND_TEXT : abilitySound;

                // Setup file selection
                soundTextBox.MouseLeftButtonDown += (s, e) => HandleSoundFileSelection(s as TextBox, championId, abilityIndex);

                // Setup delete button
                deleteButton.Source = _deleteIconCache ?? new BitmapImage();
                deleteButton.MouseLeftButtonDown += (s, e) => HandleSoundDelete(s as Image, championId, abilityIndex);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting up sound controls for ability {abilityIndex}: {ex.Message}");
            }
        }

        private void SetupAbilityDelayControls(StackPanel abilityPanel, int championId, int abilityIndex)
        {
            try
            {
                var delayControlsPanel = (StackPanel)((StackPanel)abilityPanel.Children[1]).Children[1];
                var delayTextBox = (TextBox)delayControlsPanel.Children[1];
                var saveButton = (Image)delayControlsPanel.Children[2];

                // Setup delay text box
                delayTextBox.Text = Config.getAbilityDelaySeconds(championId, abilityIndex).ToString();

                // Setup save button
                saveButton.Source = _saveIconCache ?? new BitmapImage();
                saveButton.MouseLeftButtonDown += (s, e) => HandleDelaySave(s as Image, championId, abilityIndex);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting up delay controls for ability {abilityIndex}: {ex.Message}");
            }
        }

        private void HandleSoundFileSelection(TextBox textBox, int championId, int abilityIndex)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = AUDIO_FILE_FILTER,
                    Title = FILE_DIALOG_TITLE
                };

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    var fileName = openFileDialog.FileName.Split('/', '\\').Last();
                    textBox.Text = fileName;
                    Config.setAbilitySound(championId, abilityIndex, openFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling sound file selection: {ex.Message}");
            }
        }

        private void HandleSoundDelete(Image deleteButton, int championId, int abilityIndex)
        {
            try
            {
                var soundTextBox = (TextBox)((StackPanel)deleteButton.Parent).Children[1];
                soundTextBox.Text = NO_SOUND_TEXT;
                Config.removeAbilitySound(championId, abilityIndex);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling sound delete: {ex.Message}");
            }
        }

        private void HandleDelaySave(Image saveButton, int championId, int abilityIndex)
        {
            try
            {
                var delayTextBox = (TextBox)((StackPanel)saveButton.Parent).Children[1];
                
                if (!int.TryParse(delayTextBox.Text, out int delaySeconds))
                {
                    MessageBox.Show(DELAY_ERROR_MESSAGE, "Ошибка");
                    return;
                }

                Config.setAbilityDelaySeconds(championId, abilityIndex, delaySeconds);
                Keyboard.ClearFocus();
                
                string successMessage = string.Format(DELAY_SUCCESS_MESSAGE, delaySeconds);
                MessageBox.Show(successMessage, "Успех");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling delay save: {ex.Message}");
            }
        }

        private void FilterChampions(string searchText)
        {
            try
            {
                if (_championsInfo == null || content == null)
                    return;

                if (string.IsNullOrEmpty(searchText))
                {
                    ShowAllChampions();
                    return;
                }

                var foundChampionIds = _championsInfo
                    .Where(champion => champion.Value.Name.ToLower().Contains(searchText.ToLower()))
                    .Select(champion => champion.Key)
                    .ToArray();

                FilterChampionsByIds(foundChampionIds);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error filtering champions: {ex.Message}");
            }
        }

        private void ShowAllChampions()
        {
            foreach (var border in content.Children.OfType<Border>())
            {
                border.Visibility = Visibility.Visible;
            }
        }

        private void FilterChampionsByIds(string[] foundChampionIds)
        {
            foreach (var border in content.Children.OfType<Border>())
            {
                if (border.Tag is string tag)
                {
                    border.Visibility = foundChampionIds.Contains(tag) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }
        #endregion

        #region Event Handlers
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (sender is TextBox searchBox)
                {
                    FilterChampions(searchBox.Text);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling search text change: {ex.Message}");
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

        #region Cleanup
        ~setAbilitiesSoundsPage()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }
        #endregion
    }
}
