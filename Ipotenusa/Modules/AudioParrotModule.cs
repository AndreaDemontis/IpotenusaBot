using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Concurrent;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Ipotenusa.Services;

namespace Ipotenusa.Modules
{
	[Group("cocorita")]
	[Name("Cocorita")]
	[Summary("Questo modulo gestisce un simpatico pappagallo\n" +
		"che vi terrà compagnia nei momenti più tristi della giornata.")]
	public class AudioParrotModule : ModuleBase<SocketCommandContext>
	{
		private readonly ServerManager _servers;

		/// <summary>
		/// Initializes a new instance of <see cref="AudioModule"/> class.
		/// </summary>
		public AudioParrotModule(ServerManager server)
		{
			_servers = server;
		}

		/// <summary>
		/// Recorders dictionary.
		/// </summary>
		protected ConcurrentDictionary<ulong, Process> Recorders { get; }

		/// <summary>
		/// Join parrot bot module
		/// </summary>
		[Command("listen", RunMode = RunMode.Async)]
		[Summary("Vi ascolta eheh.")]
		public async Task StartRecord()
		{
			await Context.Message.DeleteAsync();

			var server = _servers.Get(Context.Guild.Id);
			var channel = Context.User as IVoiceState;

			await server.AudioModule.JoinChannel(channel.VoiceChannel as SocketVoiceChannel);
			await server.CocoritaModule.StartRecording();
		}


		[Command("stopListen", RunMode = RunMode.Async)]
		[Summary("Minkia...")]
		public async Task StopRecord()
		{
			await Context.Message.DeleteAsync();

			var server = _servers.Get(Context.Guild.Id);
			var channel = Context.User as IVoiceState;

			await server.CocoritaModule.StopRecording();
		}

		[Command("saySomething", RunMode = RunMode.Async)]
		[Summary("Minkia...")]
		public async Task SaySomething()
		{
			await Context.Message.DeleteAsync();

			var server = _servers.Get(Context.Guild.Id);
			var channel = Context.User as IVoiceState;

			await server.AudioModule.JoinChannel(channel.VoiceChannel as SocketVoiceChannel);
			await server.CocoritaModule.ReproduceRandom();
		}

		[Command("start", RunMode = RunMode.Async)]
		[Summary("Minkia...")]
		public async Task StartParrot()
		{
			await Context.Message.DeleteAsync();

			var server = _servers.Get(Context.Guild.Id);
			var channel = Context.User as IVoiceState;

			await server.AudioModule.JoinChannel(channel.VoiceChannel as SocketVoiceChannel);
			await server.CocoritaModule.StartSpeaking();
		}

		[Command("stop", RunMode = RunMode.Async)]
		[Summary("Minkia...")]
		public async Task StopParrot()
		{
			await Context.Message.DeleteAsync();

			var server = _servers.Get(Context.Guild.Id);
			var channel = Context.User as IVoiceState;

			await server.AudioModule.JoinChannel(channel.VoiceChannel as SocketVoiceChannel);
			await server.CocoritaModule.StopSpeaking();
		}

		[Command("s", RunMode = RunMode.Async)]
		[Summary("Salva ultima cosa")]
		public async Task Save()
		{
			await Context.Message.DeleteAsync();

			var server = _servers.Get(Context.Guild.Id);
			var channel = Context.User as IVoiceState;
			
			await server.CocoritaModule.SaveLast();
		}
	}
}
