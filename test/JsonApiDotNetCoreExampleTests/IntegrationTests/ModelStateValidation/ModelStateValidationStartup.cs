using JsonApiDotNetCore.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ModelStateValidation
{
    public sealed class ModelStateValidationStartup<TDbContext> : TestableStartup<TDbContext>
        where TDbContext : DbContext
    {
        public ModelStateValidationStartup(IConfiguration configuration) : base(configuration)
        {
        }

        protected override void SetJsonApiOptions(JsonApiOptions options)
        {
            base.SetJsonApiOptions(options);

            options.ValidateModelState = true;
        }
    }
}
