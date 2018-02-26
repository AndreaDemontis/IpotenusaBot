using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Ipotenusa.Modules
{
	[Name("Simone CM Recupero")]
	[Summary("Luca colico è una persona addicted da yuasa e\n" +
		"basta, non c'è altro da dire per il resto non ha caratteristiche particolari.")]
	public class ShymonModule : ModuleBase<SocketCommandContext>
	{

		private readonly VoiceService _voice;
		private readonly RecorderService _recorder;
		private readonly IConfigurationRoot _config;

		// Remember to add an instance of the AudioService
		// to your IServiceCollection when you initialize your bot
		public ShymonModule(VoiceService service, RecorderService recorder, IConfigurationRoot config)
		{
			_voice = service;
			_recorder = recorder;
			_config = config;
		}

		[Command("epico")]
		[Summary("Semplicemente epico")]
		public async Task LucaColico()
		{
			await Context.Message.DeleteAsync();
			var userVoice = Context.User as IVoiceState;
			await ReplyAsync($"NO");
			await _voice.JoinChannel(userVoice.VoiceChannel as SocketVoiceChannel);
			_voice.ReproduceWav(userVoice.VoiceChannel as SocketVoiceChannel, _config["sounds:folder"] + "\\epicoraga.wav");
			await ReplyAsync($"VABBÈ");
			await ReplyAsync($"EPICO");
		}

		[Command("trombetta")]
		[Summary("Semplicemente strombettaa")]
		public async Task Trombetta()
		{
			await Context.Message.DeleteAsync();
			var userVoice = Context.User as IVoiceState;
			await _voice.JoinChannel(userVoice.VoiceChannel as SocketVoiceChannel);
			_voice.ReproduceWav(userVoice.VoiceChannel as SocketVoiceChannel, _config["sounds:folder"] + "\\trombetta.wav");
		}

		[Command("papero")]
		[Summary("Kek.")]
		public async Task Papero()
		{
			await Context.Message.DeleteAsync();
			var userVoice = Context.User as IVoiceState;
			await _voice.JoinChannel(userVoice.VoiceChannel as SocketVoiceChannel);
			_voice.ReproduceWav(userVoice.VoiceChannel as SocketVoiceChannel, _config["sounds:folder"] + "\\quack.wav");
		}
	}
}
