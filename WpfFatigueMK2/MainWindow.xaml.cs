using Microsoft.Data.SqlClient;
using NLog;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;


namespace WpfFatigueMK2
{
    public partial class MainWindow : Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private SerialPort _serialPort;
        private Queue<int> _stepCounts = new();
        private DateTime _lastUpdate = DateTime.Now;
        private const int MaxSteps = 200;
        private bool _initialized = false;
        private int _initialAverageSteps = 0;
        private DatabaseHelper _dbHelper;
        private bool _matchStarted = false;


        private List<PlayerSlot> _playerSlots = new();
        private Dictionary<int, string> _assignedPlayers = new();
        private int _trackedPlayerSlot = 1; // Arduino tracks slot 1
        private ObservableCollection<string> _substitutePlayers = new();


        public MainWindow()
        {
            InitializeComponent();
            Logger.Info("Application started.");
            string connStr = "Server=tcp:myplayerserver.database.windows.net,1433;Initial Catalog=PlayerTracker;Persist Security Info=False;User ID=AdamGleeson;Password=Tyrone19;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
            _dbHelper = new DatabaseHelper(connStr);
            InitializeSerialPort();
            LoadWeather();

            _ = LoadSubstitutesListAsync();
            InitializeJerseyGrid();

            SubstitutesListBox.MouseDoubleClick += SubstitutesListBox_MouseDoubleClick;
        }

        private async Task LoadSubstitutesListAsync()
        {
            try
            {
                var players = await _dbHelper.GetPlayersByTeamAsync("Claremorris");
                _substitutePlayers = new ObservableCollection<string>(players);
                SubstitutesListBox.ItemsSource = _substitutePlayers;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load substitutes list.");
                MessageBox.Show("Failed to load players: " + ex.Message);
            }
        }


        private void InitializeJerseyGrid()
        {
            for (int i = 1; i <= 15; i++)
            {
                var stack = new StackPanel { Orientation = Orientation.Vertical };

                // Jersey number
                stack.Children.Add(new TextBlock
                {
                    Text = i.ToString(),
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 5)
                });

                // Jersey image
                stack.Children.Add(new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/Images/jersey.jpg")),
                    Width = 80,
                    Height = 80
                });

                var btn = new Button
                {
                    Width = 100,
                    Height = 120,
                    Margin = new Thickness(5),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Tag = i,
                    Content = stack
                };

                PlayerGrid.Children.Add(btn);

