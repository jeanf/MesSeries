using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace JF
{
	/// <summary>
	/// 
	/// </summary>
	public class TheTvDb
	{
		#region Constantes
		private const string _apikey = "06A6C9B4677609A5";
		private const string _userkey = "4KY7HFWJ0W1K0DGZ";
		private const string _username = "jeanfevre";
		//private const string _apikey = "06A6C9B4677609A5";
		//private const string _userkey = "4KY7HFWJ0W1K0DGZ";
		//private const string _username = "jeanfevre";
		private const string _url = "https://api.thetvdb.com/";
		private const string _imageprefix = "http://thetvdb.com/banners/";
		#endregion

		#region Variables privées
		private RestClient _client = null;
		private string _token = String.Empty;
		#endregion

		#region Propriétés publiques
		/// <summary>
		/// 
		/// </summary>
		public string ImagePrefix { get; set; }
		#endregion

		#region Constructeurs
		/// <summary>
		/// 
		/// </summary>
		public TheTvDb()
		{
			Init();
		}
		#endregion

		#region Méthodes privées
		/// <summary>
		/// 
		/// </summary>
		private void Init()
		{
			ImagePrefix = _imageprefix;
			_client = new RestClient
			{
				BaseUrl = new Uri(_url)
			};
			Login(_apikey, _userkey, _username);
		}
		#endregion

		#region Méthodes publiques
		/// <summary>
		/// 
		/// </summary>
		/// <param name="apikey"></param>
		/// <param name="userkey"></param>
		/// <param name="username"></param>
		/// <returns></returns>
		public string Login(string apikey, string userkey, string username)
		{
			var l = new Logon { apikey = apikey, userkey = userkey, username = username };
			return Login(l);
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="l"></param>
		/// <returns></returns>
		public string Login(Logon l)
		{
			string ret;
			var json = JsonConvert.SerializeObject(l);
			var request = new RestRequest("/login", Method.POST);
			request.AddParameter("application/json; charset=utf-8", json, ParameterType.RequestBody);
			request.RequestFormat = DataFormat.Json;
			var response = _client.Execute<Logged>(request);
			if (response.IsSuccessful)
			{
				ret = response.Data.token;
				_token = ret;
				Console.WriteLine($"Token = {_token}");
			}
			else
			{
				ret = null;
				Console.WriteLine($"Erreur au login : [{response.StatusCode}] {response.StatusDescription}");
			}
			return ret;
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="nom"></param>
		/// <param name="lang"></param>
		/// <returns></returns>
		public Series Search(string nom, string lang = "fr")
		{
			Series ret;
			var nomencoded = WebUtility.UrlEncode(nom);
			var request = new RestRequest($"search/series?name={nomencoded}", Method.GET);
			request.AddHeader("Authorization", $"Bearer {_token}");
			request.AddHeader("Accept-Language", lang);
			request.AddHeader("Accept-Encoding", "application/json");
			var response = _client.Execute(request);
			if (response.IsSuccessful)
			{
				ret = JsonConvert.DeserializeObject<Series>(response.Content);
				//Console.WriteLine($"{ret.data.Count} série(s)");
			}
			else
			{
				ret = new Series();
				Console.WriteLine($"Erreur : [{response.StatusCode}] {response.StatusDescription}");
				Console.WriteLine($"Erreur : [{response.ErrorException}] {response.ErrorMessage}");
			}
			return ret;
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="id"></param>
		/// <param name="lang"></param>
		/// <returns></returns>
		public Serie1 SerieInfo(int id, string lang = "fr")
		{
			Serie1 ret;
			var request = new RestRequest($"series/{id}", Method.GET);
			request.AddHeader("Authorization", $"Bearer {_token}");
			request.AddHeader("Accept-Language", lang);
			request.AddHeader("Accept-Encoding", "application/json");
			var response = _client.Execute(request);
			if (response.IsSuccessful)
			{
				ret = JsonConvert.DeserializeObject<Serie1>(response.Content);
			}
			else
			{
				ret = new Serie1();
				Console.WriteLine($"Erreur : [{response.StatusCode}] {response.StatusDescription}");
				Console.WriteLine($"Erreur : [{response.ErrorException}] {response.ErrorMessage}");
			}
			return ret;
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="id"></param>
		/// <param name="lang"></param>
		/// <returns></returns>
		public List<Episode> EpisodesFormSerie(int id, string lang = "fr")
		{
			var ret = new List<Episode>();
			var fin = false;
			var page = 1;
			do
			{
				var request = new RestRequest($"series/{id}/episodes?page={page}", Method.GET);
				request.AddHeader("Authorization", $"Bearer {_token}");
				request.AddHeader("Accept-Language", lang);
				request.AddHeader("Accept-Encoding", "application/json");
				var response = _client.Execute(request);
				if (response.IsSuccessful)
				{
					var r = JsonConvert.DeserializeObject<Episodes>(response.Content);
					ret.AddRange(r.data);
					if (r.links.next == null)
					{
						fin = true;
					}
					else
					{
						page++;
					}
				}
				else
				{
					Console.WriteLine($"Erreur : [{response.StatusCode}] {response.StatusDescription}");
					Console.WriteLine($"Erreur : [{response.ErrorException}] {response.ErrorMessage}");
					fin = true;
				}
			} while (!fin);
			return ret.OrderBy(o => o.airedSeason).ThenBy(o => o.airedEpisodeNumber).ToList();
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="id"></param>
		/// <param name="lang"></param>
		/// <returns></returns>
		public Episode EpisodeInfo(int id, string lang = "fr")
		{
			Episode ret = null;
			var request = new RestRequest($"episodes/{id}", Method.GET);
			request.AddHeader("Authorization", $"Bearer {_token}");
			request.AddHeader("Accept-Language", lang);
			request.AddHeader("Accept-Encoding", "application/json");
			var response = _client.Execute(request);
			if (response.IsSuccessful)
			{
				var r = JsonConvert.DeserializeObject<EpisodeInfo>(response.Content);
				ret = r.data;
			}
			else
			{
				Console.WriteLine($"Erreur : [{response.StatusCode}] {response.StatusDescription}");
				Console.WriteLine($"Erreur : [{response.ErrorException}] {response.ErrorMessage}");
			}
			return ret;
		}
		#endregion

		#region Méthodes statiques
		#endregion

		#region Méthodes surclassées
		#endregion
	}
}
