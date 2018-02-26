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
	public class ParrotService
	{
		DiscordSocketClient _discord;
		IConfigurationRoot _config;
		VoiceService _voice;
		Random _rand;

		/// <summary>
		/// Makes a new instance of <see cref="RecorderService"/> class.
		/// </summary>
		public ParrotService(DiscordSocketClient discord, CommandService commands, VoiceService voice, IConfigurationRoot config, Random rand)
		{
			_discord = discord;
			_config = config;
			_voice = voice;
			_rand = rand;

			Sessions = new ConcurrentDictionary<ulong, ParrotSession>();
		}

		/// <summary>
		/// 
		/// </summary>
		ConcurrentDictionary<ulong, ParrotSession> Sessions { get; }

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

			while (Running)
			{
				foreach (var s in Sessions)
				{
					try
					{
						if (DateTime.Now - s.Value.LastTime > s.Value.Next)
						{
							var channel = _voice.Channels[s.Key];

							List<string> files = new List<string>();
							files.AddRange(Directory.GetFiles($"{_config["parrot:folder"]}\\", "*.wav"));
							files.AddRange(Directory.GetFiles($"{_config["parrot:favFolder"]}\\", "*.wav"));
							files = files.Where(i => i != s.Value.LastReproduced).ToList();

							var file = files[_rand.Next(files.Count)];
							s.Value.LastReproduced = file;

							s.Value.Next = TimeSpan.FromSeconds(_rand.Next(Int32.Parse(_config["parrot:minTime"]), Int32.Parse(_config["parrot:maxTime"])));
							s.Value.LastTime = DateTime.Now;

							_voice.ReproduceWav(channel.Channel, file);
						}
					}
					catch (Exception e) { }
				}
			}
		}

		/// <summary>
		/// Stops this module.
		/// </summary>
		public void Stop()
		{
			Running = false;
		}

		/// <summary>
		/// Reproduce a random recorded audio.
		/// </summary>
		public async Task ReproduceRandom(SocketVoiceChannel channel)
		{
			var files = Directory.GetFiles("Audio\\", "*.wav");
			_voice.ReproduceWav(channel, files[_rand.Next(files.Length)]);
		}

		public async Task SaveLast(SocketVoiceChannel channel)
		{
			if (!Sessions.ContainsKey(channel.Guild.Id))
				return;

			var fullpath = Sessions[channel.Guild.Id].LastReproduced;

			if (fullpath == null || fullpath == "")
				return;

			var filename = fullpath.Split('\\').Last();

			File.Copy(fullpath, _config["parrot:favFolder"] + "\\" + filename);

			await Task.CompletedTask;
		}

		public async Task StartParrot(SocketVoiceChannel channel)
		{
			Sessions.TryAdd(channel.Guild.Id, new ParrotSession
			{
				LastReproduced = "",
				LastTime = DateTime.Now,
				Next = TimeSpan.FromSeconds(_rand.Next(Int32.Parse(_config["parrot:minTime"]), Int32.Parse(_config["parrot:maxTime"])))
			});

			await Task.CompletedTask;
		}

		public async Task StopParrot(SocketVoiceChannel channel)
		{
			Sessions.TryRemove(channel.Guild.Id, out ParrotSession v);

			await Task.CompletedTask;
		}
	}

	public class ParrotSession
	{
		public string LastReproduced { get; set; }
		public DateTime LastTime { get; set; }
		public TimeSpan Next { get; set; }
	}
}
