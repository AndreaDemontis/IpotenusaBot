using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ipotenusa.Modules
{
	[Name("Baldo")]
	[Summary("Questo modulo gestisce un simpatico pappagallo\n" +
		"che vi terrà compagnia nei momenti più tristi della giornata.")]
	public class BaldoModule : ModuleBase<SocketCommandContext>
	{
		private readonly VoiceService _voice;
		private readonly RecorderService _recorder;
		private readonly IConfigurationRoot _config;

		// Remember to add an instance of the AudioService
		// to your IServiceCollection when you initialize your bot
		public BaldoModule(VoiceService service, RecorderService recorder, IConfigurationRoot config)
		{
			_voice = service;
			_recorder = recorder;
			_config = config;
		}


		
	}
}
