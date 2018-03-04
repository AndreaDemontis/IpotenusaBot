using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Discord;
using Discord.Rest;
using Discord.WebSocket;

using Ipotenusa.Servers.Channels;
using Ipotenusa.Services;
using Ipotenusa.Utils;

namespace Ipotenusa.Servers.Audio
{
	public class MusicPlayer : IAudioSource
	{
		private readonly DiscordServer _server;
		private readonly VoiceChannel _audio;
		private readonly Youtube _youtube;

		private double volume = 100;
		private double gain = 0;

		/// <summary>
		/// Makes a new instance of <see cref="MusicPlayer"/> class.
		/// </summary>
		public MusicPlayer(DiscordServer server, Youtube youtube)
		{
			_server = server;
			_audio = server.AudioModule;
			_youtube = youtube;

			ReproductionQueue = new ConcurrentQueue<SongReproductionState>();

			Drawer = new PlayerDrawable(this, server.DiscordClient)
			{
				Avatar = server.Configuration["player:avatar"]
			};
		}

		public PlayerDrawable Drawer { get; }

		/// <summary>
		/// Gets or sets the volume.
		/// </summary>
		public double Volume
		{
			get => volume;
			set
			{
				volume = Math.Max(Math.Min(value, 100), 0);
				Drawer.Update().Start();
			}
		}

		/// <summary>
		/// Source gain.
		/// </summary>
		public double Gain { get; set; }

		#region Reproductuon queue management

		/// <summary>
		/// Reproduction queue.
		/// </summary>
		protected ConcurrentQueue<SongReproductionState> ReproductionQueue { get; }

		/// <summary>
		/// Reproduction queue immutable.
		/// </summary>
		public IEnumerable<SongReproductionState> Queue => ReproductionQueue;

		/// <summary>
		/// Gets the current song in the queue.
		/// </summary>
		public SongReproductionState CurrentSong => ReproductionQueue.Count > 0 ? ReproductionQueue.First() : null;

		/// <summary>
		/// Reproduce an audio from an url.
		/// </summary>
		/// <param name="url">Audio/video to reproduce.</param>
		public async Task PlayUrl(SocketGuildUser user, string url)
		{
			var ytObject = await _youtube.ParseLink(url);

			if (ytObject == null)
				return;

			if (ytObject is Youtube.Playlist)
			{
				var playlist = ytObject as Youtube.Playlist;

				foreach(var video in playlist.Videos)
				{
					EnqueueMedia(user, new YoutubeReproductionState(this, _audio, user, video));
				}
			}

			if (ytObject is Youtube.Video)
			{
				var video = ytObject as Youtube.Video;

				EnqueueMedia(user, new YoutubeReproductionState(this, _audio, user, video));
			}

			await Drawer.Update();
		}

		private void EnqueueMedia(SocketGuildUser user, SongReproductionState media)
		{
			media.ReproductionEnded +=
				async (obj, e) => await Skip(user, 1);

			ReproductionQueue.Enqueue(media);

			if (ReproductionQueue.Count <= 1)
				media.Play();
		}

		/// <summary>
		/// Skip the specified number of songs in the queue.
		/// </summary>
		/// <param name="n"></param>
		public async Task Skip(SocketGuildUser user, int n)
		{
			ReproductionQueue.TryPeek(out SongReproductionState current);
			current.Discard();

			for (int i = n; i > 0; --i)
				ReproductionQueue.TryDequeue(out SongReproductionState c);

			if (ReproductionQueue.TryPeek(out SongReproductionState newSong))
			{
				newSong.Play();
			}

			await Drawer.Update();
		}

		/// <summary>
		/// Stop and clear the queue.
		/// </summary>
		public async Task Stop(SocketGuildUser user)
		{
			if (CurrentSong != null)
				CurrentSong.Discard();
			
			while (ReproductionQueue.Count > 0)
				ReproductionQueue.TryDequeue(out SongReproductionState c);
			
			await Drawer.Update();
		}

