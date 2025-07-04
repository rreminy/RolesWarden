using DryIoc;
using DryIoc.MefAttributedModel;
using DryIoc.Microsoft.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RolesWarden.Db;
using Serilog;
using System;
using System.Reflection;
using System.Threading.Tasks;
using RolesWarden.Bot;
using System.Threading;
using System.Runtime.CompilerServices;
using DryIoc.ImTools;

namespace RolesWarden
{
    public static class Program
    {
        public static Container Container { get; private set; } = default!;
        public static WebApplication Application { get; private set; } = default!;

        public static async Task Main(string[] args)
        {
            // Bootstrap logging
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateBootstrapLogger();

            try
            {
                await MainInternal(args);
            }
            catch (HostAbortedException ex)
            {
                Log.Information(ex, "Host Aborted");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Fatal exception occurred");
                throw;
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }

        private static async Task MainInternal(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure Serilog
            builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Async(a => a.Console(), 256, false));

            // Configure DryIoc
            Container = CreateContainer();
            builder.Host.UseServiceProviderFactory(new DryIocServiceProviderFactory(Container));
            ConfigureServices(builder.Services);

            var app = builder.Build();
            Application = app;
            ConfigureApplication(app);
            await RunMigrationsAsync();

            Log.Information("Starting {name} v{version}", Assembly.GetExecutingAssembly().GetName().Name, Assembly.GetExecutingAssembly().GetName().Version);
            await app.StartAsync();
            Log.Information("{name} v{version} is ready", Assembly.GetExecutingAssembly().GetName().Name, Assembly.GetExecutingAssembly().GetName().Version);
            await app.WaitForShutdownAsync();
            Log.Information("Shutting down {name} v{version}", Assembly.GetExecutingAssembly().GetName().Name, Assembly.GetExecutingAssembly().GetName().Version);
            await app.StopAsync();
        }

        private static Container CreateContainer()
        {
            Log.Information("Creating DryIoC");
            var container = new Container(Rules.MicrosoftDependencyInjectionRules);
            container.RegisterExports(typeof(Program).Assembly);

            container.RegisterMany(Made.Of(r => ServiceInfo.Of<WardenBot>(), bot => bot.Client), Reuse.Singleton, setup: Setup.With(preventDisposal: true));
            container.RegisterMany(Made.Of(r => ServiceInfo.Of<WardenBot>(), bot => bot.Commands), Reuse.Singleton, setup: Setup.With(preventDisposal: true));
            container.RegisterMany(Made.Of(r => ServiceInfo.Of<WardenBot>(), bot => bot.Interactions), Reuse.Singleton, setup: Setup.With(preventDisposal: true));

            return container;
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Loggers
            services.AddLogging();

            // Database
            services.AddPooledDbContextFactory<WardenDbContext>((services, options) =>
            {
                var connectionString = services.GetRequiredService<IConfiguration>().GetValue<string>("WardenDb");
                options.UseNpgsql(connectionString, options =>
                {
                    options.EnableRetryOnFailure(); // Retry limit: 6, Max delay: 30 seconds, a System.Random is taken as well
                })
#if DEBUG
                    .EnableDetailedErrors()
                    .EnableSensitiveDataLogging()
                    .EnableThreadSafetyChecks()
#endif
                    ;
            });
        }

        private static void ConfigureApplication(WebApplication app)
        {
            var env = app.Environment;

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }
        }

        public static async Task RunMigrationsAsync(CancellationToken cancellationToken = default)
        {
            Log.Information("Initializing database migrations");
            var dbPool = Application.Services.GetRequiredService<IDbContextFactory<WardenDbContext>>();
            await using var dbContext = await dbPool.CreateDbContextAsync(cancellationToken);
            var migrations = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToListOrSelf();

            if (migrations.Count is 0)
            {
                Log.Information("Database is up to date");
                return;
            }

            Log.Information("Pending migrations found: {count}", migrations.Count);
            foreach (var migration in migrations)
            {
                Log.Information("Applying migration: {migration}", migration);
                await dbContext.Database.MigrateAsync(migration, cancellationToken);
            }
        }

        private static void UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Unobserved Task Exception");
        }

        private static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)e.ExceptionObject;
            if (e.IsTerminating) Log.Fatal(ex, "Fatal Unhandled Exception");
            else Log.Fatal(ex, "Unhandled Exception");
        }
    }
}
