using Microsoft.EntityFrameworkCore;

namespace PrivatePond.Data.EF
{
    public class PrivatePondDesignTimeDbContextFactory :
        DesignTimeDbContextFactoryBase<PrivatePondDbContext>
    {
        public override string DefaultConnectionStringName { get; } = PrivatePondDbContext.DatabaseConnectionStringName;

        protected override PrivatePondDbContext CreateNewInstance(DbContextOptions<PrivatePondDbContext> options)
        {
            return new PrivatePondDbContext(options);
        }
    }
}