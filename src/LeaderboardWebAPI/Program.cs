using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace LeaderboardWebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("secrets/appsettings.secrets.json", optional: true);
                    var hostConfig = config.Build();

                    if (!String.IsNullOrEmpty(hostConfig["KeyVaultName"]))
                    {
                        var secretClient = new SecretClient(
                            new Uri(hostConfig["KeyVaultName"]),
                            new ClientSecretCredential(
                                hostConfig["KeyVaultTenantID"],
                                hostConfig["KeyVaultClientID"],
                                hostConfig["KeyVaultClientSecret"])
                        // For managed identities use:
                        //   new DefaultAzureCredential()
                        );
                        config.AddAzureKeyVault(secretClient, new KeyVaultSecretManager());
                    }
                })
                .ConfigureLogging((context, builder) =>
                {
                    builder.AddApplicationInsights(options =>
                    {
                        options.IncludeScopes = true;
                        options.TrackExceptionsAsExceptionTelemetry = true;
                    });
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    Assembly startupAssembly = typeof(Startup).GetTypeInfo().Assembly;
                    webBuilder.UseStartup(startupAssembly.GetName().Name);
                });
    }
}
