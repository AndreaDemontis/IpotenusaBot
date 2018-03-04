using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Microsoft.Extensions.Configuration;

using Ipotenusa.Services;

namespace Ipotenusa.Modules
{
	[Name("Audio player")]
	[Summary("Tanta musica, mettete musica e lui è felice.")]
	public class PlayerModule : ModuleBase<SocketCommandContext>
	{
		private readonly ServerManager _servers;
		private readonly Random _rand;
		private readonly IConfigurationRoot _config;

		// Remember to add an instance of the AudioService
		// to your IServiceCollection when you initialize your bot
		public PlayerModule(ServerManager servers, Random rand, IConfigurationRoot config)
		{
			_servers = servers;
			_rand = rand;
			_config = config;
		}

		[Command("play")]
		[Summary("L'arancia è fatta.")]
		public async Task Play(string link)
		{
			await Context.Message.DeleteAsync();

			var server = _servers.Get(Context.Guild.Id);
			var channel = Context.User as IVoiceState;

			await server.AudioModule.JoinChannel(channel.VoiceChannel as SocketVoiceChannel);
			await server.Player.PlayUrl(Context.User as SocketGuildUser, link);
		}

		[Command("stop")]
		[Summary("L'arancia è fatta.")]
		public async Task Stop()
		{
			await Context.Message.DeleteAsync();

			var server = _servers.Get(Context.Guild.Id);
			var channel = Context.User as IVoiceState;

			await server.Player.Stop(Context.User as SocketGuildUser);
		}

		[Command("skip")]
		[Summary("L'arancia è fatta.")]
		public async Task Skip(int n)
		{
			await Context.Message.DeleteAsync();

			var server = _servers.Get(Context.Guild.Id);
			var channel = Context.User as IVoiceState;

			await server.Player.Skip(Context.User as SocketGuildUser, n);
		}

		[Command("skip")]
		[Summary("L'arancia è fatta.")]
		public async Task Skip()
		{
			await Context.Message.DeleteAsync();

			var server = _servers.Get(Context.Guild.Id);
			var channel = Context.User as IVoiceState;

			await server.Player.Skip(Context.User as SocketGuildUser, 1);
		}

		[Command("pause")]
		[Summary("L'arancia è fatta.")]
		public async Task Pause()
		{
			await Context.Message.DeleteAsync();

			var server = _servers.Get(Context.Guild.Id);
			var channel = Context.User as IVoiceState;

			await server.Player.Pause(Context.User as SocketGuildUser);
		}

		[Command("resume")]
		[Summary("L'arancia è fatta.")]
		public async Task Resume()
		{
			await Context.Message.DeleteAsync();

			var server = _servers.Get(Context.Guild.Id);
			var channel = Context.User as IVoiceState;

			await server.Player.Resume(Context.User as SocketGuildUser);
		}

		[Command("current")]
		[Summary("L'arancia è fatta.")]
		public async Task Current()
		{
			await Context.Message.DeleteAsync();

			var server = _servers.Get(Context.Guild.Id);
			var channel = Context.User as IVoiceState;

			if (server.Player.Queue.Count() <= 0)
			{
				await ReplyAsync("Empty queue.");
				return;
			}

			var current = server.Player.Queue.First();

			var builder = new EmbedBuilder()
			{
				Color = new Color(114, 137, 218),
				Title = current.Title,
				ThumbnailUrl = current.ImageUrl
			};

			builder.WithAuthor(current.User);

			builder.AddInlineField("Song duration", current.TotalTime.ToString());

			await ReplyAsync("", false, builder.Build());
		}
	}
}
