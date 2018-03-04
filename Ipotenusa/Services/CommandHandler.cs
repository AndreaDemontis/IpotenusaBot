using Discord.Commands;
using Discord.WebSocket;
using Discord;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System;

namespace Ipotenusa.Services
{
	public class CommandHandler
	{
		private readonly DiscordSocketClient _discord;
		private readonly CommandService _commands;
		private readonly IConfigurationRoot _config;
		private readonly IServiceProvider _provider;

		/// <summary>
		/// Makes a new instance of <see cref="CommandHandler"/> class.
		/// </summary>
		public CommandHandler(DiscordSocketClient discord, CommandService commands, IConfigurationRoot config, IServiceProvider provider)
		{
			_discord = discord;
			_commands = commands;
			_config = config;
			_provider = provider;

			_discord.MessageReceived += OnMessageReceivedAsync;
		}

		/// <summary>
		/// Handles messages.
		/// </summary>
		private async Task OnMessageReceivedAsync(SocketMessage s)
		{
			var msg = s as SocketUserMessage;

			// - Ignore null messages
			if (msg == null)
				return;

			// - Ignore self
			if (msg.Author.Id == _discord.CurrentUser.Id)
				return;

			// - Create command context
			var context = new SocketCommandContext(_discord, msg);

			int argPos = 0;

			// - Check if the message has a valid command prefix
			if (msg.HasStringPrefix(_config["prefix"], ref argPos) || msg.HasMentionPrefix(_discord.CurrentUser, ref argPos))
			{
				// - Execute the command
				var result = await _commands.ExecuteAsync(context, argPos, _provider);

				// - If not successful, reply with the error.
				if (!result.IsSuccess)
					await context.Channel.SendMessageAsync(result.ToString());
			}
		}
	}
}