using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

using Microsoft.Extensions.Configuration;
using System.Xml;

namespace Ipotenusa.Services
{
	public class Youtube
	{
		private const string linkRegex = @"^(http(s)?:\/\/)?((w){3}.)?youtu(be|.be)?(\.com)?\/.+";
		private const string playlistRegex = @"(?:http|https|)(?::\/\/|)(?:www.|)(?:youtu\.be\/|youtube\.com(?:\/embed\/|\/v\/|\/watch\?v=|\/ytscreeningroom\?v=|\/feeds\/api\/videos\/|\/user\S*[^\w\-\s]|\S*[^\w\-\s]))([\w\-]{12,})[a-z0-9;:@#?&%=+\/\$_.-]*";
		private const string videoIdRegex = @"(?:http|https|)(?::\/\/|)(?:www.|)(?:youtu\.be\/|youtube\.com(?:\/embed\/|\/v\/|\/watch\?v=|\/ytscreeningroom\?v=|\/feeds\/api\/videos\/|\/user\S*[^\w\-\s]|\S*[^\w\-\s]))([\w\-]{11})[a-z0-9;:@#?&%=+\/\$_.-]*";

		private readonly IConfigurationRoot _config;

		/// <summary>
		/// Makes a new instance of <see cref="Youtube"/> class.
		/// </summary>
		public Youtube(IConfigurationRoot config)
		{
			_config = config;

			var service = new BaseClientService.Initializer();
			service.ApiKey = config["youtube:apikey"];
			service.ApplicationName = config["youtube:appname"];
			YoutubeService = new YouTubeService(service);
		}

		/// <summary>
		/// Gets the youtube api service.
		/// </summary>
		public YouTubeService YoutubeService { get; }

		/// <summary>
		/// Returns an object reappresenting the youtube object from an url.
		/// </summary>
		/// <param name="url">Source url.</param>
		public async Task<IYoutubeElement> ParseLink(string url)
		{
			Regex playlistPicker = new Regex(playlistRegex);
			Regex videoIdPicker = new Regex(linkRegex);

			var playlistMatch = playlistPicker.Match(url);

			if (playlistMatch.Success)
			{
				var playlist = new Playlist($@"{url}");
				await playlist.LoadMetadata(YoutubeService);
				return playlist;
			}

			var videoMatch = videoIdPicker.Match(url);

			if (videoMatch.Success)
			{
				var video = new Video($@"{url}");
				await video.LoadMetadata(YoutubeService);
				return video;
			}

			return null;
		}

		/// <summary>
		/// Youtube object general interface.
		/// </summary>
		public interface IYoutubeElement
		{
			/// <summary>
			/// Object url.
			/// </summary>
			string Url { get; }

			/// <summary>
			/// Loads object medatada.
			/// </summary>
			Task LoadMetadata(YouTubeService youtube);
		}


		public class Playlist : IYoutubeElement
		{
			/// <summary>
			/// Makes a new instance of <see cref="Playlist"/> class.
			/// </summary>
			/// <param name="url">Playlist link.</param>
			public Playlist(string url)
			{
				Url = url;
			}

			/// <summary>
			/// Playlist url.
			/// </summary>
			public string Url { get; }

			/// <summary>
			/// Gets the playlist videos.
			/// </summary>
			public IEnumerable<Video> Videos { get; private set; }

			public async Task LoadMetadata(YouTubeService youtube)
			{
				List<Video> videos = new List<Video>();

				Regex playlistPicker = new Regex(playlistRegex);
				string id = playlistPicker.Match(Url).Groups[1].Value;

				var nextPageToken = "";
				while (nextPageToken != null)
				{
					var query = youtube.PlaylistItems.List("snippet");

					query.PlaylistId = id;
					query.MaxResults = 50;
					query.PageToken = nextPageToken;

					// Retrieve the list of videos uploaded to the authenticated user's channel.
					var res = await query.ExecuteAsync();

					foreach (var playlistItem in res.Items)
					{
						// Print information about each video.
						var video = new Video($@"https://youtu.be/{playlistItem.Snippet.ResourceId.VideoId}");
						await video.LoadMetadata(youtube);
						videos.Add(video);
					}

					nextPageToken = res.NextPageToken;
				}

				Videos = videos;
			}
		}

		public class Video : IYoutubeElement
		{
			/// <summary>
			/// Makes a new instance of <see cref="Video"/> class.
			/// </summary>
			/// <param name="url">Video link.</param>
			public Video(string url)
			{
				Url = url;
			}

			/// <summary>
			/// Video url.
			/// </summary>
			public string Url { get; }

			/// <summary>
			/// Video title.
			/// </summary>
			public string Title { get; private set; }

			/// <summary>
			/// Video description.
			/// </summary>
			public string Description { get; private set; }

			/// <summary>
			/// Gets the video preview image link.
			/// </summary>
			public string ImageUrl { get; private set; }

			/// <summary>
			/// Video length.
			/// </summary>
			public TimeSpan Duration { get; private set; }

			public async Task LoadMetadata(YouTubeService youtube)
			{
				var query = youtube.Videos.List("snippet, contentDetails");
				
				Regex videoIdPicker = new Regex(videoIdRegex);
				string id = videoIdPicker.Match(Url).Groups[1].Value;

				query.Id = id;

				var res = await query.ExecuteAsync();

				if (res.Items.First().ContentDetails != null)
					Duration = XmlConvert.ToTimeSpan(res.Items.First().ContentDetails.Duration);
				else Duration = TimeSpan.FromMilliseconds(0);

				ImageUrl = res.Items.First().Snippet.Thumbnails.Medium.Url;
				Title = res.Items.First().Snippet.Title ?? "";
				Description = res.Items.First().Snippet.Description ?? "";
			}
		}
	}
}