		/// <summary>
		/// Stop and clear the queue.
		/// </summary>
		public async Task Pause(SocketGuildUser user)
		{
			if (ReproductionQueue.TryPeek(out SongReproductionState curr))
			{
				curr.Pause();
			}

			await Drawer.Update();
		}

		/// <summary>
		/// Stop and clear the queue.
		/// </summary>
		public async Task Resume(SocketGuildUser user)
		{
			if (ReproductionQueue.TryPeek(out SongReproductionState curr))
			{
				curr.Resume();
			}

			await Drawer.Update();
		}

		#endregion
	}

	public class PlayerDrawable : DrawableSection
	{
		private readonly MusicPlayer _module;

		/// <summary>
		/// Makes a new instance of <see cref="PlayerDrawable"/> class.
		/// </summary>
		public PlayerDrawable(MusicPlayer module, DiscordSocketClient discord)
			: base("Puraier chan", discord)
		{
			_module = module;

			Buttons.Add(EmojiUnicodes.LessLoud);
			Buttons.Add(EmojiUnicodes.Loud);
			Buttons.Add(EmojiUnicodes.PlayPause);
			Buttons.Add(EmojiUnicodes.Stop);
			//Buttons.Add(EmojiUnicodes.Repeat);
			Buttons.Add(EmojiUnicodes.Next);

			ButtonPressed += ButtonPressedEvent;
		}
		

		/// <summary>
		/// Logger avatar.
		/// </summary>
		public string Avatar { get; set; }

		/// <summary>
		/// Current playing song.
		/// </summary>
		public SongReproductionState CurrentSong => _module.CurrentSong;

		private async Task ButtonPressedEvent(object sender, ButtonPressEventArgs data)
		{
			switch (data.Button)
			{
				case EmojiUnicodes.PlayPause:
					if (_module.CurrentSong != null)
					{
						if (_module.CurrentSong.State == ReproductionState.Playing)
							await _module.Pause(data.User as SocketGuildUser);
						else if (_module.CurrentSong.State == ReproductionState.Paused)
							await _module.Resume(data.User as SocketGuildUser);
					}
					break;
				case EmojiUnicodes.Stop:
					await _module.Stop(data.User as SocketGuildUser);
					break;
				case EmojiUnicodes.Next:
					await _module.Skip(data.User as SocketGuildUser, 1);
					break;
				case EmojiUnicodes.LessLoud:
					_module.Volume -= 10;
					break;
				case EmojiUnicodes.Loud:
					_module.Volume += 10;
					break;
			}

			await Task.CompletedTask;
		}


		public override Embed Render()
		{
			var builder = new EmbedBuilder()
			{
				Color = Color.Gold,
				ThumbnailUrl = CurrentSong?.ImageUrl ?? Avatar,
				Description = "D-dammi m-musiche da r-riprodurre onii-chan <3"
			};

			builder.WithAuthor(Name, Avatar);

			// - Metadata
			string description = CurrentSong?.Description;
			description = description == null || description == "" ? "No description" : description;
			description = string.Join("\n", description.Split('\n').Take(3));
			builder.AddField(CurrentSong?.Title ?? "Currently reproducing nothing.", description);

			// - Reproduction informations
			builder.AddInlineField("Reproduction state", CurrentSong?.State.ToString() ?? "No song.");
			builder.AddInlineField("Song length", CurrentSong?.TotalTime.ToString() ?? "00:00");
			builder.AddInlineField("Volume", $"{_module.Volume}/100");

			// - Queue string builder
			string queue = ""; int i = 0;
			foreach (var song in _module.Queue.Take(10))
			{
				queue += $"{i++} - {Utils.Formatting.Truncate(song.Title, 40)}\n";
			}

			queue = queue == string.Empty ? "\nEmpty queue.\n" : queue;
			builder.AddField("Queue", Format.Code(queue));

			return builder.Build();
		}

	}

	public class SongReproductionState
	{
		protected VoiceChannel _service;

		/// <summary>
		/// Makes a new instance of <see cref="SongReproductionState"/> class.
		/// </summary>
		public SongReproductionState(IAudioSource source, VoiceChannel service, SocketGuildUser user)
		{
			_service = service;
			User = user;
			Source = source;
		}

