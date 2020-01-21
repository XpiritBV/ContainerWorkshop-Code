using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FeatureToggle.Internal;
using Leaderboard.WebAPI.Infrastructure;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.AzureAppServices;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NSwag.AspNetCore;
using NSwag.Generation.Processors;

namespace Leaderboard.WebAPI
{
    public class Startup
    {
        public IConfigurationRoot Configuration { get; }

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                // In Kubernetes deployment mount settings file from secret
                .AddJsonFile($"secrets/appsettings.secrets.json", optional: true);
            if (env.EnvironmentName == Microsoft.Extensions.Hosting.Environments.Development)
            {
                builder.AddUserSecrets<Startup>(true);
            }
            builder.AddEnvironmentVariables();

            // In Docker Swarm include Docker secrets 
            //if (env.IsProduction())
            //{
            //    builder.AddDockerSecrets();
            //}

            Configuration = builder.Build();
            if (env.EnvironmentName != Microsoft.Extensions.Hosting.Environments.Development)
            {
                builder.AddAzureKeyVault(
                    Configuration["KeyVaultName"],
                    Configuration["KeyVaultClientID"],
                    Configuration["KeyVaultClientSecret"]
                );
                Configuration = builder.Build();
            }
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<LeaderboardContext>(options =>
            {
                string connectionString =
                    Configuration.GetConnectionString("LeaderboardContext");
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
                });
            });

            ConfigureFeatures(services);
            ConfigureApiOptions(services);
            ConfigureTelemetry(services);
            ConfigureOpenApi(services);
            ConfigureSecurity(services);
            ConfigureHealth(services);
            ConfigureSerialization();

            services.AddMvc(options => options.EnableEndpointRouting = false)
                .AddXmlSerializerFormatters();
                //.SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
        }

        private void ConfigureFeatures(IServiceCollection services)
        {
            var provider = new AppSettingsProvider { Configuration = this.Configuration };
            services.AddSingleton(new AdvancedHealthFeature { ToggleValueProvider = provider });
        }

        private void ConfigureHealth(IServiceCollection services)
        {
            services.AddHealthChecks(checks =>
            {
                checks.AddHealthCheckGroup(
                        "memory",
                        group => group
                            .AddPrivateMemorySizeCheck(2000000000)
                            .AddVirtualMemorySizeCheck(30000000000000)
                            .AddWorkingSetCheck(2000000000),
                        CheckStatus.Unhealthy
                    );

                // Use feature toggle to add this functionality
                var feature = services.BuildServiceProvider().GetRequiredService<AdvancedHealthFeature>();
                if (feature.FeatureEnabled)
                {
                    checks.AddHealthCheckGroup(
                        "memory",
                        group => group
                            .AddPrivateMemorySizeCheck(200000000) // Maximum private memory
                            .AddVirtualMemorySizeCheck(3000000000000)
                            .AddWorkingSetCheck(200000000),
                        CheckStatus.Unhealthy
                    );
                }
            });
        }

        private void ConfigureTelemetry(IServiceCollection services)
        {
            services.AddSingleton<ITelemetryInitializer, ServiceNameInitializer>();
            var env = services.BuildServiceProvider().GetRequiredService<IWebHostEnvironment>();
            services.AddApplicationInsightsTelemetry(options =>
            {
                options.DeveloperMode = env.EnvironmentName == Microsoft.Extensions.Hosting.Environments.Development;
                    
                options.InstrumentationKey = Configuration["ApplicationInsights:InstrumentationKey"];
            });
        }

        private void ConfigureSecurity(IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
            });
        }

        private void ConfigureOpenApi(IServiceCollection services)
        {
            services.AddSwaggerDocument();
        }

        private void ConfigureApiOptions(IServiceCollection services)
        {
            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var problemDetails = new ValidationProblemDetails(context.ModelState)
                    {
                        Instance = context.HttpContext.Request.Path,
                        Status = StatusCodes.Status400BadRequest,
                        Type = "https://asp.net/core",
                        Detail = "Please refer to the errors property for additional details."
                    };
                    return new BadRequestObjectResult(problemDetails)
                    {
                        ContentTypes = { "application/problem+json", "application/problem+xml" }
                    };
                };
            });
        }

        private static void ConfigureSerialization()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env,
            ILoggerFactory loggerFactory, LeaderboardContext context)
        {

            LoggerFactory.Create(builder => builder.AddConsole());
            LoggerFactory.Create(builder => builder.AddDebug());
            if (env.EnvironmentName == Microsoft.Extensions.Hosting.Environments.Development)
            {
                
                DbInitializer.Initialize(context).Wait();
                app.UseDeveloperExceptionPage();
            }

            app.UseOpenApi();
            app.UseSwaggerUi3();
            app.UseMvc();
        }
    }
}