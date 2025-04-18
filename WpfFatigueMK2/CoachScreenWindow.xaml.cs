using Microsoft.Data.SqlClient;
using System.Windows;

namespace WpfFatigueMK2
{


    public partial class CoachScreenWindow : Window
    {
        private readonly int _managerId;
        string connStr = "Server=tcp:myplayerserver.database.windows.net,1433;Initial Catalog=PlayerTracker;Persist Security Info=False;User ID=AdamGleeson;Password=Tyrone19;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

        public CoachScreenWindow(int managerId)
        {
            InitializeComponent();
            _managerId = managerId;

            LoadCoachData();
        }

        private async void LoadCoachData()
        {
            using var connection = new SqlConnection(connStr);
            await connection.OpenAsync();

            // Get manager name
            string managerName = "";
            var getManagerNameCmd = new SqlCommand("SELECT Name FROM Managers WHERE ManagerId = @id", connection);
            getManagerNameCmd.Parameters.AddWithValue("@id", _managerId);

            var result = await getManagerNameCmd.ExecuteScalarAsync();
            if (result != null)
            {
                managerName = result.ToString();
                CoachNameLabel.Content = $"Coach: {managerName}";
            }

            // Get teams for this manager
            var getTeamsCmd = new SqlCommand("SELECT TeamName FROM Teams WHERE ManagerId = @id", connection);
            getTeamsCmd.Parameters.AddWithValue("@id", _managerId);

            var reader = await getTeamsCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string teamName = reader.GetString(0);
                TeamsListBox.Items.Add(teamName); // ListBox must exist in XAML
            }
        }
    }
}
