using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Discord;
using Discord.Rest;
using Discord.WebSocket;

using NAudio.Wave;

using Microsoft.Extensions.Configuration;

using Ipotenusa.Servers.Channels;

namespace Ipotenusa.Servers.Audio
{
	public class Cocorita : IDisposable
	{
		private readonly IConfigurationRoot _config;
		private readonly DiscordServer _server;
		private readonly VoiceChannel _voice;
		private readonly Random _rand;

		/// <summary>
		/// Makes a new instance of <see cref="Cocorita"/> class.
		/// </summary>
		public Cocorita(DiscordServer server)
		{
			_server = server;
			_voice = _server.AudioModule;
			_config = _server.Configuration;
			_rand = _server.Rand;

			_voice.UserStateChange += UserStateChange;
			_voice.PcmReceived += PcmReceived;

			Drawable = new CocoritaDrawable(server.DiscordClient, this)
			{
				Avatar = _config["parrot:avatar"]
			};

			Recorders = new ConcurrentDictionary<ulong, MemoryStream>();
		}

		/// <summary>
		/// Drawable.
		/// </summary>
		public CocoritaDrawable Drawable { get; }

		/// <summary>
		/// True if this module is running.
		/// </summary>
		public bool Running { get; private set; }

		/// <summary>
		/// Stop server and release resources.
		/// </summary>
		public void Dispose()
		{
			Running = false;
		}

		/// <summary>
		/// Start this module.
		/// </summary>
		public async Task StartAsync()
		{
			Running = true;

			new Task(async () => await StartAsyncCleanup()).Start();
			new Task(async () => await StartAsyncCocorita()).Start();

			await Task.CompletedTask;
		}

		#region Management

		/// <summary>
		/// If true this bot is recording.
		/// </summary>
		public bool Recording { get; private set; }

		/// <summary>
		/// If true this bot can speak.
		/// </summary>
		public bool Speaking { get; private set; }

		/// <summary>
		/// Start register.
		/// </summary>
		public async Task StartRecording()
		{
			Recording = true;

			Drawable.Recording = true;

			await Drawable.Update();
		}

		/// <summary>
		/// Stop register.
		/// </summary>
		public async Task StopRecording()
		{
			Recording = false;

			Drawable.Recording = false;

			await Drawable.Update();
		}

		/// <summary>
		/// Start speaking.
		/// </summary>
		public async Task StartSpeaking()
		{
			Speaking = true;

			Drawable.Speaking = true;

			LastReproduced = "";
			LastTime = DateTime.Now;
			Next = TimeSpan.FromSeconds(_rand.Next(Int32.Parse(_config["parrot:minTime"]), Int32.Parse(_config["parrot:maxTime"])));

			await Drawable.Update();
		}

		/// <summary>
		/// Stop speaking.
		/// </summary>
		public async Task StopSpeaking()
		{
			Speaking = false;

			Drawable.Speaking = false;

			await Drawable.Update();
		}

		#endregion

		#region User state

		/// <summary>
		/// Active recorders.
		/// </summary>
		public ConcurrentDictionary<ulong, MemoryStream> Recorders { get; }

		/// <summary>
		/// Checks user state changes.
		/// </summary>
		private async Task UserStateChange(object sender, UserEventArgs data)
		{
			var user = data.UserState.User;
			var talking = data.UserState.Talking;

			if (user == null || user.IsBot)
				return;

			if (!Recording)
				return;

			try
			{
				if (talking)
				{
					if (!Recorders.TryAdd(user.Id, new MemoryStream()))
					{
						// - Errore
						return;
					}
				}

				if (!talking)
				{
					if (!Recorders.TryRemove(user.Id, out MemoryStream process))
					{
						// - Errore
						return;
					}

					string filepath = $"{_config["parrot:folder"]}/{_server.DiscordHandler.Id}/{user.Id}-{DateTime.Now.Ticks}.wav";

					if (!Directory.Exists($"{_config["parrot:folder"]}/{_server.DiscordHandler.Id}"))
					{
						Directory.CreateDirectory($"{_config["parrot:folder"]}/{_server.DiscordHandler.Id}");
					}

					process.Position = 0;

					using (var rec = new RawSourceWaveStream(process, AudioHelpers.PcmFormat))
					{
						using (var convertedStream = WaveFormatConversionStream.CreatePcmStream(rec))
							WaveFileWriter.CreateWaveFile(filepath, convertedStream);
					}

					process.Flush();
					process.Dispose();
					process = null;
				}

				await Task.CompletedTask;
			}
			catch (Exception)
			{
				// - Ignore all exception
			}
		}

		#endregion

		#region Pcm management

		/// <summary>
		/// Gets a packet and write it in ffmpeg stream.
		/// </summary>
		private async Task PcmReceived(object sender, AudioPcmEventArgs data)
		{
			if (data.User == null || data.User.IsBot)
				return;

			if (!Recorders.TryGetValue(data.User.Id, out MemoryStream process) || process == null)
				return;

			try
			{
				// - For each packet we save it in ffmpeg stream
				for (int f = 0; f < data.Stream.AvailableFrames; f++)
				{
					// - Gets frame from stream
					var frame = await data.Stream.ReadFrameAsync(CancellationToken.None);

					// - Write packet on ffmpeg
					process.Write(frame.Payload, 0, frame.Payload.Length);
				}
			}
			catch (Exception)
			{
				// - Packet loss
			}
		}

