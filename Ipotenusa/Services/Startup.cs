using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Ipotenusa.Services
{
	public class StartupService
	{
		private readonly DiscordSocketClient _discord;
		private readonly CommandService _commands;
		private readonly IConfigurationRoot _config;

		/// <summary>
		/// Makes a new instance of <see cref="StartupService"/> class.
		/// </summary>
		public StartupService(DiscordSocketClient discord, CommandService commands, IConfigurationRoot config)
		{
			_config = config;
			_discord = discord;
			_commands = commands;
		}

		/// <summary>
		/// Initialize this service.
		/// </summary>
		public async Task StartAsync()
		{
			// - Get the discord token from the config file
			string discordToken = _config["tokens:discord"];

			if (string.IsNullOrWhiteSpace(discordToken))
				throw new Exception("Please enter your bot's token into the `_configuration.json` file found in the applications root directory.");
			
			// - Login on discord
			await _discord.LoginAsync(TokenType.Bot, discordToken);
			await _discord.StartAsync();

			// - Load commands and modules into the command service
			await _commands.AddModulesAsync(Assembly.GetEntryAssembly());
		}
	}
}