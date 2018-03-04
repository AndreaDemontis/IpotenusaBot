using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Discord.Audio;
using Discord.WebSocket;

using NAudio.Wave;

namespace Ipotenusa.Servers.Audio
{
	public class VoiceChannel : IDisposable
	{
		private readonly DiscordServer _server;

		/// <summary>
		/// Makes a new instance of <see cref="VoiceChannel"/> class.
		/// </summary>
		public VoiceChannel(DiscordServer server)
		{
			_server = server;

			AudioQueue = new ConcurrentDictionary<int, BaseReproductionSession>();
		}

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
			if (_server.DiscordHandler.CurrentUser.VoiceChannel != null)
			{
				CurrentChannel = _server.DiscordHandler.CurrentUser.VoiceChannel;
				Client = await CurrentChannel.ConnectAsync();
				OutStream = Client.CreateDirectPCMStream(AudioApplication.Music, AudioHelpers.BITRATE);

				await LeaveVoice();
			}

			new Task(async () => await StartAsyncSend()).Start();
			new Task(async () => await StartAsyncReceive()).Start();

			await Task.CompletedTask;
		}

		#region Channels management

		/// <summary>
		/// Triggered on channel connection / disconnection.
		/// </summary>
		public event AsyncEventHandler<ChannelEventArgs> ChannelConnection;

		/// <summary>
		/// Discord channel reference.
		/// </summary>
		public SocketVoiceChannel CurrentChannel { get; private set; }

		/// <summary>
		/// Stream for playback.
		/// </summary>
		public AudioOutStream OutStream { get; private set; }

		/// <summary>
		/// Audio channel client.
		/// </summary>
		public IAudioClient Client { get; private set; }

		/// <summary>
		/// Users streams.
		/// </summary>
		public IEnumerable<AudioInStream> InStreams => CurrentChannel.Users.Select(x => x.AudioStream);

		/// <summary>
		/// Joins in a new channel.
		/// </summary>
		/// <param name="channel">Target channel.</param>
		public async Task JoinChannel(SocketVoiceChannel channel)
		{
			if (channel == null || channel.Guild == null)
				return;

			if (channel == CurrentChannel)
				return;

			if (CurrentChannel != null && CurrentChannel != channel)
				await LeaveVoice();

			try
			{
				CurrentChannel = channel;
				Client = await channel.ConnectAsync();
				OutStream = Client.CreatePCMStream(AudioApplication.Music, AudioHelpers.BITRATE);

				Client.SpeakingUpdated += UserStateHandler;

				if (ChannelConnection != null)
					await ChannelConnection(this, new ChannelEventArgs(CurrentChannel, true));

				await Task.CompletedTask;
			}
			catch (Exception e)
			{
				return;
			}
		}

		/// <summary>
		/// Leave from a specified channel.
		/// </summary>
		/// <param name="channelId">Channel id.</param>
		public async Task LeaveVoice()
		{
			if (ChannelConnection != null)
				await ChannelConnection(this, new ChannelEventArgs(CurrentChannel, false));

			CurrentChannel = null;

			// Disconnect audio streams
			if (OutStream != null)
				OutStream.Close();

			// leave voice chat
			await Client.StopAsync();

			Client = null;
			OutStream = null;
		}

		#endregion

		#region User state

		/// <summary>
		/// Raised on user voice status change.
		/// </summary>
		public event AsyncEventHandler<UserEventArgs> UserStateChange;

		/// <summary>
		/// Handles channel user state changes.
		/// </summary>
		private async Task UserStateHandler(ulong userId, bool talking)
		{
			UserVoiceState state = new UserVoiceState
			{
				User = CurrentChannel.GetUser(userId),
				Talking = talking
			};

			if (UserStateChange != null)
				await UserStateChange(this, new UserEventArgs(state));

			await Task.CompletedTask;
		}

		#endregion

		#region Reproduction

		/// <summary>
		/// Outgoing audio queue stream.
		/// </summary>
		public ConcurrentDictionary<int, BaseReproductionSession> AudioQueue { get; }

