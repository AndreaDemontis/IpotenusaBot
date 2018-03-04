using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using System.Text.RegularExpressions;
using DotNetOpenAuth.Messaging;
using DotNetOpenAuth.OAuth2;
using RestSharp;
using RestSharp.Authenticators;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ipotenusa.Services
{
	public class AniList
	{

		private readonly Random _rand;
		private readonly IConfigurationRoot _config;

		/// <summary>
		/// Makes a new instance of <see cref="MyAnimeList"/> api class.
		/// </summary>
		public AniList(Random rand, IConfigurationRoot config)
		{
			_rand = rand;
			_config = config;
		}


		public async Task<JArray> SearchStaff(string name)
		{
			name = Uri.EscapeDataString(name);
			string response = await MakeRequest("staff/search/" + name);

			return JArray.Parse(response);
		}

		public async Task<JObject> StaffInfo(long id)
		{
			string response = await MakeRequest("staff/" + id.ToString() + "/page");

			return JObject.Parse(response);
		}

		public async Task<JObject> AnimeInfo(long id)
		{
			string response = await MakeRequest("anime/" + id.ToString());

			return JObject.Parse(response);
		}

		private async Task<string> MakeRequest(string endpoint)
		{
			var AuthEndpoint = new AuthorizationServerDescription
			{
				TokenEndpoint = new Uri("https://anilist.co/api/auth/access_token"),
				ProtocolVersion = ProtocolVersion.V20
			};

			var clientid = _config["anilist:clientId"];
			var clientsecret = _config["anilist:clientSecret"];

			WebServerClient client = new WebServerClient(AuthEndpoint, clientid, clientsecret);

			// - Get access token
			var token = client.GetClientAccessToken();

			// - Request
			RestRequest request = new RestRequest() { Method = Method.GET };
			request.AddHeader("Authorization", "Bearer " + token.AccessToken);
			request.AddHeader("Content-Type", "application/json");

			// - Rest request setting
			var restclient = new RestClient("https://anilist.co/api/" + endpoint);
			restclient.Authenticator = new OAuth2AuthorizationRequestHeaderAuthenticator(token.AccessToken);

			// - Send request
			var response = await restclient.ExecuteTaskAsync(request);

			if (response.StatusCode != System.Net.HttpStatusCode.OK)
			{
				return null;
			}

			return Regex.Unescape(response.Content);
		}
	}
}
