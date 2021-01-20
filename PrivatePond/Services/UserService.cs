using System;
using System.Collections.Generic;
using System.Linq;
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

        public async Task<List<UserData>> GetUsers(int skip = 0, int take = int.MaxValue)
        {
            await using var dbContext = _dbContextFactory.CreateDbContext();
            return await dbContext.Users.Skip(skip).Take(take).Select(user1 => FromDbModel(user1)).ToListAsync();
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