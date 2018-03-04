using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ipotenusa.Servers.Audio
{
	public class DefaultAudioSource : IAudioSource
	{
		double d;
		public double Volume { get => 100; set { } }
		public double Gain { get => 1; set { } }

		static DefaultAudioSource inst = new DefaultAudioSource();
		public static DefaultAudioSource Instance => inst;
	}

	public interface IAudioSource
	{

		/// <summary>
		/// Source device volume.
		/// </summary>
		double Volume { get; set; }

		/// <summary>
		/// Source device gain.
		/// </summary>
		double Gain { get; set; }
	}
}
