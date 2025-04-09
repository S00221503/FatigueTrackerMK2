using System.Windows;
using System.Threading.Tasks;

namespace WpfFatigueMK2
{
    public partial class PlayerDetailsWindow : Window
    {
        private readonly DatabaseHelper _dbHelper;

        public PlayerDetailsWindow(string playerName, int slotNumber, DatabaseHelper dbHelper)
        {
            InitializeComponent();
            _dbHelper = dbHelper;
            LoadPlayerDetails(playerName, slotNumber);
        }

        private async void LoadPlayerDetails(string playerName, int slotNumber)
        {
            PlayerNameTextBlock.Text = playerName;
            PlayerSlotTextBlock.Text = slotNumber.ToString();

            int playerId = await _dbHelper.GetPlayerIdByNameAsync(playerName);
            int avgSteps = await _dbHelper.GetAverageStepsForPlayerAsync(playerId);

            PlayerAvgStepsTextBlock.Text = avgSteps.ToString();
        }
    }
}
