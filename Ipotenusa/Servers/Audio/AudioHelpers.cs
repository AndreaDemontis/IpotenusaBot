using NAudio.Wave;

using System;
using System.Threading.Tasks;

namespace Ipotenusa.Servers.Audio
{
	public static class AudioHelpers
	{
		/// <summary>
		/// Discord default sample rate.
		/// </summary>
		public const int SAMPLE_RATE = 48000;

		/// <summary>
		/// Discord default sample size.
		/// </summary>
		public const int SAMPLE_SIZE = sizeof(short);

		/// <summary>
		/// Audio channel count.
		/// </summary>
		public const int CHANNEL_COUNT = 2;

		/// <summary>
		/// Size for a PCM packet.
		/// </summary>
		public const int FRAME_SAMPLES = 80 * (SAMPLE_RATE / 1000);

		/// <summary>
		/// Sample bits.
		/// </summary>
		public const int SAMPLE_BITS = SAMPLE_SIZE * 8;

		/// <summary>
		/// Default send/receive buffer size.
		/// </summary>
		public const int BUFFER_SIZE = FRAME_SAMPLES * SAMPLE_SIZE;

		/// <summary>
		/// Default discord bitrate.
		/// </summary>
		public const int BITRATE = 128000;

		/// <summary>
		/// Bytes/sec
		/// </summary>
		public const int BYTES_PER_SECOND = SAMPLE_RATE * CHANNEL_COUNT * SAMPLE_SIZE;

		/// <summary>
		/// Default wave format for IO.
		/// </summary>
		public static WaveFormat PcmFormat => new WaveFormat(SAMPLE_RATE, SAMPLE_SIZE * 8, CHANNEL_COUNT);

		#region PCM helpers

		/// <summary>
		/// Gets a 16bit sample from a buffer.
		/// </summary>
		/// <param name="buffer">Source buffer.</param>
		/// <param name="index">Sample index in the source buffer.</param>
		/// <param name="littleEndian">Bit arrangement.</param>
		/// <returns>Sample value.</returns>
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

		/// <summary>
		/// Gets the bytes from a 16bit sample.
		/// </summary>
		/// <param name="value">Sample value.</param>
		/// <param name="buffer">Output buffer.</param>
		/// <param name="index">Save index in the output buffer.</param>
		/// <param name="littleEndian">Bitt arrangement.</param>
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
}
