using NLog;
using System.IO.Ports;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfFatigueMK2
{
    public partial class MainWindow : Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private SerialPort _serialPort;
        private Queue<int> _stepCounts = new Queue<int>(); // Queue to store step counts for 10-second intervals
        private DateTime _lastUpdate = DateTime.Now; // Tracks the last update time
        private const int MaxSteps = 200; // Maximum steps for 100% progress (adjust based on expected steps)
        private bool _initialized = false; // Flag to track if 3 intervals have passed
        private int _initialAverageSteps = 0; // Stores the initial average after 3 intervals
        private DatabaseHelper _dbHelper;


        public MainWindow()
        {
            InitializeComponent();
            Logger.Info("Application started.");
            string connStr = "Server=tcp:myplayerserver.database.windows.net,1433;Initial Catalog=PlayerTracker;Persist Security Info=False;User ID=AdamGleeson;Password=Tyrone19;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
            _dbHelper = new DatabaseHelper(connStr);
            InitializeSerialPort();
            LoadWeather();

            _ = InitDatabaseAndPlayer(); //No need to run once table and players were added.
            _ = LoadSubstitutesListAsync();
        }

        //No need to run now that tables and players have been made.
        private async Task InitDatabaseAndPlayer()
{
    try
    {
        Logger.Info("Connecting to DB...");
        //await _dbHelper.InitializeDatabaseAsync(); // Creates Teams and Players tables
        await _dbHelper.CreateMatchesTableAsync(); // Creates Matches table
        Logger.Info("All tables ready");

        // Optional: Only if needed
        // await _dbHelper.AddTeamAsync("Claremorris");
        // await _dbHelper.AddPlayerAsync(1, "Adam Gleeson", 5200);
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "DB setup failed");
        MessageBox.Show("Database error: " + ex.Message);
    }
}

        private async Task LoadSubstitutesListAsync()
        {
            try
            {
                string selectedTeam = "Claremorris"; // hardcoded team name

                var players = await _dbHelper.GetPlayersByTeamAsync(selectedTeam);
                SubstitutesListBox.ItemsSource = players;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load substitutes list.");
                MessageBox.Show("Failed to load players: " + ex.Message);
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

        //Button for opening new window
        private void PlayerButton_Click(object sender, RoutedEventArgs e)
        {
            // Calculate the average steps using the existing method
            int averageSteps = CalculateAverageSteps();

            // Create an instance of the new window
            PlayerDetailsWindow playerDetailsWindow = new PlayerDetailsWindow();

            // Pass the average steps to the new window
            playerDetailsWindow.AverageSteps = averageSteps;

            // Show the new window
            playerDetailsWindow.ShowDialog();
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

                Console.WriteLine("Serial port opened. Waiting for Arduino to initialize...");
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
            try
            {
                string data = _serialPort.ReadLine();
                Console.WriteLine($"Raw data received: {data}");

                if (data.StartsWith("Steps in last 5 seconds:"))
                {
                    string stepCountStr = data.Replace("Steps in last 5 seconds:", "").Trim();
                    if (int.TryParse(stepCountStr, out int stepCount))
                    {
                        UpdateFatigueLevel(stepCount);
                    }
                }
                else
                {
                    Console.WriteLine($"Unexpected data format: {data}");
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("No data received (timeout).");
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
                // Add the current step count to the queue
                _stepCounts.Enqueue(stepCount);
                _lastUpdate = DateTime.Now;

                // Keep only the last 3 intervals (30 seconds of data)
                if (_stepCounts.Count > 3)
                {
                    _stepCounts.Dequeue();
                }

                // After 3 intervals, calculate the initial average
                if (_stepCounts.Count == 3)
                {
                    _initialized = true; // Mark as initialized after 3 intervals
                    _initialAverageSteps = CalculateAverageSteps();

                    // Set the progress bar to 100% (full) based on the initial average
                    Dispatcher.Invoke(() =>
                    {
                        FatigueProgressBar.Value = 100; // Set to 100%
                        FatigueLevelText.Text = "Fatigue Level: Normal (Player is fine)";
                    });
                }
            }
            else
            {
                // Compare the new step count to the initial average
                double percentage = ((double)stepCount / _initialAverageSteps) * 100;
                percentage = Math.Min(percentage, 100); // Ensure it doesn't exceed 100%

                Dispatcher.Invoke(() =>
                {
                    // Update ProgressBar value
                    FatigueProgressBar.Value = percentage;

                    // Update Fatigue Level Text
                    UpdateFatigueLevelText(percentage);
                });
            }
        }

        private int CalculateAverageSteps()
        {
            int sum = 0;
            foreach (int steps in _stepCounts)
            {
                sum += steps;
            }
            return sum / _stepCounts.Count;
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

                int avgSteps = CalculateAverageSteps(); // You must calculate this first
                int playerId = 4;  //Hardcoded values for now
                string position = "7"; //Hardcoded position for now

                // Show this before inserting into the database
                MessageBox.Show($"Saving match for PlayerID={playerId}, Position={position}, AvgSteps={avgSteps}");

                await _dbHelper.AddMatchAsync(playerId, position, avgSteps);
                Logger.Info($"Match saved: PlayerID={playerId}, Position={position}, AvgSteps={avgSteps}");
                await _dbHelper.UpdatePlayerAverageStepsAsync(playerId);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save match data.");
                MessageBox.Show("Failed to save match: " + ex.Message);
            }
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
}

