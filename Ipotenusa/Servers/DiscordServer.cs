using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Discord.WebSocket;

using Ipotenusa.Services;
using Ipotenusa.Servers.Audio;
using Ipotenusa.Servers.Channels;

namespace Ipotenusa.Servers
{
	public class DiscordServer : IDisposable
	{
		/// <summary>
		/// Makes a new instance of <see cref="DiscordServer"/> class.
		/// </summary>
		public DiscordServer(SocketGuild handler, IConfigurationRoot config, Random rand, ServiceProvider services)
		{
			DiscordHandler = handler;
			Configuration = config;
			Rand = rand;
			Services = services;

			DiscordClient = Services.GetService<DiscordSocketClient>();
			var youtube = Services.GetService<Youtube>();

			BotChannel = new BotChannel(this);
			AudioModule = new VoiceChannel(this);
			CocoritaModule = new Cocorita(this);
			Player = new MusicPlayer(this, youtube);
		}

		/// <summary>
		/// Bot configuration.
		/// </summary>
		public IConfigurationRoot Configuration { get; }

		/// <summary>
		/// Reference to discord server object.
		/// </summary>
		public SocketGuild DiscordHandler { get; }

		/// <summary>
		/// Discord server playback module.
		/// </summary>
		public VoiceChannel AudioModule { get; }

		/// <summary>
		/// Discord server playback module.
		/// </summary>
		public Cocorita CocoritaModule { get; }

		/// <summary>
		/// Manages bot server channel.
		/// </summary>
		public BotChannel BotChannel { get; }

		/// <summary>
		/// Random number generator.
		/// </summary>
		public Random Rand { get; }

		/// <summary>
		/// Public bot services.
		/// </summary>
		public ServiceProvider Services { get; }

		/// <summary>
		/// Public music bot.
		/// </summary>
		public MusicPlayer Player { get; }

		/// <summary>
		/// Discord socket.
		/// </summary>
		public DiscordSocketClient DiscordClient { get; }

		/// <summary>
		/// Initialize and run server modules.
		/// </summary>
		public async Task RunServer()
		{
			var diagnostic = Services.GetService<Diagnostic>();
			var logs = Services.GetService<LoggingService>();

			BotChannel.RegisterDrawable(0, diagnostic.DiagnosticDrawable);
			BotChannel.RegisterDrawable(1, CocoritaModule.Drawable);
			BotChannel.RegisterDrawable(2, Player.Drawer);
			BotChannel.RegisterDrawable(3, logs.LogDrawable);

			new Task(async () => await BotChannel.StartAsync()).Start();
			new Task(async () => await AudioModule.StartAsync()).Start();
			new Task(async () => await CocoritaModule.StartAsync()).Start();

			await Task.CompletedTask;
		}

		/// <summary>
		/// Stop server and release resources.
		/// </summary>
		public void Dispose()
		{
			// - Dispose all modules
			CocoritaModule.Dispose();
			AudioModule.Dispose();
			BotChannel.Dispose();
		}
	}
}
