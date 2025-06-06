﻿using Microsoft.Data.SqlClient;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Text.Json;

namespace WpfFatigueMK2
{
    public partial class MainWindow : Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly HttpClient _httpClient = new HttpClient();
        private DispatcherTimer _pollTimer;
        private Queue<int> _stepCounts = new();
        private DateTime _lastUpdate = DateTime.Now;
        private const int MaxSteps = 200;
        private bool _initialized = false;
        private int _initialAverageSteps = 0;
        private DatabaseHelper _dbHelper;
        private bool _matchStarted = false;
        private int _managerId;
        private List<PlayerSlot> _playerSlots = new();
        private Dictionary<int, string> _assignedPlayers = new();
        private int _trackedPlayerSlot = 1; // Arduino tracks slot 1
        private ObservableCollection<string> _substitutePlayers = new();

        public MainWindow(int managerId)
        {
            InitializeComponent();
            Logger.Info("Application started.");
            string connStr = "Server=tcp:myplayerserver.database.windows.net,1433;Initial Catalog=PlayerTracker;Persist Security Info=False;User ID=AdamGleeson;Password=Tyrone19;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
            _dbHelper = new DatabaseHelper(connStr);
            LoadWeather();
            TrackingSlotListBox.ItemsSource = Enumerable.Range(1, 15);
            TrackingSlotListBox.SelectedIndex = 0;
            _managerId = managerId;


            _ = LoadSubstitutesListAsync(_managerId);
            InitializeJerseyGrid();

            SubstitutesListBox.MouseDoubleClick += SubstitutesListBox_MouseDoubleClick;

            _pollTimer = new DispatcherTimer();
            _pollTimer.Interval = TimeSpan.FromSeconds(5);
            _pollTimer.Tick += async (s, e) => await PollStepDataFromYunAsync();
        }

        private async Task LoadSubstitutesListAsync(int managerId)
        {
            try
            {
                // Get the team(s) assigned to the manager
                var teamNames = await _dbHelper.GetTeamsByManagerIdAsync(managerId);

                var allPlayers = new List<string>();

                foreach (var teamName in teamNames)
                {
                    var players = await _dbHelper.GetPlayersByTeamAsync(teamName);
                    allPlayers.AddRange(players);
                }

                _substitutePlayers = new ObservableCollection<string>(allPlayers);
                SubstitutesListBox.ItemsSource = _substitutePlayers;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load substitutes list.");
                MessageBox.Show("Failed to load players: " + ex.Message);
            }
        }


        private void TrackingSlotListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TrackingSlotListBox.SelectedItem is int selectedSlot)
            {
                _trackedPlayerSlot = selectedSlot;
                Logger.Info($"Tracking player in slot: {_trackedPlayerSlot}");
            }
        }


        private void CoachOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            var coachWindow = new CoachScreenWindow(_managerId); // pass managerId if needed
            coachWindow.Show();
        }


        private void InitializeJerseyGrid()
        {
            for (int i = 1; i <= 15; i++)
            {
                var stack = new StackPanel { Orientation = Orientation.Vertical };

                stack.Children.Add(new TextBlock
                {
                    Text = i.ToString(),
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 5)
                });

                stack.Children.Add(new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/Images/jersey.jpg")),
                    Width = 65,
                    Height = 65
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
                    StackPanel = stack
                });
                btn.MouseDoubleClick += JerseyButton_MouseDoubleClick;
            }
        }

        private async void LoadWeather()
        {
            string? city = await GetCityFromIPAsync();
            city ??= "Dublin"; // fallback if IP lookup fails

            try
            {
                var forecast = await WeatherService.GetForecastAsync(city);

                if (forecast?.data?.weather?.Count > 0)
                {
                    var firstDay = forecast.data.weather[0];
                    var firstHour = firstDay.hourly?.FirstOrDefault();

                    if (firstHour != null)
                    {
                        string? temp = firstHour.tempC;
                        string description = firstHour.weatherDesc?.FirstOrDefault()?.value ?? "No description";

                        WeatherTextBlock.Text = $"{temp}°C - {description}";
                    }
                }
            }
            catch (Exception ex)
            {
                WeatherTextBlock.Text = "Failed to load weather.";
                Logger.Error(ex, "Error loading weather with auto-location.");
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
            _pollTimer.Start();
            StartMatchButton.IsEnabled = false;
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

                    emptySlot.StackPanel.Children.Add(new TextBlock
                    {
                        Text = playerName,
                        FontSize = 10,
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Center
                    });

                    _substitutePlayers.Remove(playerName);

                }
            }
        }

        private async Task PollStepDataFromYunAsync()
        {
            Logger.Info("Polling Yun for step data...");

            if (!_matchStarted)
            {
                Logger.Info("Match not started, skipping poll.");
                return;
            }

            try
            {
                string response = await _httpClient.GetStringAsync("http://192.168.240.1/arduino/data");
                Logger.Info("Raw response received from Yun:\n" + response);

                // Extract the last non-empty line
                string? line = response.Split('\n')
                                      .Select(l => l.Trim())
                                      .Where(l => !string.IsNullOrWhiteSpace(l))
                                      .LastOrDefault();

                Logger.Info("Extracted line: " + line);

                if (line != null && line.StartsWith("Steps in last 5 seconds:"))
                {
                    string stepCountStr = line.Replace("Steps in last 5 seconds:", "").Trim();
                    if (int.TryParse(stepCountStr, out int stepCount))
                    {
                        Logger.Info($"Parsed step count: {stepCount}");
                        UpdateFatigueLevel(stepCount);
                    }
                    else
                    {
                        Logger.Warn("Failed to parse step count from extracted line.");
                    }
                }
                else
                {
                    Logger.Warn("Unexpected or missing data line: " + line);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error while polling step data from Yun.");
            }
        }


        private void UpdateFatigueLevel(int stepCount)
        {
            Logger.Info($"Updating fatigue level with step count: {stepCount}");

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

                    Logger.Info($"Initialized with average steps: {_initialAverageSteps}");
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

                Logger.Info($"Fatigue level updated. Percentage: {percentage}");
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

        //Get users location based on their IP
        public static async Task<string?> GetCityFromIPAsync()
        {
            try
            {
                using var client = new HttpClient();
                var response = await client.GetStringAsync("http://ip-api.com/json/");
                var json = JsonSerializer.Deserialize<GeoInfo>(response);
                return json?.city;
            }
            catch
            {
                return null;
            }
        }

        public class GeoInfo
        {
            public string? city { get; set; }
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

                if (_assignedPlayers.TryGetValue(_trackedPlayerSlot, out string? playerName))
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
            _pollTimer.Stop();
            EndMatchButton.IsEnabled = false;
            StartMatchButton.IsEnabled = true;

            await SaveSessionToDatabase();

            MessageBox.Show("Match ended and data saved.", "End Match", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            await SaveSessionToDatabase();
        }
    }

    public class PlayerSlot
    {
        public int SlotNumber { get; set; }
        public string? PlayerName { get; set; }
        public Button JerseyButton { get; set; }
        public StackPanel StackPanel { get; set; }
    }
} 
