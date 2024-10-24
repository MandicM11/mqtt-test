using DbConnection;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql("Host=localhost;Database=Mqtt;Username=postgres;Password=1234");
    }

    public DbSet<RoomTemperature> RoomTemperatures { get; set; }
}
