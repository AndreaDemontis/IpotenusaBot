using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

using Discord.WebSocket;
using Discord;

using Microsoft.Extensions.Configuration;

using Ipotenusa.Servers.Channels;
using Discord.Rest;

namespace Ipotenusa.Services
{
    public class Diagnostic
    {

		DiscordSocketClient _discord;
		IConfigurationRoot _config;
		BotChannel _channel;
		DiagnosticDrawable _chat;

		PerformanceCounter cpuCounter;
		PerformanceCounter ramCounter;
		PerformanceCounter ioCounter;

		/// <summary>
		/// Makes a new instance of <see cref="RecorderService"/> class.
		/// </summary>
		public Diagnostic(DiscordSocketClient discord, IConfigurationRoot config)
		{
			_discord = discord;
			_config = config;

			_chat = new DiagnosticDrawable(discord)
			{
				Avatar = _config["diagnostic:avatar"]
			};

			cpuCounter = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
			ramCounter = new PerformanceCounter("Process", "Working Set", Process.GetCurrentProcess().ProcessName);
			ioCounter = new PerformanceCounter("Process", "IO Data Operations/sec", Process.GetCurrentProcess().ProcessName);
		}

		/// <summary>
		/// Drawable.
		/// </summary>
		public DiagnosticDrawable DiagnosticDrawable => _chat;

		#region Logic

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
					_chat.CpuUsage = cpuCounter.NextValue() / 8;
					_chat.RamUsage = ramCounter.NextValue() / 1014 / 1014;
					_chat.DiskUsage = ioCounter.NextValue();

					await _chat.Update();

					Thread.Sleep(5000);
				}
			}).Start();

			await Task.CompletedTask;
		}

		#endregion
	}


	public class DiagnosticDrawable : DrawableSection
	{

		/// <summary>
		/// Makes a new instance of <see cref="LogDrawable"/> class.
		/// </summary>
		public DiagnosticDrawable(DiscordSocketClient discord)
			: base("Nyan stats", discord)
		{
		}

		/// <summary>
		/// Logger avatar.
		/// </summary>
		public string Avatar { get; set; }

		/// <summary>
		/// Cpu usage.
		/// </summary>
		public float CpuUsage { get; set; }

		/// <summary>
		/// Ram usage.
		/// </summary>
		public float RamUsage { get; set; }

		/// <summary>
		/// Disk usage.
		/// </summary>
		public float DiskUsage { get; set; }

		public override Embed Render()
		{
			var builder = new EmbedBuilder()
			{
				Color = Color.Green,
				Description = "(¬ ¬ ) Q-queste statistiche n-non sono p-per te b-baka."
			};

			builder.WithAuthor(Name, Avatar);

			builder.AddField("CPU usage", Utils.Formatting.BarChart(44, (int)CpuUsage));
			builder.AddInlineField("RAM usage", RamUsage.ToString("00.##" + " mb"));
			builder.AddInlineField("Disk usage", DiskUsage.ToString("##0.00") + " IOPS");
			builder.AddInlineField("Status", "I love cats!");
			builder.AddInlineField("Other info", "I'm a loli.");
			builder.AddInlineField("Other info", "I'm a loli.");
			builder.AddInlineField("Other info", "I'm a loli.");

			return builder.Build();
		}

	}
}
