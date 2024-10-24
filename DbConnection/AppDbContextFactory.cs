using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Provide your PostgreSQL connection string here
        optionsBuilder.UseNpgsql("Host=localhost;Database=Mqtt;Username=postgres;Password=1234");

        return new AppDbContext(optionsBuilder.Options);
    }
}
