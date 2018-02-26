using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Ipotenusa.Modules
{
	[Name("Pizza Kun")]
	[Summary("Luca colico è una persona addicted da yuasa e\n" +
		"basta, non c'è altro da dire per il resto non ha caratteristiche particolari.")]
	public class PizzaModule : ModuleBase<SocketCommandContext>
	{
		private readonly VoiceService _voice;
		private readonly Random _rand;
		private readonly IConfigurationRoot _config;

		// Remember to add an instance of the AudioService
		// to your IServiceCollection when you initialize your bot
		public PizzaModule(VoiceService service, Random rand, IConfigurationRoot config)
		{
			_rand = rand;
			_voice = service;
			_config = config;
		}


		[Command("randomText")]
		[Summary("L'arancia è fatta.")]
		public async Task RandomText()
		{
			await Context.Message.DeleteAsync();
			await ReplyAsync(GetRandomWords(10));
		}

		[Command("poi")]
		[Summary("L'arancia è fatta.")]
		public async Task Poi()
		{
			await Context.Message.DeleteAsync();
			var userVoice = Context.User as IVoiceState;

			var n = _rand.Next(1, 9);

			var num = n == 9 ? "99" : "0" + n;

			await _voice.JoinChannel(userVoice.VoiceChannel as SocketVoiceChannel);
			_voice.ReproduceMp3(userVoice.VoiceChannel as SocketVoiceChannel, _config["sounds:folder"] + $"\\POI_{num}.mp3");
		}

		public string GetRandomWords(int MaxNumber)
		{
			string Source = "https://it.wikipedia.org/w/api.php?action=query&generator=random&grnnamespace=0&prop=extracts&explaintext&exintro=&format=json";

			try
			{
				HttpWebRequest request = WebRequest.Create(Source) as HttpWebRequest;
				using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
				{
					if (response.StatusCode != HttpStatusCode.OK)
						return "";

					// Convert stream to string
					StreamReader reader = new StreamReader(response.GetResponseStream());
					string Text = reader.ReadToEnd();

					// Parse json
					string CompleteText;
					Text = Text.Split(new String[] { @"extract" }, StringSplitOptions.None)[1];
					Text = Text.Split(new String[] { "\"}" }, StringSplitOptions.None)[0].Substring(3);
					CompleteText = Regex.Unescape(Text);

					// Get words
					int CurrentNumber = _rand.Next(MaxNumber) + 1;
					string ToReturn = "";

					for (int i = 0; i < CurrentNumber; i++)
						ToReturn += Text.Split(' ')[_rand.Next(Text.Split(' ').Length)] + ' ';

					ToReturn = WebUtility.HtmlDecode(ToReturn);
					ToReturn = new String(ToReturn.Where(x => char.IsLetterOrDigit(x) || char.IsWhiteSpace(x)).ToArray());

					if (ToReturn.Length == 0)
						ToReturn = "Pollo";

					return ToReturn;
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
				return null;
			}
		}
	}
}
