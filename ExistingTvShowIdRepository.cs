using Microsoft.Data.Sqlite;

namespace webhooktesting;

public class ExistingTvShowIdRepository
{
    private readonly string _connectionString;

    public ExistingTvShowIdRepository(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
        Initialize();
    }
    
    private void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = @"
                CREATE TABLE IF NOT EXISTS ExistingTvShowIds (
                    Id INTEGER PRIMARY KEY
                );";
        command.ExecuteNonQuery();
    }
    
    public HashSet<int> GetAllIds()
    {
        var ids = new HashSet<int>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM ExistingTvShowIds;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            ids.Add(reader.GetInt32(0));
        }
        return ids;
    }
    
    public void AddId(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO ExistingTvShowIds (Id) VALUES (@id);";
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }

}