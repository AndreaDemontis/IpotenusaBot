using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Ipotenusa.Utils;
using NAudio.Wave;

using Microsoft.Extensions.Configuration;

namespace Ipotenusa
{
	public class VoiceService
	{
		public const int SAMPLE_RATE = 48000;
		public const int SAMPLE_SIZE = sizeof(short);
		public const int CHANNEL_COUNT = 2;
		public const int FRAME_SAMPLES = 20 * (SAMPLE_RATE / 1000);

		DiscordSocketClient _discord;

		/// <summary>
		/// Makes a new instance of <see cref="VoiceService"/> class.
		/// </summary>
		public VoiceService(DiscordSocketClient discord, CommandService commands, IConfigurationRoot config)
		{
			_discord = discord;
			Channels = new ConcurrentDictionary<ulong, AudioChannel>();


		}

		#region Loop and logic

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
						foreach (var ch in Channels)
						{
							// - Find the server
							var guild = _discord.GetGuild(ch.Key);

							// - Server not found
							if (guild == null)
								continue;

							var channel = ch.Value.Channel;

							// - Receive from users
							foreach (var user in channel.Users)
							{
								// - User not connected in audio stream
								if (user.AudioStream == null)
									continue;

								// - Gets user informations
								var sender = guild.GetUser(user.Id);
								if (sender == null || sender.IsMuted || sender.IsSelfMuted || sender.IsSuppressed)
									continue;

								// - If user sending audio packets
								if (user.AudioStream.AvailableFrames > 0)
								{
									await ch.Value.SendAudio(sender, user.AudioStream);
								}
							}
						}

