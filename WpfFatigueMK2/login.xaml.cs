using Microsoft.Data.SqlClient;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace WpfFatigueMK2
{
    /// <summary>
    /// Interaction logic for login.xaml
    /// </summary>
    public partial class login : Window
    {
        private readonly string connStr = "Server=tcp:myplayerserver.database.windows.net,1433;Initial Catalog=PlayerTracker;Persist Security Info=False;User ID=AdamGleeson;Password=Tyrone19;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

        public login()
        {
            InitializeComponent();
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
           // MessageBox.Show("Login button clicked"); // Test line

            string username = UsernameBox.Text;
            string password = PasswordBox.Password;

            int? managerId = await GetManagerIdAsync(username, password);

            if (managerId != null)
            {
                MainWindow main = new MainWindow((int)managerId);
                main.Show();
                this.Close();
            }
            else
            {
                MessageBox.Show("Invalid login. Try again.");
            }
        }


        private async Task<int?> GetManagerIdAsync(string username, string password)
        {
            using var connection = new SqlConnection(connStr);
            await connection.OpenAsync();

            string query = "SELECT ManagerId FROM Managers WHERE Username = @username AND Password = @password";

            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@password", password); // hash later for security

            var result = await cmd.ExecuteScalarAsync();
            return result != null ? (int?)result : null;
        }
    }
}
