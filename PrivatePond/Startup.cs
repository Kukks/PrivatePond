using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using PrivatePond.Controllers;
using PrivatePond.Data;
using PrivatePond.Data.EF;
using PrivatePond.Services.NBXplorer;

namespace PrivatePond
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddNBXPlorerIntegration(Configuration);
            services.AddHttpClient();
            services.AddSingleton<DepositService>();
            services.AddSingleton<UserService>();
            services.AddDataProtection(options => options.ApplicationDiscriminator = "PrivatePond");
            services.AddOptions<PrivatePondOptions>().Bind(Configuration.GetSection(PrivatePondOptions.OptionsConfigSection)).PostConfigure(
                options =>
                {
                    foreach (var optionsWallet in options.Wallets)
                    {
                        optionsWallet.WalletId = null;
                    }
                });
            // services.AddDbContext<PrivatePondDbContext>(builder =>
            //     builder.UseNpgsql(
            //         Configuration.GetConnectionString(PrivatePondDbContext.DatabaseConnectionStringName)), ServiceLifetime.Singleton);
            services.AddDbContextFactory<PrivatePondDbContext>(builder =>
                builder.UseNpgsql(
                    Configuration.GetConnectionString(PrivatePondDbContext.DatabaseConnectionStringName)??"fake"), ServiceLifetime.Singleton);
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo {Title = "PrivatePond", Version = "v1"});
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, PrivatePondDbContext privatePondDbContext )
        {
            privatePondDbContext.Database.Migrate();
            
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "PrivatePond v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}