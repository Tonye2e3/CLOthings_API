using Microsoft.EntityFrameworkCore;

namespace CLOthings_API.Models
{
    public partial class CLOthingsContext : DbContext
    {
        public CLOthingsContext()
        {

        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                IConfiguration Config = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json")
                    .Build();
                optionsBuilder.UseSqlServer(Config.GetConnectionString("CLOthingsConnection"));
            }
        }
    }
}



