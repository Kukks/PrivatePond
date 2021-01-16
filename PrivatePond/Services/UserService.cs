using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PrivatePond.Data;
using PrivatePond.Data.EF;

namespace PrivatePond.Controllers
{
    public class UserService
    {
        private readonly IDbContextFactory<PrivatePondDbContext> _dbContextFactory;

        public UserService(IDbContextFactory<PrivatePondDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<UserData> CreateUser()
        {
            await using var dbContext = _dbContextFactory.CreateDbContext();
            var user = new User();
            await dbContext.AddAsync(user);
            await dbContext.SaveChangesAsync();
            return FromDbModel(user);
        }

        public async Task<UserData> FindUser(string id)
        {
            await using var dbContext = _dbContextFactory.CreateDbContext();
            var user = await dbContext.Users.FindAsync(id);
            return user is not null ? FromDbModel(user) : null;
        }

        private UserData FromDbModel(User user)
        {
            return new()
            {
                Id = user.Id
            };
        }
    }
}