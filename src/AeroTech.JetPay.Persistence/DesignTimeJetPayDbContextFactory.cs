using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AeroTech.JetPay.Persistence
{
    public sealed class DesignTimeJetPayDbContextFactory : IDesignTimeDbContextFactory<JetPayDbContext>
    {
        public JetPayDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<JetPayDbContext>()
                .UseSqlServer("Server=localhost\\SQLEXPRESS;Database=DotAirAeroTechJetPay;Trusted_Connection=True;TrustServerCertificate=True")
                .Options;

            return new JetPayDbContext(options, null!, null!, null!);
        }
    }
}
