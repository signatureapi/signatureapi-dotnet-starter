using Microsoft.EntityFrameworkCore;

namespace SignatureApiStarter.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Agreement> Agreements => Set<Agreement>();
}
