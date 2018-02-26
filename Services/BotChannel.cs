using Discord.Commands;
using Discord.WebSocket;
using Discord;
using Discord.Rest;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ipotenusa
{
	public class BotChannel
	{

		DiscordSocketClient _discord;
		IConfigurationRoot _config;

		/// <summary>
		/// Makes a new instance of <see cref="BotChannel"/> class.
		/// </summary>
		public BotChannel(DiscordSocketClient discord, CommandService commands, IConfigurationRoot config)
		{
			_discord = discord;
			_config = config;

			_discord.GuildAvailable += _discord_GuildAvailable;
			
			Drawables = new ConcurrentBag<DrawableSection>();
		}

		#region Channel management
		
		/// <summary>
		/// Triggered on server connection.
		/// </summary>
		private async Task _discord_GuildAvailable(SocketGuild guild)
		{
			SocketTextChannel channel = null;

			// - Check if exists the bot channel
			var channels = guild.Channels.Where(x => x.Name == _config["channel:name"]);

			// - If channel not exists
			if (channels.Count() <= 0)
			{
				var c = await guild.CreateTextChannelAsync(_config["channel:name"]);
				channel = guild.GetChannel(c.Id) as SocketTextChannel;
			}
			else channel = channels.FirstOrDefault() as SocketTextChannel;

			// - Gets all channel messages
			var asyncMessages = await channel.GetMessagesAsync().Flatten();

			// - Assign a message for each drawable
			foreach (var drawable in Drawables)
			{
				// - Get the message for this drawable
				var message = asyncMessages.Where(x => x.Embeds.First().Author.Value.Name == drawable.Name).FirstOrDefault();

				// - If message not exists make it!
				if (message == null)
					message = await channel.SendMessageAsync("", false, drawable.Render());
				
				drawable.Messages[guild.Id] = message;
			}
		}

		#endregion

		#region Drawables management

		/// <summary>
		/// All registered drawables.
		/// </summary>
		protected ConcurrentBag<DrawableSection> Drawables { get; }

		/// <summary>
		/// Adds a new drawable to this module for rendering.
		/// </summary>
		/// <param name="dr">Drawable to add.</param>
		public void RegisterDrawable(DrawableSection dr)
		{
			Drawables.Add(dr);
		}

		#endregion

		#region Loop and logic

		/// <summary>
		/// True if this module is running.
		/// </summary>
		public bool Running { get; private set; }

		/// <summary>
		/// Start service loop.
		/// </summary>
		public async Task StartAsync()
		{
			Running = true;

			new Task(async () =>
			{

				while (Running)
				{
					try
					{
						foreach (var drawable in Drawables)
						{
							foreach (RestUserMessage message in drawable.Messages.Values)
							{
								await message.ModifyAsync((x) => x.Embed = drawable.Render());
							}
						}

						Thread.Sleep(int.Parse(_config["channel:refreshRate"]));
					}
					catch (Exception)
					{

					}
				}
			}).Start();

			await Task.CompletedTask;
		}

		/// <summary>
		/// Stops this module.
		/// </summary>
		public void Stop()
		{
			Running = false;
		}

		#endregion
	}

	public abstract class DrawableSection
	{
		/// <summary>
		/// Makes a new instance of <see cref="DrawableSection"/> class.
		/// </summary>
		/// <param name="name">Name for this drawable.</param>
		public DrawableSection(string name)
		{
			Name = name;
			Messages = new ConcurrentDictionary<ulong, IMessage>();
		}

		/// <summary>
		/// Unique name for all drawables.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// List of messages for all server.
		/// </summary>
		public ConcurrentDictionary<ulong, IMessage> Messages { get; }

		/// <summary>
		/// Called for rendering.
		/// </summary>
		public abstract Embed Render();

	}
}
