using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;

using System.Net;
namespace Ipotenusa.Modules
{
	[Group("cocorita")]
	[Name("Cocorita")]
	[Summary("Questo modulo gestisce un simpatico pappagallo\n" +
		"che vi terrà compagnia nei momenti più tristi della giornata.")]
	public class AudioParrotModule : ModuleBase<SocketCommandContext>
	{
		private readonly ParrotService _parrot;
		private readonly VoiceService _voice;
		private readonly RecorderService _recorder;

		// Remember to add an instance of the AudioService
		// to your IServiceCollection when you initialize your bot
		public AudioParrotModule(VoiceService service, RecorderService recorder, ParrotService parrot)
		{
			_parrot = parrot;
			_voice = service;
			_recorder = recorder;
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
			var userVoice = Context.User as IVoiceState;
			await _voice.JoinChannel(userVoice.VoiceChannel as SocketVoiceChannel);
			await _recorder.StartRegisterChannel(userVoice.VoiceChannel as SocketVoiceChannel);
		}


		[Command("stopListen", RunMode = RunMode.Async)]
		[Summary("Minkia...")]
		public async Task StopRecord()
		{
			await Context.Message.DeleteAsync();
			var userVoice = Context.User as IVoiceState;
			await _recorder.StopRegisterChannel(userVoice.VoiceChannel as SocketVoiceChannel);
		}

		[Command("saySomething", RunMode = RunMode.Async)]
		[Summary("Minkia...")]
		public async Task SaySomething()
		{
			await Context.Message.DeleteAsync();
			var userVoice = Context.User as IVoiceState;
			await _voice.JoinChannel(userVoice.VoiceChannel as SocketVoiceChannel);
			await _parrot.ReproduceRandom(userVoice.VoiceChannel as SocketVoiceChannel);
		}

		[Command("start", RunMode = RunMode.Async)]
		[Summary("Minkia...")]
		public async Task StartParrot()
		{
			await Context.Message.DeleteAsync();
			var userVoice = Context.User as IVoiceState;
			await _voice.JoinChannel(userVoice.VoiceChannel as SocketVoiceChannel);
			await _parrot.StartParrot(userVoice.VoiceChannel as SocketVoiceChannel);
		}

		[Command("stop", RunMode = RunMode.Async)]
		[Summary("Minkia...")]
		public async Task StopParrot()
		{
			await Context.Message.DeleteAsync();
			var userVoice = Context.User as IVoiceState;
			await _parrot.StopParrot(userVoice.VoiceChannel as SocketVoiceChannel);
		}

		[Command("s", RunMode = RunMode.Async)]
		[Summary("Salva ultima cosa")]
		public async Task Save()
		{
			await Context.Message.DeleteAsync();
			var userVoice = Context.User as IVoiceState;
			await _parrot.SaveLast(userVoice.VoiceChannel as SocketVoiceChannel);
		}
	}
}
