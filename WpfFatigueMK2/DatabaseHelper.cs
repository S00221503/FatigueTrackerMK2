using Microsoft.Data.SqlClient;
using System;

using System.Threading.Tasks;

public class DatabaseHelper
{
    private readonly string _connectionString;

    public DatabaseHelper(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeDatabaseAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var createTeams = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Teams' AND xtype='U')
            CREATE TABLE Teams (
                TeamID INT PRIMARY KEY IDENTITY(1,1),
                TeamName NVARCHAR(100) NOT NULL
            );";

        var createPlayers = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Players' AND xtype='U')
            CREATE TABLE Players (
                PlayerID INT PRIMARY KEY IDENTITY(1,1),
                TeamID INT NOT NULL,
                Name NVARCHAR(100) NOT NULL,
                AvgStepsPerGame FLOAT,
                FOREIGN KEY (TeamID) REFERENCES Teams(TeamID)
            );";

        using var cmd1 = new SqlCommand(createTeams, connection);
        await cmd1.ExecuteNonQueryAsync();

        using var cmd2 = new SqlCommand(createPlayers, connection);
        await cmd2.ExecuteNonQueryAsync();
    }

    public async Task CreateMatchesTableAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var createMatches = @"
        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Matches' AND xtype='U')
        CREATE TABLE Matches (
            MatchID INT PRIMARY KEY IDENTITY(1,1),
            PlayerID INT NOT NULL,
            MatchDate DATETIME NOT NULL,
            Position NVARCHAR(100),
            AvgSteps FLOAT,
            FOREIGN KEY (PlayerID) REFERENCES Players(PlayerID)
        );";

        using var cmd = new SqlCommand(createMatches, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<string>> GetPlayersByTeamAsync(string teamName)
    {
        var players = new List<string>();

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        string query = @"
        SELECT P.Name
        FROM Players P
        JOIN Teams T ON P.TeamID = T.TeamID
        WHERE T.TeamName = @TeamName";

        using var cmd = new SqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@TeamName", teamName);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            players.Add(reader.GetString(0));
        }

        return players;
    }



    //Everytime a game is played this will update the players average steps in the game
    public async Task UpdatePlayerAverageStepsAsync(int playerId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        string avgQuery = @"SELECT AVG(AvgSteps) FROM Matches WHERE PlayerID = @PlayerID";
        using var avgCmd = new SqlCommand(avgQuery, connection);
        avgCmd.Parameters.AddWithValue("@PlayerID", playerId);

        object result = await avgCmd.ExecuteScalarAsync();
        if (result != DBNull.Value)
        {
            double newAvg = Convert.ToDouble(result);

            string updatePlayer = @"UPDATE Players SET AvgStepsPerGame = @Avg WHERE PlayerID = @PlayerID";
            using var updateCmd = new SqlCommand(updatePlayer, connection);
            updateCmd.Parameters.AddWithValue("@Avg", newAvg);
            updateCmd.Parameters.AddWithValue("@PlayerID", playerId);

            await updateCmd.ExecuteNonQueryAsync();
        }
    }


    public async Task AddMatchAsync(int playerId, string position, double avgSteps)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        string insert = @"INSERT INTO Matches (PlayerID, MatchDate, Position, AvgSteps)
                      VALUES (@PlayerID, @Date, @Position, @Steps)";
        using var cmd = new SqlCommand(insert, connection);
        cmd.Parameters.AddWithValue("@PlayerID", playerId);
        cmd.Parameters.AddWithValue("@Date", DateTime.Now);
        cmd.Parameters.AddWithValue("@Position", position);
        cmd.Parameters.AddWithValue("@Steps", avgSteps);

        await cmd.ExecuteNonQueryAsync();
    }


    public async Task AddTeamAsync(string teamName)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var insert = "INSERT INTO Teams (TeamName) VALUES (@TeamName)";
        using var cmd = new SqlCommand(insert, connection);
        cmd.Parameters.AddWithValue("@TeamName", teamName);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task AddPlayerAsync(int teamId, string name, double avgSteps)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var insert = "INSERT INTO Players (TeamID, Name, AvgStepsPerGame) VALUES (@TeamID, @Name, @Avg)";
        using var cmd = new SqlCommand(insert, connection);
        cmd.Parameters.AddWithValue("@TeamID", teamId);
        cmd.Parameters.AddWithValue("@Name", name);
        cmd.Parameters.AddWithValue("@Avg", avgSteps);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<(string Name, double AvgStepsPerGame)>> GetPlayersByTeamAsync(int teamId)
    {
        var players = new List<(string, double)>();

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var query = "SELECT Name, AvgStepsPerGame FROM Players WHERE TeamID = @TeamID";
        using var cmd = new SqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@TeamID", teamId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string name = reader.GetString(0);
            double avg = reader.GetDouble(1);
            players.Add((name, avg));
        }

        return players;
    }
}
