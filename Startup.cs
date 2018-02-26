using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ipotenusa
{
	public class Startup
	{
		public IConfigurationRoot Configuration { get; }

		public Startup(string[] args)
		{
			var builder = new ConfigurationBuilder()        // Create a new instance of the config builder
				.SetBasePath(AppContext.BaseDirectory)      // Specify the default location for the config file
				.AddJsonFile("_configuration.json");        // Add this (json encoded) file to the configuration
			Configuration = builder.Build();                // Build the configuration
		}

		public static async Task RunAsync(string[] args)
		{
			var startup = new Startup(args);
			await startup.RunAsync();
		}

		public async Task RunAsync()
		{
			var services = new ServiceCollection();

			// - Inser all services
			ConfigureServices(services);

			// - Build the service provider
			var provider = services.BuildServiceProvider();

			// - Start the logging service
			provider.GetRequiredService<LoggingService>();

			// - Start the command handler service
			provider.GetRequiredService<CommandHandler>();

			// - Start all modules
			await provider.GetRequiredService<StartupService>().StartAsync();
			await provider.GetRequiredService<BotChannel>().StartAsync();
			await provider.GetRequiredService<VoiceService>().StartAsync();
			await provider.GetRequiredService<RecorderService>().StartAsync();
			await provider.GetRequiredService<ParrotService>().StartAsync();

			// - Keep the program alive
			await Task.Delay(-1);
		}

		private void ConfigureServices(IServiceCollection services)
		{
			services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
			{
				LogLevel = LogSeverity.Info,
				MessageCacheSize = 1000
			}))

			.AddSingleton(new CommandService(new CommandServiceConfig
			{
				LogLevel = LogSeverity.Info,
				DefaultRunMode = RunMode.Async,
				CaseSensitiveCommands = false
			}))

			// - Add service modules
			.AddSingleton<StartupService>()
			.AddSingleton<BotChannel>()
			.AddSingleton<LoggingService>()
			.AddSingleton<VoiceService>()
			.AddSingleton<RecorderService>()
			.AddSingleton<ParrotService>()
			.AddSingleton<AniList>()
			.AddSingleton<CommandHandler>()
			.AddSingleton<Random>()
			.AddSingleton(Configuration);
		}
	}
}
