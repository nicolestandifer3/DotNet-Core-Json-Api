using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using JsonApiDotNetCoreExample.Data;
using Microsoft.EntityFrameworkCore;
using JsonApiDotNetCore.Extensions;
using DotNetCoreDocs.Configuration;
using System;
using JsonApiDotNetCoreExample;
using Moq;
using JsonApiDotNetCoreExampleTests.Services;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCoreExample.Models;
using JsonApiDotNetCoreExampleTests.Repositories;

namespace JsonApiDotNetCoreExampleTests.Startups
{
    public class AuthorizedStartup : Startup
    {
        public AuthorizedStartup(IHostingEnvironment env)
        : base (env)
        {  }        

        public override IServiceProvider ConfigureServices(IServiceCollection services)
        {
            var loggerFactory = new LoggerFactory();

            loggerFactory
              .AddConsole(LogLevel.Trace);

            services.AddSingleton<ILoggerFactory>(loggerFactory);

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(GetDbConnectionString());
            }, ServiceLifetime.Transient);

            services.AddJsonApi<AppDbContext>(opt =>
            {
                opt.Namespace = "api/v1";
                opt.DefaultPageSize = 5;
                opt.IncludeTotalRecordCount = true;
            });

            // custom authorization implementation
            var authServicMock = new Mock<IAuthorizationService>();
            authServicMock.SetupAllProperties();
            services.AddSingleton<IAuthorizationService>(authServicMock.Object);
            services.AddScoped<IEntityRepository<TodoItem>, AuthorizedTodoItemsRepository>();

            services.AddDocumentationConfiguration(Config);

            return services.BuildServiceProvider();
        }
    }
}
