using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

using Ipotenusa.Utils;
using Ipotenusa.Servers.Channels;
using Discord.Rest;

namespace Ipotenusa
{
	public class LoggingService
	{
		private readonly DiscordSocketClient _discord;
		private readonly CommandService _commands;
		private readonly LogDrawable _channelReporter;
		private readonly IConfigurationRoot _config;

		private string _logDirectory { get; }
		private string _logFile => Path.Combine(_logDirectory, $"{DateTime.UtcNow.ToString("yyyy-MM-dd")}.txt");

		/// <summary>
		/// Makes a new instance of <see cref="LoggingService"/> service.
		/// </summary>
		public LoggingService(DiscordSocketClient discord, CommandService commands, IConfigurationRoot config)
		{
			_discord = discord;
			_commands = commands;
			_config = config;

			_logDirectory = Path.Combine(AppContext.BaseDirectory, _config["logger:folder"]);

			// - State reporter
			_channelReporter = new LogDrawable(5, discord)
			{
				Avatar = _config["logger:avatar"]
			};

			// - Log events
			_discord.Log += OnLogAsync;
			_commands.Log += OnLogAsync;
		}

		/// <summary>
		/// Gets the channel reporter drawable.
		/// </summary>
		public LogDrawable LogDrawable => _channelReporter;

		private async Task OnLogAsync(LogMessage msg)
		{
			// - Create directory if not exists
			if (!Directory.Exists(_logDirectory))
				Directory.CreateDirectory(_logDirectory);

			// - Create file if not exists
			if (!File.Exists(_logFile))
				File.Create(_logFile).Dispose();

			// - Report to state reporter
			_channelReporter.PushLog(msg);

			// - Log message building
			string logText = $"{DateTime.UtcNow.ToString("hh:mm:ss")} " +
				$"[{msg.Severity}] {msg.Source}: {msg.Exception?.ToString() ?? msg.Message}";

			// - Write the log in a file
			File.AppendAllText(_logFile, logText + "\n");

			// - Write the log in the console
			await Console.Out.WriteLineAsync(logText);
		}
	}

	public class LogDrawable : DrawableSection
	{
		private ConcurrentFixedSizedQueue<TimeLog> logs;

		/// <summary>
		/// Makes a new instance of <see cref="LogDrawable"/> class.
		/// </summary>
		public LogDrawable(int logLimit, DiscordSocketClient discord)
			: base("Moe logger", discord)
		{
			logs = new ConcurrentFixedSizedQueue<TimeLog>();
			logs.Limit = logLimit;
		}

		/// <summary>
		/// Logger avatar.
		/// </summary>
		public string Avatar { get; set; }

		/// <summary>
		/// Push a log in the reporter service.
		/// </summary>
		/// <param name="log">Log to push.</param>
		public void PushLog(LogMessage log)
		{
			logs.Enqueue(new TimeLog
			{
				Log = log,
				Time = DateTime.UtcNow
			});
		}

		public override Embed Render()
		{
			var builder = new EmbedBuilder()
			{
				Color = Color.Orange,
				Description = "Non sono mica qui perchè me lo hai chiesto te!! non fraintendere! mpfh!!"
			};

			builder.WithAuthor(Name, Avatar);

			string logsString = "";

			foreach (TimeLog log in logs)
			{
				string l = $"{log.Time.ToString("hh:mm:ss")} [{log.Log.Severity}] {log.Log.Source}\n{log.Log.Exception?.ToString() ?? log.Log.Message}";
				logsString += Formatting.Code(l);
			}

			builder.Description += logsString;

			return builder.Build();
		}

	}

	/// <summary>
	/// Class for logs with time informations.
	/// </summary>
	public struct TimeLog
	{
		/// <summary>
		/// Log content.
		/// </summary>
		public LogMessage Log { get; set; }

		/// <summary>
		/// Log time.
		/// </summary>
		public DateTime Time { get; set; }
	}
}