                _playerSlots.Add(new PlayerSlot
                {
                    SlotNumber = i,
                    PlayerName = null,
                    JerseyButton = btn,
                    StackPanel = stack // ✅ Store this for later access
                });
                btn.MouseDoubleClick += JerseyButton_MouseDoubleClick;
            }
        }

        private void JerseyButton_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button button && button.Tag is int slotNumber)
            {
                var playerSlot = _playerSlots.FirstOrDefault(s => s.SlotNumber == slotNumber);
                if (playerSlot != null && !string.IsNullOrEmpty(playerSlot.PlayerName))
                {
                    var detailsWindow = new PlayerDetailsWindow(playerSlot.PlayerName, slotNumber, _dbHelper);
                    detailsWindow.ShowDialog();
                }
                else
                {
                    MessageBox.Show("No player assigned to this slot.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }




        private void StartMatchButton_Click(object sender, RoutedEventArgs e)
        {
            _matchStarted = true;
            StartMatchButton.IsEnabled = false; // optional: disable the button after clicking
            MessageBox.Show("Match started! Fatigue tracking is now active.");
        }




        private void SubstitutesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SubstitutesListBox.SelectedItem is string playerName)
            {
                var emptySlot = _playerSlots.FirstOrDefault(s => string.IsNullOrEmpty(s.PlayerName));
                if (emptySlot != null)
                {
                    emptySlot.PlayerName = playerName;
                    _assignedPlayers[emptySlot.SlotNumber] = playerName;

                    // Add name to jersey UI
                    emptySlot.StackPanel.Children.Add(new TextBlock
                    {
                        Text = playerName,
                        FontSize = 10,
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Center
                    });

                    // ✅ Remove from the ObservableCollection, NOT from Items directly
                    _substitutePlayers.Remove(playerName);

                    MessageBox.Show($"{playerName} assigned to position {emptySlot.SlotNumber}");
                }
            }
        }



        private async void LoadWeather()
        {
            var weather = await WeatherService.GetForecastAsync("Dublin,IE");

            if (weather != null && weather.list != null && weather.list.Count > 0)
            {
                var first = weather.list[0];
                string condition = first.weather[0].main;
                string description = first.weather[0].description;
                double temp = first.main.temp;

                WeatherConditionTextBox.Text = $"{condition} ({description}), {temp}°C";
            }
            else
            {
                WeatherConditionTextBox.Text = "N/A";
            }
        }

        private void InitializeSerialPort()
        {
            try
            {
                _serialPort = new SerialPort("COM5", 9600)
                {
                    DtrEnable = true,
                    RtsEnable = true,
                    ReadTimeout = 2000,
                    WriteTimeout = 500
                };

                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();
                System.Threading.Thread.Sleep(2000);
                _serialPort.DiscardInBuffer();

                MessageBox.Show("Connected to Arduino successfully!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing serial port: {ex.Message}");
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!_matchStarted) return; // 🔒 Prevent tracking before match starts

            try
            {
                string data = _serialPort.ReadLine();
                if (data.StartsWith("Steps in last 5 seconds:"))
                {
                    string stepCountStr = data.Replace("Steps in last 5 seconds:", "").Trim();
                    if (int.TryParse(stepCountStr, out int stepCount))
                    {
                        UpdateFatigueLevel(stepCount);
                    }
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading serial data: {ex.Message}");
            }
        }


        private void UpdateFatigueLevel(int stepCount)
        {
            if (!_initialized)
            {
                _stepCounts.Enqueue(stepCount);
                _lastUpdate = DateTime.Now;

                if (_stepCounts.Count > 3)
                    _stepCounts.Dequeue();

                if (_stepCounts.Count == 3)
                {
                    _initialized = true;
                    _initialAverageSteps = CalculateAverageSteps();

                    Dispatcher.Invoke(() =>
                    {
                        FatigueProgressBar.Value = 100;
                        FatigueLevelText.Text = "Fatigue Level: Normal (Player is fine)";
                    });
                }
            }
            else
            {
                double percentage = ((double)stepCount / _initialAverageSteps) * 100;
                percentage = Math.Min(percentage, 100);

                Dispatcher.Invoke(() =>
                {
                    FatigueProgressBar.Value = percentage;
                    UpdateFatigueLevelText(percentage);
                });
            }
        }

        private int CalculateAverageSteps()
        {
            return _stepCounts.Count > 0 ? _stepCounts.Sum() / _stepCounts.Count : 0;
        }

        private void UpdateFatigueLevelText(double percentage)
        {
            if (percentage <= 25)
            {
                FatigueLevelText.Text = "Fatigue Level: Critical (Player should be subbed)";
                MessageBox.Show("Player should be subbed!", "Fatigue Alert", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else if (percentage <= 50)
            {
                FatigueLevelText.Text = "Fatigue Level: Tired (Player is beginning to get tired)";
            }
            else
            {
                FatigueLevelText.Text = "Fatigue Level: Normal (Player is fine)";
            }
        }

        private async Task SaveSessionToDatabase()
        {
            try
            {
                if (_stepCounts.Count == 0)
                {
                    Logger.Info("No step data to save.");
                    return;
                }

                if (_assignedPlayers.TryGetValue(_trackedPlayerSlot, out string playerName))
                {
                    int playerId = await _dbHelper.GetPlayerIdByNameAsync(playerName);
                    string position = _trackedPlayerSlot.ToString();
                    int avgSteps = CalculateAverageSteps();

                    MessageBox.Show($"Saving match for PlayerID={playerId}, Position={position}, AvgSteps={avgSteps}");

                    await _dbHelper.AddMatchAsync(playerId, position, avgSteps);
                    Logger.Info($"Match saved: PlayerID={playerId}, Position={position}, AvgSteps={avgSteps}");
                    await _dbHelper.UpdatePlayerAverageStepsAsync(playerId);
                }
                else
                {
                    Logger.Warn("No player assigned to tracked slot.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save match data.");
                MessageBox.Show("Failed to save match: " + ex.Message);
            }
        }

        private async void EndMatchButton_Click(object sender, RoutedEventArgs e)
        {
            _matchStarted = false;
            EndMatchButton.IsEnabled = false;
            StartMatchButton.IsEnabled = true;

            await SaveSessionToDatabase();

            MessageBox.Show("Match ended and data saved.", "End Match", MessageBoxButton.OK, MessageBoxImage.Information);
        }



        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            await SaveSessionToDatabase();

            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                Console.WriteLine("Serial port closed.");
            }
        }
    }


    public class PlayerSlot
    {
        public int SlotNumber { get; set; }
        public string? PlayerName { get; set; }
        public Button JerseyButton { get; set; }
        public StackPanel StackPanel { get; set; } // NEW
    }
}
