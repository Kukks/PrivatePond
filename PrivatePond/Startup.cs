using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using NBitcoin;
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
            services.AddSingleton<TransferRequestService>();
            services.AddSingleton<IHostedService,TransferRequestService>(provider => provider.GetRequiredService<TransferRequestService>());
            services.AddDataProtection(options => options.ApplicationDiscriminator = "PrivatePond");
            services.AddOptions<PrivatePondOptions>()
                .Bind(Configuration.GetSection(PrivatePondOptions.OptionsConfigSection)).PostConfigure(
                    options =>
                    {
                        foreach (var optionsWallet in options.Wallets)
                        {
                            optionsWallet.WalletId = null;
                        }
                    });
            services.AddDbContextFactory<PrivatePondDbContext>(builder =>
            {
                var connString = Configuration.GetConnectionString(PrivatePondDbContext.DatabaseConnectionStringName);
                builder.UseNpgsql(connString, optionsBuilder => { optionsBuilder.EnableRetryOnFailure(10); });
            }, ServiceLifetime.Singleton);
            services.AddSingleton<IStartupTask, MigrationStartupTask>();
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                var filePath = Path.Combine(System.AppContext.BaseDirectory, "PrivatePond.xml");
                c.MapType<OutPoint>(() => new OpenApiSchema { Type = "string" });
                c.IncludeXmlComments(filePath);
                c.SwaggerDoc("v1", new OpenApiInfo {Title = "PrivatePond", Version = "v1"});
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "PrivatePond v1"));
            }
    

            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}