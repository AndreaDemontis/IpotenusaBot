using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;

using Microsoft.Extensions.Configuration;

namespace Ipotenusa
{
	public class RecorderService
	{
		DiscordSocketClient _discord;
		IConfigurationRoot _config;
		VoiceService _voice;

		/// <summary>
		/// Makes a new instance of <see cref="RecorderService"/> class.
		/// </summary>
		public RecorderService(DiscordSocketClient discord, CommandService commands, VoiceService voice, IConfigurationRoot config)
		{
			_discord = discord;
			_config = config;
			_voice = voice;

			_voice.OnChannelConnection += voice_OnChannelConnection;

			SelectedServer = new List<ulong>();
			Recorders = new ConcurrentDictionary<ulong, Process>();
		}

		#region Cleanup

		/// <summary>
		/// True if this module is running.
		/// </summary>
		public bool Running { get; private set; }

		/// <summary>
		/// Start service loop.
		/// </summary>
		public async Task StartAsync()
		{
			Running = true;

			new Task(async () =>
			{

				while (Running)
				{
					try
					{
						await Task.Delay(Int32.Parse(_config["recorder:cleanupInterval"]) * 1000);

						var files = Directory.GetFiles(_config["recorder:folder"], "*.wav");

						var delFiles = files.Where(x =>
						{
							var info = new FileInfo(x);
							var lowTresh = info.Length < Int32.Parse(_config["recorder:cleanupLowTreshold"]);
							var highTresh = info.Length > Int32.Parse(_config["recorder:cleanupHighTreshold"]);
							var time = DateTime.Now - info.LastWriteTime > TimeSpan.FromMinutes(Int32.Parse(_config["recorder:historySize"]));
							return lowTresh || highTresh || time;
						});

						foreach (string f in delFiles)
							File.Delete(f);
					}
					catch (Exception e)
					{

					}
				}

			}).Start();

			await Task.CompletedTask;
		}

		/// <summary>
		/// Stops this module.
		/// </summary>
		public void Stop()
		{
			Running = false;
		}

		#endregion

		#region Register management

		/// <summary>
		/// Channels active for recording.
		/// </summary>
		public List<ulong> SelectedServer { get; }

		/// <summary>
		/// Current recording channels.
		/// </summary>
		public IEnumerable<AudioChannel> RecordingChannels => Recorders.Select(x => _voice.Channels[x.Key]);

		/// <summary>
		/// Start register the specified server.
		/// </summary>
		public async Task StartRegisterChannel(SocketVoiceChannel channel)
		{
			if (SelectedServer.Contains(channel.Guild.Id))
				return;

			SelectedServer.Add(channel.Guild.Id);

			if (_voice.Channels.ContainsKey(channel.Guild.Id))
			{
				_voice.Channels[channel.Guild.Id].OnUserStateChange += OnUserStateChange;
				_voice.Channels[channel.Guild.Id].OnAudioPacket += OnAudioPacket;
			}

			await Task.CompletedTask;
		}

		/// <summary>
		/// Stop register the specified server.
		/// </summary>
		public async Task StopRegisterChannel(SocketVoiceChannel channel)
		{
			if (!SelectedServer.Contains(channel.Guild.Id))
				return;

			SelectedServer.Remove(channel.Guild.Id);

			if (_voice.Channels.ContainsKey(channel.Guild.Id))
			{
				_voice.Channels[channel.Guild.Id].OnUserStateChange -= OnUserStateChange;
				_voice.Channels[channel.Guild.Id].OnAudioPacket -= OnAudioPacket;
			}

			await Task.CompletedTask;
		}

		private async Task voice_OnChannelConnection(object sender, ChannelConnectionEventArgs data)
		{
			if (data.Connected && SelectedServer.Contains(data.Channel.Channel.Guild.Id))
			{
				data.Channel.OnUserStateChange += OnUserStateChange;
				data.Channel.OnAudioPacket += OnAudioPacket;
			}
			else if (SelectedServer.Contains(data.Channel.Channel.Guild.Id))
			{
				data.Channel.OnUserStateChange -= OnUserStateChange;
				data.Channel.OnAudioPacket -= OnAudioPacket;
			}

			await Task.CompletedTask;
		}

		#endregion

		#region Audio logic

		/// <summary>
		/// Active recorders.
		/// </summary>
		public ConcurrentDictionary<ulong, Process> Recorders { get; }

		/// <summary>
		/// Gets a packet and write it in ffmpeg stream.
		/// </summary>
		private async Task OnAudioPacket(object sender, AudioStreamEventArgs data)
		{
			if (data.User == null || data.User.IsBot)
				return;

			if (!Recorders.TryGetValue(data.User.Id, out Process process) && process != null)
				return;
			
			try
			{
				// - For each packet we save it in ffmpeg stream
				for (int f = 0; f < data.Stream.AvailableFrames; f++)
				{
					// - Gets frame from stream
					var frame = await data.Stream.ReadFrameAsync(CancellationToken.None);
					
					// - Write packet on ffmpeg
					await process.StandardInput.BaseStream.WriteAsync(frame.Payload, 0, frame.Payload.Length);
				}

				// - Clear stream
				await process.StandardInput.BaseStream.FlushAsync();
			}
			catch (Exception e)
			{
				// - Packet loss
			}
			
		}

		/// <summary>
		/// Checks user state changes.
		/// </summary>
		private async Task OnUserStateChange(object sender, UserStateEventArgs data)
		{
			var user = _discord.GetUser(data.User.Id);

			if (user == null)
				return;

			if (user.IsBot && !Boolean.Parse(_config["parrot:recordBots"]))
				return;

			if (data.Talking)
			{
				string filepath = $"{_config["parrot:folder"]}/{user.Id}-{DateTime.Now.Ticks}.wav";

				var psi = new ProcessStartInfo
				{
					FileName = "ffmpeg.exe",
					Arguments = $"-ac 2 -f s16le -ar 48000 -i pipe:0 -ac 2 -ar 44100 \"{filepath}\"",
					UseShellExecute = false,
					RedirectStandardInput = true,
					RedirectStandardError = true
				};

				var process = Process.Start(psi);

				if (!Recorders.TryAdd(data.User.Id, process))
				{
					// - Errore
					return;
				}
				
				await Task.CompletedTask;
			}

			if (!data.Talking)
			{
				if (!Recorders.TryRemove(data.User.Id, out Process process))
				{
					// - Errore
					return;
				}

				await process.StandardInput.BaseStream.FlushAsync();
				process.StandardInput.Dispose();
				process.WaitForExit();
			}
		}

		#endregion
	}
}
