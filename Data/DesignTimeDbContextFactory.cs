using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DentalClinic.Data
{
    /// <summary>Lets `dotnet ef` create migrations without starting the whole application.</summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext(string[] args)
        {
            var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? "Server=localhost,1433;Database=dental_clinic;User Id=sa;Password=Aurora_Dent_123;TrustServerCertificate=True";

            var options = new DbContextOptionsBuilder<DatabaseContext>().UseSqlServer(connection).Options;
            return new DatabaseContext(options);
        }
    }
}