		/// <summary>
		/// Reproduction handler.
		/// </summary>
		public AsyncReproductionSession ReproductionSession { get; private set; }

		/// <summary>
		/// Audio source.
		/// </summary>
		public IAudioSource Source { get; }

		/// <summary>
		/// Song title.
		/// </summary>
		public virtual string Title { get; }

		/// <summary>
		/// Preview image url.
		/// </summary>
		public virtual string ImageUrl { get; }

		/// <summary>
		/// Video description.
		/// </summary>
		public virtual string Description { get; }

		/// <summary>
		/// 
		/// </summary>
		public SocketGuildUser User { get; }

		/// <summary>
		/// Current reproduction time.
		/// </summary>
		public virtual TimeSpan ReproductionTime => TimeSpan.FromSeconds((double)ReproductionSession.Position / AudioHelpers.BYTES_PER_SECOND);

		/// <summary>
		/// Total audio buffer time.
		/// </summary>
		public virtual TimeSpan TotalTime { get; }

		/// <summary>
		/// Gets the reproduction state.
		/// </summary>
		public ReproductionState State => ReproductionSession?.State ?? ReproductionState.Closed;

		/// <summary>
		/// Raised on reproduction end.
		/// </summary>
		public event EventHandler ReproductionEnded;

		/// <summary>
		/// Stop this reproduction.
		/// </summary>
		public void Stop()
		{
			ReproductionSession.Stop();
		}

		/// <summary>
		/// Pause this reproduction.
		/// </summary>
		public void Pause()
		{
			ReproductionSession.Pause();
		}

		/// <summary>
		/// Resume this reproduction.
		/// </summary>
		public void Resume()
		{
			ReproductionSession.Play();
		}

		/// <summary>
		/// Forced stop.
		/// </summary>
		public void Discard()
		{
			ReproductionSession.CloseReproduction();
		}

		/// <summary>
		/// Start reproducing this stream.
		/// </summary>
		public void Play()
		{
			ReproductionSession = LoadStream();

			ReproductionSession.ReproductionEnded +=
				(obj, e) => ReproductionEnded?.Invoke(this, EventArgs.Empty);

			ReproductionSession.Play();
		}

		/// <summary>
		/// Called on audio loading.
		/// </summary>
		public virtual AsyncReproductionSession LoadStream()
		{
			return null;
		}
	}

	public class YoutubeReproductionState : SongReproductionState
	{
		/// <summary>
		/// Makes a new instance of <see cref="YoutubeReproductionState"/> class.
		/// </summary>
		public YoutubeReproductionState(IAudioSource source, VoiceChannel service, SocketGuildUser user, Youtube.Video video)
			: base(source, service, user)
		{
			Video = video;
		}

		/// <summary>
		/// Source video url.
		/// </summary>
		public Youtube.Video Video { get; }

		/// <summary>
		/// Song title.
		/// </summary>
		public override string Title => Video.Title;

		/// <summary>
		/// Preview image url.
		/// </summary>
		public override string ImageUrl => Video.ImageUrl;

		/// <summary>
		/// Video description.
		/// </summary>
		public override string Description => Video.Description;

		/// <summary>
		/// Total audio buffer time.
		/// </summary>
		public override TimeSpan TotalTime => Video.Duration;

		/// <summary>
		/// Called on audio loading.
		/// </summary>
		public override AsyncReproductionSession LoadStream()
		{
			bool hideOutput = true;

			string dlho = hideOutput ? "-q" : "";
			string ffho = hideOutput ? "-loglevel panic" : "";
			
			var p = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "cmd.exe",
					Arguments = $"/C youtube-dl.exe {dlho} -o - {Video.Url} | ffmpeg.exe {ffho} -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1",
					//RedirectStandardError = true,
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = false,
					WindowStyle = ProcessWindowStyle.Hidden
				}
			};

			p.Start();

			new Task(() =>
			{
				p.WaitForExit();
				ReproductionSession?.Stop();
			}).Start();

			return _service.SendAudio(p.StandardOutput, Source);
		}

	}
}
