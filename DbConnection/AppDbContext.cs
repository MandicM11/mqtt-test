using DbConnection;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    private readonly string _connectionString;

    public AppDbContext(DbContextOptions<AppDbContext> options, string connectionString)
        : base(options)
    {
        _connectionString = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql(_connectionString);
        }
    }

    public DbSet<RoomTemperature> RoomTemperatures { get; set; }
}
