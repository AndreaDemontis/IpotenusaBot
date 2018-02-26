using System;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;

using System.Net;
using System.IO;

namespace Ipotenusa.Modules
{
	public class AudioModule : ModuleBase<SocketCommandContext>
	{
		private readonly VoiceService _voice;
		private readonly RecorderService _recorder;

		// Remember to add an instance of the AudioService
		// to your IServiceCollection when you initialize your bot
		public AudioModule(VoiceService service, RecorderService recorder)
		{
			_voice = service;
			_recorder = recorder;
		}

		/// <summary>
		/// Join parrot bot module
		/// </summary>
		[Command("join", RunMode = RunMode.Async)]
		[Summary("Entra nel canale audio.")]
		public async Task Join()
		{
			await Context.Message.DeleteAsync();
			var userVoice = Context.User as IVoiceState;
			await _voice.JoinChannel(userVoice.VoiceChannel as SocketVoiceChannel);
		}

		/// <summary>
		/// Join parrot bot module
		/// </summary>
		[Command("leave", RunMode = RunMode.Async)]
		[Summary("Esce dal canale audio.")]
		public async Task Leave()
		{
			await Context.Message.DeleteAsync();
			var userVoice = Context.User as IVoiceState;
			await _voice.LeaveVoice(userVoice.VoiceChannel.Guild.Id);
		}

		/// <summary>
		/// Random sound
		/// </summary>
		[Command("randomSound", RunMode = RunMode.Async)]
		[Summary("Esce dalla vita.")]
		public async Task RandomSound()
		{
			var userVoice = Context.User as IVoiceState;

			var sound = GetRandomSound();

			if (sound != null)
			{
				await _voice.JoinChannel(userVoice.VoiceChannel as SocketVoiceChannel);
				_voice.ReproduceMp3(userVoice.VoiceChannel as SocketVoiceChannel, sound);
			}
		}

		#region Helpers

		Stream GetRandomSound()
		{
			try
			{
				HttpWebRequest request = WebRequest.Create("https://api.cleanvoice.ru/myinstants/?type=file") as HttpWebRequest;
				using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
				{
					if (response.StatusCode != HttpStatusCode.OK)
						return null;

					byte[] result = null;
					int bytCount = Convert.ToInt32(response.ContentLength);
					using (BinaryReader reader = new BinaryReader(response.GetResponseStream()))
					{
						result = reader.ReadBytes(bytCount);
					}

					return new MemoryStream(result);
				}
			}
			catch (Exception e)
			{
				return null;
			}
		}

		#endregion

	}
}
