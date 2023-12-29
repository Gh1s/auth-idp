using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Sinks.Elasticsearch;
using Serilog.Sinks.SystemConsole.Themes;
using System;

namespace Csb.Auth.Idp
{
    public class Program
    {
        private static readonly AnsiConsoleTheme Theme = AnsiConsoleTheme.Literate;
        private const string OutputTemplate = "[{Timestamp:HH:mm:ss.fff} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}";

        public static int Main(string[] args)
        {
            // The global logger is used only when the host starts and stops.
            // Otherwise, the logger factory configured in the host is used.
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .WriteTo.Console(theme: Theme, outputTemplate: OutputTemplate)
                .CreateLogger();

            try
            {
                Log.Information("Starting web host.");
                CreateHostBuilder(args).Build().Run();
                return 0;
            }
            catch (Exception e)
            {
                Log.Fatal(e, "Host terminated unexpectedly.");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog((context, loggerConfig) =>
                {
                    loggerConfig.ReadFrom.Configuration(context.Configuration);
                    loggerConfig.Enrich.FromLogContext();
                    loggerConfig.WriteTo.Console(theme: Theme, outputTemplate: OutputTemplate);

                    if (context.Configuration.GetValue<bool>("Serilog:Elasticsearch:Enabled"))
                    {
                        var elasticsearchSinkOptions =
                            new ElasticsearchSinkOptions(new Uri(context.Configuration.GetValue<string>("Serilog:Elasticsearch:Url")))
                            {
                                AutoRegisterTemplate = true,
                                AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
                                IndexFormat = context.Configuration.GetValue<string>("Serilog:Elasticsearch:IndexFormat")
                            };
                        if (context.Configuration.GetValue<bool>("Serilog:Elasticsearch:BypassCertificateValidation"))
                        {
                            elasticsearchSinkOptions.ModifyConnectionSettings = configuration =>
                                configuration.ServerCertificateValidationCallback((o, certificate, arg3, arg4) => true);
                        }
                        var assembly = typeof(Program).Assembly;
                        loggerConfig.Enrich.WithProperty("Assembly", assembly.GetName().Name);
                        loggerConfig.Enrich.WithProperty("Version", $"{assembly.GetName().Version.Major}.{assembly.GetName().Version.Minor}.{assembly.GetName().Version.Build}");
                        loggerConfig.WriteTo.Elasticsearch(elasticsearchSinkOptions);
                    }
                })
                .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
    }
}
