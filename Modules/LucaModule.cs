using System;
using System.Text.RegularExpressions;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace Ipotenusa.Modules
{
	[Name("Luca colico")]
	[Summary("Luca colico è una persona addicted da yuasa e\n" +
		"basta, non c'è altro da dire per il resto non ha caratteristiche particolari.")]
	public class LucaModule : ModuleBase<SocketCommandContext>
	{
		private readonly Random _rand;
		private readonly VoiceService _voice;
		private readonly AniList _myanimelist;
		private readonly IConfigurationRoot _config;

		// Remember to add an instance of the AudioService
		// to your IServiceCollection when you initialize your bot
		public LucaModule(VoiceService service, AniList myanimelist, IConfigurationRoot config, Random rand)
		{
			_voice = service;
			_myanimelist = myanimelist;
			_config = config;
			_rand = rand;
		}

		[Command("lucacolico")]
		[Summary("Semplicemente luca colico")]
		public async Task LucaColico()
		{
			await Context.Message.DeleteAsync();
			await ReplyAsync($"NO VABBÈ EPICO");
		}

		[Command("capolavoro")]
		[Summary("Semplicemente oggettivo")]
		public async Task Capolavoro()
		{
			await Context.Message.DeleteAsync();
			var userVoice = Context.User as IVoiceState;
			await _voice.JoinChannel(userVoice.VoiceChannel as SocketVoiceChannel);
			_voice.ReproduceWav(userVoice.VoiceChannel as SocketVoiceChannel, _config["sounds:folder"] + "\\capolavoro.wav");
		}

		[Command("yuasa")]
		[Summary("Semplicemente luca colico")]
		public async Task Yuasa()
		{
			await Context.Message.DeleteAsync();

			var searchRes = await _myanimelist.SearchStaff("Masaaki Yuasa");

			var yuasa = await _myanimelist.StaffInfo(long.Parse(searchRes.First["id"].ToString()));

			var animeStaff = yuasa["anime_staff"] as JArray;

			var randomAnime = animeStaff[_rand.Next(animeStaff.Count)];

			var animeInfo = await _myanimelist.AnimeInfo(long.Parse(randomAnime["id"].ToString()));

			var builder = new EmbedBuilder()
			{
				Color = new Color(114, 137, 218),
				Description = Regex.Replace(animeInfo["description"]?.ToString() ?? "No description.", "<.*?>", String.Empty),
				Title = $"{randomAnime["title_japanese"]} - {randomAnime["title_romaji"]}",
				ImageUrl = randomAnime["image_url_med"].ToString(),
				Url = @"https://anilist.co/anime/" + randomAnime["id"]
			};

			builder.AddInlineField("Role", randomAnime["role"].ToString());
			builder.AddInlineField("Episodes", randomAnime["total_episodes"].ToString() + " " + randomAnime["airing_status"].ToString());
			builder.AddInlineField("Type", randomAnime["type"].ToString());

			await ReplyAsync("", false, builder.Build());
		}

	}
}