						Thread.Sleep(1);
					}
					catch (Exception )
					{

					}
				}
			}).Start();

			new Task(async () =>
			{

				while (Running)
				{
					try
					{
						foreach (var ch in Channels)
						{
							// - Find the server
							var guild = ch.Value.Channel.Guild.Id;

							var channel = ch.Value.Channel;
							var outQueue = ch.Value.AudioQueue;

							if (outQueue.Count > 0)
							{
								byte[] mixed = new byte[FRAME_SAMPLES * SAMPLE_SIZE];
								for (int i = 0; i < FRAME_SAMPLES; ++i)
								{
									int sample = 0;
									foreach (var pair in outQueue)
									{
										var c = pair.Value;
										if (((i + c.ReproductionIndex) * SAMPLE_SIZE) + SAMPLE_SIZE > c.Data.Length)
											continue;
										sample += GetInt16(c.Data, (c.ReproductionIndex + i) * SAMPLE_SIZE, true);
									}

									GetBytes((short)Math.Min(Math.Max(short.MinValue, sample), short.MaxValue), mixed, i * SAMPLE_SIZE, true);
								}

								// Send the buffer to Discord
								await ch.Value.OutStream.WriteAsync(mixed, 0, FRAME_SAMPLES);

								foreach (var c in outQueue)
								{
									if (c.Value.ReproductionIndex + FRAME_SAMPLES < c.Value.Data.Length)
									{
										c.Value.ReproductionIndex += FRAME_SAMPLES / 2;
									}
									else outQueue.TryRemove(c.Key, out var e);
								}
							}
							else
							{
								// Flush stream
								await ch.Value.OutStream.FlushAsync();
								Thread.Sleep(1);
							}
						}
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

		#region Channel management

		/// <summary>
		/// Connected channels streams.
		/// </summary>
		public ConcurrentDictionary<ulong, AudioChannel> Channels { get; }

		/// <summary>
		/// Joined audio channels.
		/// </summary>
		public IEnumerable<AudioChannel> ConnectedChannels => Channels.Values;

		/// <summary>
		/// Event channel connection.
		/// </summary>
		public event AsyncEventHandler<ChannelConnectionEventArgs> OnChannelConnection;

		/// <summary>
		/// Joins in a new channel.
		/// </summary>
		/// <param name="channel">Target channel.</param>
		public async Task JoinChannel(SocketVoiceChannel channel)
		{
			AudioChannel data = null;
			IAudioClient client;
			AudioOutStream outStream;

			if (channel == null || channel.Guild == null)
				return;

			bool joinedInServer = Channels.ContainsKey(channel.Guild.Id);

			if (joinedInServer && Channels[channel.Guild.Id].Channel.Id == channel.Id)
				return;

			if (joinedInServer)
				await LeaveVoice(Channels[channel.Guild.Id].Channel.Id);

			try
			{
				client = await channel.ConnectAsync();
			}
			catch (Exception) { return; }

			outStream = client.CreatePCMStream(AudioApplication.Mixed);

			data = new AudioChannel(channel, client, outStream);

			client.SpeakingUpdated += (u, s) => data.SetUserState(channel.Guild.GetUser(u), s);

			if (joinedInServer)
			{
				Channels[channel.Guild.Id] = data;
			}
			else if (!Channels.TryAdd(channel.Guild.Id, data))
			{
				return;
			}

			OnChannelConnection?.Invoke(this, new ChannelConnectionEventArgs(data, true));
		}

		/// <summary>
		/// Leave from a specified channel.
		/// </summary>
		/// <param name="channelId">Channel id.</param>
		public async Task LeaveVoice(ulong channelId)
		{
			AudioChannel val;

			if (!Channels.TryRemove(channelId, out val))
				return;

			OnChannelConnection?.Invoke(this, new ChannelConnectionEventArgs(val, false));

			// Disconnect audio streams
			if (val.OutStream != null)
				val.OutStream.Close();

			// leave voice chat
			await val.Client.StopAsync();
		}

		#endregion

		#region Reproduce

		/// <summary>
		/// Send audio in queue for reproduction.
		/// </summary>
		/// <param name="buffer"></param>
		public void SendAudio(SocketVoiceChannel channel, byte[] buffer)
		{
			var queue = Channels[channel.Guild.Id].AudioQueue;

			int lastIndex = queue.Count > 0 ? queue.Last().Key + 1 : 0;

			queue.TryAdd(lastIndex, new ReproductionSession
			{
				Data = buffer,
				ReproductionIndex = 0
			});
		}

		/// <summary>
		/// Reproduce an audio stream in a channel.
		/// </summary>
		/// <param name="channel">Target channel.</param>
		public void ReproduceMp3(SocketVoiceChannel channel, Stream audio)
		{
			try
			{
				byte[] buffer = null;

				// Create a new Disposable MP3FileReader, to read audio from the filePath parameter 
				using (var MP3Reader = new Mp3FileReader(audio))
					buffer = Reproduce(channel, MP3Reader);

				SendAudio(channel, buffer);
			}
			catch (Exception e) { }
		}

		/// <summary>
		/// Reproduce an audio stream in a channel.
		/// </summary>
		/// <param name="channel">Target channel.</param>
		public void ReproduceMp3(SocketVoiceChannel channel, string audio)
		{
			try
			{
				byte[] buffer = null;

				// Create a new Disposable MP3FileReader, to read audio from the filePath parameter 
				using (var MP3Reader = new Mp3FileReader(audio))
					buffer = Reproduce(channel, MP3Reader);

				SendAudio(channel, buffer);
			}
			catch (Exception e) { }
		}

		/// <summary>
		/// Reproduce an audio stream in a channel.
		/// </summary>
		/// <param name="channel">Target channel.</param>
		public void ReproduceWav(SocketVoiceChannel channel, Stream audio)
		{
			try
			{
				byte[] buffer = null;

				// Create a new Disposable MP3FileReader, to read audio from the filePath parameter 
				using (var MP3Reader = new WaveFileReader(audio))
					buffer = Reproduce(channel, MP3Reader);

				SendAudio(channel, buffer);
			}
			catch (Exception e) { }
		}

		/// <summary>
		/// Reproduce an audio stream in a channel.
		/// </summary>
		/// <param name="channel">Target channel.</param>
		public void ReproduceWav(SocketVoiceChannel channel, string audio)
		{
			byte[] buffer = null;

			try
			{
				// Create a new Disposable MP3FileReader, to read audio from the filePath parameter 
				using (var MP3Reader = new WaveFileReader(audio))
					buffer = Reproduce(channel, MP3Reader);

				SendAudio(channel, buffer);
			}
			catch (Exception e) { }
		}

		/// <summary>
		/// Reproduce an audio in a channel.
		/// </summary>
		/// <param name="channel">Target channel.</param>
		public byte[] Reproduce(SocketVoiceChannel channel, IWaveProvider audio)
		{
			// Create a new Output Format, using the spec that Discord will accept, and with the number of channels that our client supports.
			var OutFormat = new WaveFormat(SAMPLE_RATE, SAMPLE_SIZE * 8, CHANNEL_COUNT);

			List<byte> bytes = new List<byte>();

			// Create a Disposable Resampler, which will convert the read MP3 data to PCM, using our Output Format
			using (var resampler = new MediaFoundationResampler(audio, OutFormat))
			{
				resampler.ResamplerQuality = 60; // Set the quality of the resampler to 60, the highest quality
				int blockSize = OutFormat.AverageBytesPerSecond / 50; // Establish the size of our AudioBuffer
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

		#region PCM helpers

		public static short GetInt16(byte[] buffer, int index, bool littleEndian)
		{
			short result = 0;
			if (littleEndian)
			{
				for (int i = 0; i < sizeof(short); i++)
				{
					result |= (short)(buffer[index + i] << (i * 8));
				}
			}
			else
			{
				for (int i = 0; i < sizeof(short); i++)
				{
					result |= (short)(buffer[index + i] << (sizeof(short) - i * 8));
				}
			}
			return result;
		}

		public static void GetBytes(short value, byte[] buffer, int index, bool littleEndian)
		{
			if (littleEndian)
			{
				for (int i = 0; i < sizeof(short); i++)
				{
					buffer[index + i] = (byte)((value >> (i * 8)) & 0xFF);
				}
			}
			else
			{
				for (int i = 0; i < sizeof(short); i++)
				{
					buffer[index + i] = (byte)((value >> (sizeof(short) - i * 8)) & 0xFF);
				}
			}
		}

		#endregion
	}

	public class ReproductionSession
	{
		public byte[] Data { get; set; }
		public int ReproductionIndex;
		public bool Finished;
	}

	public class AudioChannel
	{
		public AudioChannel(SocketVoiceChannel channel, IAudioClient client, AudioOutStream outStream)
		{
			Client = client;
			OutStream = outStream;
			Channel = channel;

			AudioQueue = new ConcurrentDictionary<int, ReproductionSession>();
		}

		/// <summary>
		/// Discord channel reference.
		/// </summary>
		public SocketVoiceChannel Channel { get; }

		/// <summary>
		/// Audio room client.
		/// </summary>
		public IAudioClient Client { get; }

		/// <summary>
		/// Stream for playback.
		/// </summary>
		public AudioOutStream OutStream { get; }

		/// <summary>
		/// Outgoing audio queue stream.
		/// </summary>
		public ConcurrentDictionary<int, ReproductionSession> AudioQueue { get; }

		/// <summary>
		/// Users streams.
		/// </summary>
		public IEnumerable<AudioInStream> InStreams => Channel.Users.Select(x => x.AudioStream);

		/// <summary>
		/// Triggered on audio packet.
		/// </summary>
		public event AsyncEventHandler<AudioStreamEventArgs> OnAudioPacket;

		/// <summary>
		/// Triggered on user state change.
		/// </summary>
		public event AsyncEventHandler<UserStateEventArgs> OnUserStateChange;

		public async Task SendAudio(SocketGuildUser usr, AudioInStream data)
		{
			if (OnAudioPacket != null)
				await OnAudioPacket(this, new AudioStreamEventArgs(this, usr, data));
		}

		public async Task SetUserState(SocketGuildUser user, bool state)
		{
			if (OnUserStateChange != null)
				await OnUserStateChange(this, new UserStateEventArgs(user, state));
		}
	}

	public class AudioStreamEventArgs : EventArgs
	{

		public AudioStreamEventArgs(AudioChannel ch, SocketGuildUser usr, AudioInStream stream)
		{
			Stream = stream;
			User = usr;
			Channel = ch;
		}

		/// <summary>
		/// Packet data.
		/// </summary>
		public AudioInStream Stream { get; }

		/// <summary>
		/// User.
		/// </summary>
		public SocketGuildUser User { get; }

		/// <summary>
		/// Audio channel.
		/// </summary>
		public AudioChannel Channel { get; }
	}

	public class ChannelConnectionEventArgs : EventArgs
	{

		public ChannelConnectionEventArgs(AudioChannel channel, bool state)
		{
			Channel = channel;
			Connected = state;
		}

		/// <summary>
		/// Channel reference.
		/// </summary>
		public AudioChannel Channel { get; }

		/// <summary>
		/// State.
		/// </summary>
		public bool Connected { get; }
	}

	public class UserStateEventArgs : EventArgs
	{

		public UserStateEventArgs(SocketGuildUser user, bool talking)
		{
			Talking = talking;
			User = user;
		}

		/// <summary>
		/// Connected state.
		/// </summary>
		public bool Talking { get; }

		/// <summary>
		/// Target user.
		/// </summary>
		public SocketGuildUser User { get; }
	}

	public delegate Task AsyncEventHandler<T>(object sender, T data) where T : EventArgs;
}
