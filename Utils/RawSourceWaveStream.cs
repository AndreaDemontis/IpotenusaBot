using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio.Wave;

namespace Ipotenusa.Utils
{
	public class RawSourceWaveStream : WaveStream
	{
		private Stream sourceStream;
		private WaveFormat waveFormat;

		public RawSourceWaveStream(Stream sourceStream, WaveFormat waveFormat)
		{
			this.sourceStream = sourceStream;
			this.waveFormat = waveFormat;
		}

		public override WaveFormat WaveFormat
		{
			get { return this.waveFormat; }
		}

		public override long Length
		{
			get { return this.sourceStream.Length; }
		}

		public override long Position
		{
			get
			{
				return this.sourceStream.Position;
			}
			set
			{
				this.sourceStream.Position = value;
			}
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			return sourceStream.Read(buffer, offset, count);
		}
	}
}
