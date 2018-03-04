using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Discord;
using Discord.WebSocket;

using Ipotenusa.Servers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ipotenusa.Services
{
    public class ServerManager
    {
		private readonly DiscordSocketClient _discord;
		private readonly IConfigurationRoot _config;
		private readonly Random _rand;

		/// <summary>
		/// Makes a new instance of <see cref="ServerManager"/> class.
		/// </summary>
		public ServerManager(DiscordSocketClient discord, IConfigurationRoot config, Random rand)
		{
			_discord = discord;
			_config = config;
			_rand = rand;

			ServerStore = new ConcurrentDictionary<ulong, DiscordServer>();

			_discord.GuildAvailable += GuildAvailable;
			_discord.GuildUnavailable += GuildUnavailable;
		}

		public ServiceProvider ServiceProvider { get; set; }

		private async Task GuildUnavailable(SocketGuild arg)
		{
			if (!ServerStore.TryRemove(arg.Id, out var server))
			{
				// - Unable to remove the server.
			}

			server.Dispose();

			await Task.CompletedTask;
		}

		private async Task GuildAvailable(SocketGuild arg)
		{
			var newServer = new DiscordServer(arg, _config, _rand, ServiceProvider);

			if (!ServerStore.TryAdd(arg.Id, newServer))
			{
				// - Unable to add the server.
			}

			await newServer.RunServer();
		}

		#region Server management

		/// <summary>
		/// All discord server store.
		/// </summary>
		private ConcurrentDictionary<ulong, DiscordServer> ServerStore { get; }

		/// <summary>
		/// All discord server connected.
		/// </summary>
		public IEnumerable<DiscordServer> Servers => ServerStore.Values;

		/// <summary>
		/// Gets the specified server.
		/// </summary>
		/// <param name="id">Server id.</param>
		public DiscordServer Get(ulong id)
		{
			if (!ServerStore.ContainsKey(id))
				return null;

			return ServerStore[id];
		}
		
		#endregion
	}
}
