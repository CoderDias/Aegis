using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Aegis.Infrastructure.Data;

public sealed class AegisDbContextFactory : IDesignTimeDbContextFactory<AegisDbContext>
{
    public AegisDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AegisDbContext>();
        optionsBuilder.UseSqlite("Data Source=App_Data/aegis.db");
        return new AegisDbContext(optionsBuilder.Options);
    }
}
