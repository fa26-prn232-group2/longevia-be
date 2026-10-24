using Npgsql;

namespace Progress.Infrastructure.Persistence;

public sealed class ProgressDatabaseOptions
{
    public string? Progress { get; set; }

    public bool IsValid()
    {
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(Progress);
            return !string.IsNullOrWhiteSpace(connection.Host)
                && connection.Database == "progress_db"
                && connection.Username == "progress_user";
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
