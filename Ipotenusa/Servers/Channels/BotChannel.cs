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

namespace Ipotenusa.Servers.Channels
{
	public class BotChannel : IDisposable
	{
		private readonly DiscordServer _server;
		private readonly IConfigurationRoot _config;

		/// <summary>
		/// Makes a new instance of <see cref="BotChannel"/> class.
		/// </summary>
		public BotChannel(DiscordServer server)
		{
			_server = server;
			_config = server.Configuration;
			
			Drawables = new ConcurrentDictionary<int, DrawableSection>();
		}

		/// <summary>
		/// True if this module is running.
		/// </summary>
		public bool Running { get; private set; }

		/// <summary>
		/// Stop server and release resources.
		/// </summary>
		public void Dispose()
		{
			Running = false;
		}

		/// <summary>
		/// Start this module.
		/// </summary>
		public async Task StartAsync()
		{
			await InitializeChannel();

			Running = true;

			new Task(async () => await StartAsyncUpdate()).Start();

			await Task.CompletedTask;
		}

		#region Channel management

		/// <summary>
		/// Start this module.
		/// </summary>
		private async Task InitializeChannel()
		{
			SocketTextChannel channel = null;
			var guild = _server.DiscordHandler;

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
			foreach (var drawable in Drawables.OrderBy(x => x.Key).Select(x => x.Value))
			{
				// - Get the message for this drawable
				var messages = asyncMessages.Where(x => x.Embeds.First().Author.Value.Name == drawable.Name);

				var message = messages.Any() ? messages.First() : null;

				// - If message not exists make it!
				if (message == null)
					message = await channel.SendMessageAsync("", false, drawable.Render());
				
				drawable.Message = message as RestUserMessage;

				await drawable.Update();
			}
		}

		#endregion

		#region Drawables management

		/// <summary>
		/// All registered drawables.
		/// </summary>
		protected ConcurrentDictionary<int, DrawableSection> Drawables { get; }

		/// <summary>
		/// Adds a new drawable to this module for rendering.
		/// </summary>
		/// <param name="dr">Drawable to add.</param>
		public void RegisterDrawable(int index, DrawableSection dr)
		{
			Drawables.TryAdd(index, dr);
		}

		#endregion

		#region Loop and logic

		/// <summary>
		/// Start service loop.
		/// </summary>
		private async Task StartAsyncUpdate()
		{
			while (Running)
			{
				try
				{
					foreach (var drawable in Drawables.Values)
					{
						//await drawable.Update();
					}

					Thread.Sleep(int.Parse(_config["channel:refreshRate"]));
				}
				catch (Exception e)
				{

				}
			}

			await Task.CompletedTask;
		}

		#endregion
	}

	public abstract class DrawableSection
	{
		/// <summary>
		/// Makes a new instance of <see cref="DrawableSection"/> class.
		/// </summary>
		/// <param name="name">Name for this drawable.</param>
		public DrawableSection(string name, DiscordSocketClient discord)
		{
			Name = name;
			Buttons = new List<string>();

			discord.ReactionAdded += ReactionAdded;
			discord.ReactionRemoved += ReactionRemoved;
			discord.ReactionsCleared += ReactionsCleared;
		}

		/// <summary>
		/// Triggered on button press.
		/// </summary>
		public event AsyncEventHandler<ButtonPressEventArgs> ButtonPressed;

		/// <summary>
		/// Unique name for all drawables.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// List of messages for all server.
		/// </summary>
		public RestUserMessage Message { get; set; }

		/// <summary>
		/// List of message buttons.
		/// </summary>
		public List<string> Buttons { get; }

		/// <summary>
		/// Called for rendering.
		/// </summary>
		public abstract Embed Render();

		/// <summary>
		/// Render and update this object.
		/// </summary>
		public async Task Update()
		{
			if (Message == null)
				return;

			if (Message.Reactions.Count != Buttons.Count)
			{
				await Message.RemoveAllReactionsAsync();
				foreach (var btn in Buttons)
				{
					await Message.AddReactionAsync(new Emoji(btn));
				}
			}

			await Message.ModifyAsync((x) => x.Embed = Render());
		}

		private async Task ReactionsCleared(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2)
		{
			await Task.CompletedTask;
		}

		private async Task ReactionRemoved(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
		{
			var msg = await arg1.DownloadAsync();

			if (Message == null || msg.Id != Message.Id)
				return;

			if (arg3.User.Value.Id != msg.Author.Id)
			{
				if (ButtonPressed != null)
					await ButtonPressed(this, new ButtonPressEventArgs(arg3.User.Value, arg3.Emote.Name));
			}
		}

		private async Task ReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
		{
			var msg = await arg1.DownloadAsync();

			if (Message == null || msg.Id != Message.Id)
				return;

			if (!Buttons.Where(x => new Emoji(x).Name != arg3.Emote.Name).Any())
			{
				await msg.RemoveReactionAsync(arg3.Emote, arg3.User.Value);
			}

			if (arg3.User.Value.Id != msg.Author.Id)
			{
				if (ButtonPressed != null)
					await ButtonPressed(this, new ButtonPressEventArgs(arg3.User.Value, arg3.Emote.Name));
			}
		}

		public class ButtonPressEventArgs : EventArgs
		{

			public ButtonPressEventArgs(IUser user, string button)
			{
				User = user;
				Button = button;
			}

			/// <summary>
			/// User who pressed this button.
			/// </summary>
			public IUser User { get; }

			/// <summary>
			/// Button emoji.
			/// </summary>
			public string Button { get; }

		}
	}
}
