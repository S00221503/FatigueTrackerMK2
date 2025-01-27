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
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger(); //Use this for logging
        private SerialPort _serialPort;

        public MainWindow()
        {
            InitializeComponent();
            Logger.Info("Application started.");
            InitializeSerialPort();
        }

        private void InitializeSerialPort()
        {
            try
            {
                // Configure and open the serial port
                _serialPort = new SerialPort("COM5", 9600)
                {
                    DtrEnable = true, // Enable Data Terminal Ready to reset Arduino
                    RtsEnable = true, // Enable Request to Send
                    ReadTimeout = 2000, // Set timeout for reading
                    WriteTimeout = 500  // Set timeout for writing
                };

                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();

                // Wait for the Arduino to initialize
                Console.WriteLine("Serial port opened. Waiting for Arduino to initialize...");
                System.Threading.Thread.Sleep(2000); // Wait 2 seconds for Arduino reset
                _serialPort.DiscardInBuffer(); // Clear any leftover data in the buffer

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
                // Read incoming data
                string data = _serialPort.ReadLine();
                Console.WriteLine($"Raw data received: {data}");

                // Parse and update the UI only if the data is in the expected format
                if (data.StartsWith("Steps in last 5 seconds:"))
                {
                    string stepCount = data.Replace("Steps in last 5 seconds:", "").Trim();
                    Dispatcher.Invoke(() =>
                    {
                        StepCountTextBlock.Text = $"Steps in last 5 seconds: {stepCount}";
                    });
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

        private void SaveStepData(string stepCount)
        {
            // Example: Write step count to a text file
            string filePath = "StepData.txt";

            // Append step data with a timestamp
            string entry = $"{DateTime.Now}: {stepCount} steps\n";
            System.IO.File.AppendAllText(filePath, entry);

            // Log success
            Console.WriteLine($"Step data saved: {entry}");
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            // Simulate updating the TextBlock with step data
            Random random = new Random();
            int simulatedStepCount = random.Next(0, 100); // Generate a random step count

            // Update the TextBlock
            StepCountTextBlock.Text = $"Steps: {simulatedStepCount}";
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                Console.WriteLine("Serial port closed.");
            }
        }

    }
}