		/// <summary>
		/// Send audio in queue for reproduction.
		/// </summary>
		/// <param name="buffer"></param>
		public ReproductionSession SendAudio(byte[] buffer, IAudioSource source = null)
		{
			ReproductionSession session; 
			int lastIndex = AudioQueue.Count > 0 ? AudioQueue.Last().Key + 1 : 0;

			session = new ReproductionSession(ref buffer);
			session.Source = source;

			session.Play();

			if (!AudioQueue.TryAdd(lastIndex, session))
			{
				return null;
			}

			return session;
		}

		/// <summary>
		/// Send audio in queue for reproduction.
		/// </summary>
		/// <param name="buffer"></param>
		public AsyncReproductionSession SendAudio(StreamReader stream, IAudioSource source = null)
		{
			AsyncReproductionSession session;
			int lastIndex = AudioQueue.Count > 0 ? AudioQueue.Last().Key + 1 : 0;

			session = new AsyncReproductionSession(stream);
			session.Source = source;

			session.Play();

			if (!AudioQueue.TryAdd(lastIndex, session))
			{
				return null;
			}

			return session;
		}

		/// <summary>
		/// Reproduce an audio stream in a channel.
		/// </summary>
		/// <param name="channel">Target channel.</param>
		public ReproductionSession ReproduceMp3(Stream audio, IAudioSource source = null)
		{
			try
			{
				byte[] buffer = null;

				// Create a new Disposable MP3FileReader, to read audio from the filePath parameter 
				using (var MP3Reader = new Mp3FileReader(audio))
					buffer = Reproduce(MP3Reader);

				return SendAudio(buffer, source);
			}
			catch (Exception) { }

			return null;
		}

		/// <summary>
		/// Reproduce an audio stream in a channel.
		/// </summary>
		/// <param name="channel">Target channel.</param>
		public ReproductionSession ReproduceMp3(string audio, IAudioSource source = null)
		{
			try
			{
				byte[] buffer = null;

				// Create a new Disposable MP3FileReader, to read audio from the filePath parameter 
				using (var MP3Reader = new Mp3FileReader(audio))
					buffer = Reproduce(MP3Reader);

				return SendAudio(buffer, source);
			}
			catch (Exception) { }

			return null;
		}

		/// <summary>
		/// Reproduce an audio stream in a channel.
		/// </summary>
		/// <param name="channel">Target channel.</param>
		public ReproductionSession ReproduceWav(Stream audio, IAudioSource source = null)
		{
			try
			{
				byte[] buffer = null;

				// Create a new Disposable MP3FileReader, to read audio from the filePath parameter 
				using (var MP3Reader = new WaveFileReader(audio))
					buffer = Reproduce(MP3Reader);

				return SendAudio(buffer, source);
			}
			catch (Exception) { }

			return null;
		}

		/// <summary>
		/// Reproduce an audio stream in a channel.
		/// </summary>
		/// <param name="channel">Target channel.</param>
		public ReproductionSession ReproduceWav(string audio, IAudioSource source = null)
		{
			byte[] buffer = null;

			try
			{
				// Create a new Disposable MP3FileReader, to read audio from the filePath parameter 
				using (var MP3Reader = new WaveFileReader(audio))
					buffer = Reproduce(MP3Reader);

				return SendAudio(buffer, source);
			}
			catch (Exception) { }

			return null;
		}

		/// <summary>
		/// Reproduce an audio in a channel.
		/// </summary>
		/// <param name="channel">Target channel.</param>
		public byte[] Reproduce(IWaveProvider audio)
		{
			List<byte> bytes = new List<byte>();

			// Create a Disposable Resampler, which will convert the read MP3 data to PCM, using our Output Format
			using (var resampler = new MediaFoundationResampler(audio, AudioHelpers.PcmFormat))
			{
				resampler.ResamplerQuality = 60;
				int blockSize = AudioHelpers.PcmFormat.AverageBytesPerSecond / 50;
				byte[] buffer = new byte[blockSize];
				int byteCount;

				// Read audio into our buffer, and keep a loop open while data is present
				while ((byteCount = resampler.Read(buffer, 0, blockSize)) > 0)
				{
					if (byteCount < blockSize)
					{
						// Incomplete Frame
						for (int i = byteCount; i < blockSize; i++)
							buffer[i] = 0;
					}

					bytes.AddRange(buffer);
				}
			}

			return bytes.ToArray();
		}