		#endregion

		#region Files cleanup

		/// <summary>
		/// Start service loop.
		/// </summary>
		private async Task StartAsyncCleanup()
		{
			while (Running)
			{
				try
				{
					Thread.Sleep(Int32.Parse(_config["recorder:cleanupInterval"]) * 1000);

					var files = Directory.GetFiles($"{_config["recorder:folder"]}/{_server.DiscordHandler.Id}", "*.wav");

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

					if (delFiles.Count() > 0)
						await Drawable.Update();
				}
				catch (Exception)
				{

				}
			}

			await Task.CompletedTask;
		}

		#endregion

		#region Reproduction

		/// <summary>
		/// Last file reproduced.
		/// </summary>
		public string LastReproduced { get; private set; }

		/// <summary>
		/// Last reproduction time.
		/// </summary>
		public DateTime LastTime { get; private set; }

		/// <summary>
		/// Next timespan.
		/// </summary>
		public TimeSpan Next { get; private set; }

		/// <summary>
		/// Start cocorita loop.
		/// </summary>
		public async Task StartAsyncCocorita()
		{
			while (Running)
			{
				try
				{
					if (DateTime.Now - LastTime > Next && Speaking)
					{
						List<string> files = new List<string>();
						files.AddRange(Directory.GetFiles($"{_config["parrot:folder"]}\\{_server.DiscordHandler.Id}\\", "*.wav"));
						files.AddRange(Directory.GetFiles($"{_config["parrot:favFolder"]}\\", "*.wav"));
						files = files.Where(i => i != LastReproduced).ToList();

						var file = files[_rand.Next(files.Count)];
						LastReproduced = file;

						Next = TimeSpan.FromSeconds(_rand.Next(Int32.Parse(_config["parrot:minTime"]), Int32.Parse(_config["parrot:maxTime"])));
						LastTime = DateTime.Now;

						_voice.ReproduceWav(file);

						await Drawable.Update();
					}
				}
				catch (Exception) { }

				Thread.Sleep(100);
			}

			await Task.CompletedTask;
		}

		/// <summary>
		/// Reproduce a random recorded audio.
		/// </summary>
		public async Task ReproduceRandom()
		{
			try
			{
				var files = Directory.GetFiles("Audio\\", "*.wav");
				_voice.ReproduceWav(files[_rand.Next(files.Length)]);

				await Drawable.Update();
			}
			catch (Exception)
			{

			}
			await Task.CompletedTask;
		}

		/// <summary>
		/// Saves last reproduced file.
		/// </summary>
		public async Task SaveLast()
		{
			if (LastReproduced == null || LastReproduced == "")
				return;

			var filename = LastReproduced.Split('\\').Last();

			File.Copy(LastReproduced, _config["parrot:favFolder"] + "\\" + filename);

			await Drawable.Update();
		}
		
		#endregion
	}

	public class CocoritaDrawable : DrawableSection
	{
		private readonly Cocorita _module;

		/// <summary>
		/// Makes a new instance of <see cref="LogDrawable"/> class.
		/// </summary>
		public CocoritaDrawable(DiscordSocketClient discord, Cocorita module)
			: base("Cocorita", discord)
		{
			Buttons.Add(Utils.EmojiUnicodes.Record);
			Buttons.Add(Utils.EmojiUnicodes.PlayPause);

			_module = module;

			ButtonPressed += ButtonPressedEvent;
		}

		/// <summary>
		/// Logger avatar.
		/// </summary>
		public string Avatar { get; set; }

		/// <summary>
		/// If cocorita is listening.
		/// </summary>
		public bool Recording { get; set; }

		/// <summary>
		/// If cocorita is speaking.
		/// </summary>
		public bool Speaking { get; set; }

		public override Embed Render()
		{
			var builder = new EmbedBuilder()
			{
				Color = Color.Blue,
				Description = "ahhhhh Fate merda... Non l'ho detto io, ti vedrei bene a giocare a league of legends."
			};

			builder.WithAuthor(Name, Avatar);

			builder.AddInlineField("Input state", Recording ? "Recording" : "Stop");
			builder.AddInlineField("Output state", Speaking ? "Speaking" : "Mute");

			return builder.Build();
		}

		private async Task ButtonPressedEvent(object sender, ButtonPressEventArgs data)
		{
			switch (data.Button)
			{
				case Utils.EmojiUnicodes.Record:
					if (!Recording)
						await _module.StartRecording();
					else await _module.StopRecording();
					break;
				case Utils.EmojiUnicodes.PlayPause:
					if (!Speaking)
						await _module.StartSpeaking();
					else await _module.StopSpeaking();
					break;
			}
		}
	}
}
