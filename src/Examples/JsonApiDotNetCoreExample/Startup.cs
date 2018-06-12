using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using JsonApiDotNetCoreExample.Data;
using Microsoft.EntityFrameworkCore;
using JsonApiDotNetCore.Extensions;
using System;

namespace JsonApiDotNetCoreExample
{
    public class Startup
    {
        public readonly IConfiguration Config;

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Config = builder.Build();
        }

        public virtual IServiceProvider ConfigureServices(IServiceCollection services)
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole(LogLevel.Trace);

            services
                .AddSingleton<ILoggerFactory>(loggerFactory)
                .AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(GetDbConnectionString()), ServiceLifetime.Transient)
                .AddJsonApi<AppDbContext>(options => {
                    options.Namespace = "api/v1";
                    options.DefaultPageSize = 5;
                    options.IncludeTotalRecordCount = true;
                });

            var provider = services.BuildServiceProvider();
            var appContext = provider.GetRequiredService<AppDbContext>();
            if(appContext == null)
                throw new ArgumentException();
                
            return provider;
        }

        public virtual void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            AppDbContext context)
        {
            context.Database.EnsureCreated();

            loggerFactory.AddConsole(Config.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseJsonApi();
        }

        public string GetDbConnectionString() => Config["Data:DefaultConnection"];
    }
}