		#endregion

		#region Reveiver

		/// <summary>
		/// Raised on audi packet received.
		/// </summary>
		public event AsyncEventHandler<AudioPcmEventArgs> PcmReceived;

		/// <summary>
		/// Start service loop.
		/// </summary>
		private async Task StartAsyncReceive()
		{
			Running = true;

			while (Running)
			{
				if (CurrentChannel == null)
				{
					Thread.Sleep(100);
					continue;
				}

				try
				{
					// - Receive from users
					foreach (var user in CurrentChannel.Users)
					{
						// - User not connected in audio stream
						if (user.AudioStream == null)
							continue;

						// - Gets user informations
						if (user == null || user.IsMuted || user.IsSelfMuted || user.IsSuppressed)
							continue;

						// - If user sending audio packets
						if (user.AudioStream.AvailableFrames > 0)
						{
							if (PcmReceived != null)
								await PcmReceived(this, new AudioPcmEventArgs(user, user.AudioStream));
						}
					}


					Thread.Sleep(1);
				}
				catch (Exception)
				{

				}
			}

			await Task.CompletedTask;
		}

		#endregion

		#region Reproduction mixer / sender

		/// <summary>
		/// Start service loop.
		/// </summary>
		private async Task StartAsyncSend()
		{
			Running = true;

			while (Running)
			{
				if (OutStream == null || !OutStream.CanWrite)
				{
					Thread.Sleep(100);
					continue;
				}

				try
				{
					if (AudioQueue.Count > 0)
					{
						byte[] mixed = new byte[AudioHelpers.BUFFER_SIZE * AudioHelpers.CHANNEL_COUNT];

						for (int i = 0; i < AudioHelpers.FRAME_SAMPLES; ++i)
						{
							int sample = 0;
							foreach (var audioPair in AudioQueue)
							{
								var session = audioPair.Value;

								if (session.State == ReproductionState.Paused)
									continue;

								if (session.State == ReproductionState.Stopped)
								{
									session.CloseReproduction();
								}

								if (session.State == ReproductionState.Closed || session.EndOfStream)
								{
									AudioQueue.TryRemove(audioPair.Key, out var e);
									continue;
								}

								double volume = Math.Max(Math.Min((session.Source?.Volume ?? DefaultAudioSource.Instance.Volume) / 100, 1), 0);
								double gain = (session.Source?.Gain ?? DefaultAudioSource.Instance.Gain) + 1;

								byte[] buffer = new byte[AudioHelpers.SAMPLE_SIZE];
								if (session.Read(buffer, 0, AudioHelpers.SAMPLE_SIZE) > 0)
								{
									sample += (Int32)(AudioHelpers.GetInt16(buffer, 0, true) * volume * gain);
								}
							}

							short sampleValue = (short)Math.Min(Math.Max(short.MinValue, sample), short.MaxValue);

							AudioHelpers.GetBytes(sampleValue, mixed, i * AudioHelpers.SAMPLE_SIZE, true);
						}

						// Send the buffer to Discord
						await OutStream.WriteAsync(mixed, 0, AudioHelpers.FRAME_SAMPLES * AudioHelpers.CHANNEL_COUNT);
					}
					else
					{
						// Flush stream
						await OutStream.FlushAsync();
						Thread.Sleep(100);
					}
				}
				catch (Exception e)
				{

				}
			}

			await Task.CompletedTask;
		}

