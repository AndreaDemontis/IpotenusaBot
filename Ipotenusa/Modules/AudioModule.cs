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

using Ipotenusa.Services;

namespace Ipotenusa.Modules
{
	public class AudioModule : ModuleBase<SocketCommandContext>
	{
		private readonly ServerManager _servers;

		/// <summary>
		/// Initializes a new instance of <see cref="AudioModule"/> class.
		/// </summary>
		public AudioModule(ServerManager server)
		{
			_servers = server;
		}

		/// <summary>
		/// Join parrot bot module
		/// </summary>
		[Command("join", RunMode = RunMode.Async)]
		[Summary("Entra nel canale audio.")]
		public async Task Join()
		{
			await Context.Message.DeleteAsync();

			var server = _servers.Get(Context.Guild.Id);
			var channel = Context.User as IVoiceState;

			await server.AudioModule.JoinChannel(channel.VoiceChannel as SocketVoiceChannel);
		}

		/// <summary>
		/// Join parrot bot module
		/// </summary>
		[Command("leave", RunMode = RunMode.Async)]
		[Summary("Esce dal canale audio.")]
		public async Task Leave()
		{
			await Context.Message.DeleteAsync();

			var server = _servers.Get(Context.Guild.Id);

			var userVoice = Context.User as IVoiceState;
			await server.AudioModule.LeaveVoice();
		}

		/// <summary>
		/// Random sound
		/// </summary>
		[Command("randomSound", RunMode = RunMode.Async)]
		[Summary("Esce dalla vita.")]
		public async Task RandomSound()
		{
			var userVoice = Context.User as IVoiceState;

			var server = _servers.Get(Context.Guild.Id);

			var sound = GetRandomSound();

			if (sound != null)
			{
				await server.AudioModule.JoinChannel(userVoice.VoiceChannel as SocketVoiceChannel);
				server.AudioModule.ReproduceMp3(sound);
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
			catch (Exception)
			{
				return null;
			}
		}

		#endregion

	}
}