		#endregion
		
	}

	public enum ReproductionState
	{
		Closed = 0,
		Stopped = 1,
		Playing = 2,
		Paused = 3
	}

	public abstract class BaseReproductionSession : Stream
	{

		/// <summary>
		/// Audio source.
		/// </summary>
		public IAudioSource Source { get; set; }

		/// <summary>
		/// Gets the reproduction state.
		/// </summary>
		public ReproductionState State { get; private set; }

		/// <summary>
		/// Raised on reproduction end.
		/// </summary>
		public event EventHandler ReproductionEnded;

		/// <summary>
		/// Check if it's in the end of the stream.
		/// </summary>
		public virtual bool EndOfStream { get; }

		/// <summary>
		/// Stop this reproduction.
		/// </summary>
		public void Stop()
		{
			State = ReproductionState.Stopped;

			ReproductionEnded?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Pause this reproduction.
		/// </summary>
		public void Pause()
		{
			State = ReproductionState.Paused;
		}

		/// <summary>
		/// Start reproducing this stream.
		/// </summary>
		public void Play()
		{
			State = ReproductionState.Playing;
		}

		/// <summary>
		/// Close the stream and the reproduction.
		/// </summary>
		public void CloseReproduction()
		{
			State = ReproductionState.Closed;

			Dispose();
		}

	}

	public class ReproductionSession : BaseReproductionSession
	{
		private long _reproductionIndex;

		/// <summary>
		/// Makes a new instance of <see cref="ReproductionSession"/> class.
		/// </summary>
		/// <param name="data">Audio data.</param>
		public ReproductionSession(ref byte[] data)
		{
			Data = data;
			Position = 0;
		}

		/// <summary>
		/// Audio stream data.
		/// </summary>
		public virtual byte[] Data { get; set; }

		/// <summary>
		/// Current reproduction index.
		/// </summary>
		public override long Position {get;set;}

		/// <summary>
		/// Check if it's in the end of the stream.
		/// </summary>
		public override bool EndOfStream => Position >= Length;

		/// <summary>
		/// Can read.
		/// </summary>
		public override bool CanRead => true;

		/// <summary>
		/// Can seeek.
		/// </summary>
		public override bool CanSeek => true;

		/// <summary>
		/// Can't write.
		/// </summary>
		public override bool CanWrite => false;

		/// <summary>
		/// Buffer length.
		/// </summary>
		public override long Length => Data.Length;

		/// <summary>
		/// Not implemented.
		/// </summary>
		public override void Flush() => throw new NotImplementedException();

		/// <summary>
		/// Read a chunk of data from the stream.
		/// </summary>
		/// <param name="buffer">Output buffer.</param>
		/// <param name="offset">Offset from current stream position.</param>
		/// <param name="count">Number of bytes to read.</param>
		/// <returns>Number of bytes readed.</returns>
		public override int Read(byte[] buffer, int offset, int count)
		{
			int limitedIndex = (int)Math.Min(Position + offset + count, Length);
			int limitedCount = (int)Math.Min(count, Length - limitedIndex);

			if (limitedIndex >= Length)
			{
				CloseReproduction();
				return 0;
			}

			Array.Copy(Data, limitedIndex, buffer, 0, limitedCount);

			Position += limitedCount;

			return limitedCount;
		}

		/// <summary>
		/// Seek to a specified index.
		/// </summary>
		/// <param name="offset">Index offset.</param>
		/// <param name="origin">Offset start position.</param>
		/// <returns>The new position.</returns>
		public override long Seek(long offset, SeekOrigin origin)
		{
			switch (origin)
			{
				case SeekOrigin.Begin:
					Position = offset;
					break;
				case SeekOrigin.Current:
					Position += offset;
					break;
				case SeekOrigin.End:
					Position = Length - offset;
					break;
			}

			return Position;
		}

		/// <summary>
		/// Not implemented.
		/// </summary>
		/// <param name="value">Not implemented.</param>
		public override void SetLength(long value) => throw new NotImplementedException();

		/// <summary>
		/// Not implemented.
		/// </summary>
		/// <param name="buffer">Not implemented.</param>
		/// <param name="offset">Not implemented.</param>
		/// <param name="count">Not implemented.</param>
		public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
	}

	public class AsyncReproductionSession : BaseReproductionSession
	{

		/// <summary>
		/// Makes a new instance of <see cref="ReproductionSession"/> class.
		/// </summary>
		/// <param name="data">Audio data.</param>
		public AsyncReproductionSession(StreamReader data)
		{
			StreamData = data;
			Position = 0;
		}

		/// <summary>
		/// Audio stream data.
		/// </summary>
		public StreamReader StreamData { get; set; }

		/// <summary>
		/// Current reproduction index.
		/// </summary>
		public override long Position { get; set; }

		/// <summary>
		/// Check if it's in the end of the stream.
		/// </summary>
		public override bool EndOfStream => StreamData.EndOfStream;

		/// <summary>
		/// Can read.
		/// </summary>
		public override bool CanRead => StreamData.BaseStream.CanRead;

		/// <summary>
		/// Can seeek.
		/// </summary>
		public override bool CanSeek => StreamData.BaseStream.CanSeek;

		/// <summary>
		/// Can't write.
		/// </summary>
		public override bool CanWrite => StreamData.BaseStream.CanWrite;

		/// <summary>
		/// Buffer length.
		/// </summary>
		public override long Length => throw new NotImplementedException();

		/// <summary>
		/// Not implemented.
		/// </summary>
		public override void Flush() => throw new NotImplementedException();

		/// <summary>
		/// Read a chunk of data from the stream.
		/// </summary>
		/// <param name="buffer">Output buffer.</param>
		/// <param name="offset">Offset from current stream position.</param>
		/// <param name="count">Number of bytes to read.</param>
		/// <returns>Number of bytes readed.</returns>
		public override int Read(byte[] buffer, int offset, int count)
		{
			var v = StreamData.BaseStream.Read(buffer, offset, count);

			Position += v;

			return v;
		}

		/// <summary>
		/// Seek to a specified index.
		/// </summary>
		/// <param name="offset">Index offset.</param>
		/// <param name="origin">Offset start position.</param>
		/// <returns>The new position.</returns>
		public override long Seek(long offset, SeekOrigin origin) => StreamData.BaseStream.Seek(offset, origin);

		/// <summary>
		/// Not implemented.
		/// </summary>
		/// <param name="value">Not implemented.</param>
		public override void SetLength(long value) => StreamData.BaseStream.SetLength(value);

		/// <summary>
		/// Not implemented.
		/// </summary>
		/// <param name="buffer">Not implemented.</param>
		/// <param name="offset">Not implemented.</param>
		/// <param name="count">Not implemented.</param>
		public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
	}

	public class UserVoiceState
	{
		/// <summary>
		/// Audio stream data.
		/// </summary>
		public SocketGuildUser User { get; set; }

		/// <summary>
		/// True if user is talking.
		/// </summary>
		public bool Talking { get; set; }
	}

	#region Event args

	public class UserEventArgs : EventArgs
	{
		/// <summary>
		/// Makes a new instance of <see cref="UserEventArgs"/> class.
		/// </summary>
		/// <param name="userState">New state.</param>
		public UserEventArgs(UserVoiceState userState)
		{
			UserState = userState;
		}

		/// <summary>
		/// User state.
		/// </summary>
		public UserVoiceState UserState { get; set; }
	}

	public class ChannelEventArgs : EventArgs
	{
		/// <summary>
		/// Makes a new instance of <see cref="ChannelEventArgs"/> class.
		/// </summary>
		/// <param name="userState">New state.</param>
		public ChannelEventArgs(SocketVoiceChannel channel, bool connection)
		{
			Channel = channel;
			Connected = connection;
		}

		/// <summary>
		/// User state.
		/// </summary>
		public SocketVoiceChannel Channel { get; set; }

		/// <summary>
		/// If true connecting to this channel, disconnecting otherwise.
		/// </summary>
		public bool Connected { get; set; }
	}

	public class AudioPcmEventArgs : EventArgs
	{
		/// <summary>
		/// Makes a new instance of <see cref="AudioPcmEventArgs"/> class.
		/// </summary>
		/// <param name="userState">New state.</param>
		public AudioPcmEventArgs(SocketGuildUser user, AudioInStream stream)
		{
			User = user;
			Stream = stream;
		}

		/// <summary>
		/// User state.
		/// </summary>
		public SocketGuildUser User { get; set; }

		/// <summary>
		/// If true connecting to this channel, disconnecting otherwise.
		/// </summary>
		public AudioInStream Stream { get; set; }
	}

	#endregion
}
