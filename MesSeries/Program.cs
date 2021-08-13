using JF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SeriesTvDb
{
	class Program
	{
		static Regex _reser = new Regex(@"(?<titre>.+) \((?<id>\d{1,})\)$");
		static List<Regex> _re = new List<Regex>{
			new Regex(@".{0,}[sS]aison (?<s>\d{1,2}) \((?<e>\d{1,2}).{0,}\).{0,}"),
			new Regex(@".{0,}[sS]aison (?<s>\d{1,2}) - (?<e>\d{1,2}).{0,}"),
			new Regex(@".{0,}[sS]aison (?<s>\d{1,2}) [EÉeé]pisode (?<e>\d{1,2}).{0,}"),
			new Regex(@".{0,}[sS](?<s>\d{1,2})[\-]{0,1} {0,}[eE](?<e>\d{1,2}).{0,}"),
			new Regex(@".{0,}[sS](?<s>\d{1,2}).[eE][pP](?<e>\d{2}).{0,}"),
			new Regex(@".{0,}(?<s>\d{1,2})[xX](?<e>\d{2}).{0,}"),
			new Regex(@".{0,}\[(?<s>\d{1,2})\]\[(?<e>\d{1,2})\].{0,}"),
			new Regex(@".{0,}(?<s>\d{1,2})(?<e>\d{2}).{0,}"),
		};
		static void Main(string[] args)
		{
			var api = new TheTvDb();
			var fin = false;
			while (!fin)
			{
				Console.Write("Série à rechercher : ");
				var cmd = Console.ReadLine();
				if (cmd == "q")
				{
					fin = true;
				}
				else if (cmd == "?")
				{
					Configure();
				}
				else if (cmd.Length == 0)
				{
					Console.Clear();
				}
				else
				{
					if (Int32.TryParse(cmd, out int id))
					{
						// Recherche par ID de série
						GetEpisodesFromSerie(api, id);
					}
					else
					{
						// Recherche par nom de série
						cmd = SearchSerie(api, cmd);
					}
				}
			}
		}

		private static void GetEpisodesFromSerie(TheTvDb api, int id)
		{
			var ser = api.SerieInfo(id);
			if (ser.data != null)
			{
				String nom;
				if (String.IsNullOrEmpty(ser.data.seriesName))
				{
					var seren = api.SerieInfo(id, "en");
					if (seren.data != null)
					{
						nom = seren.data.seriesName;
					}
					else
					{
						nom = "[Série sans nom]";
					}
				}
				else
				{
					nom = ser.data.seriesName;
				}
				nom = WebUtility.HtmlDecode(nom);
				if (String.IsNullOrEmpty(ser.data.firstAired))
				{
					FluentConsole.Yellow.Line(nom);
				}
				else
				{
					FluentConsole.Yellow.Line($"{nom} ({ser.data.firstAired})");
				}
				var r = api.EpisodesFormSerie(id).Where(o => o.airedSeason > 0).ToList();
				var ebs = new Dictionary<int, int>();
				var episodes = new Dictionary<int, string>();
				foreach (var ep in r)
				{
					if (ep.airedSeason > 0)
					{
						if (ebs.ContainsKey(ep.airedSeason))
						{
							ebs[ep.airedSeason]++;
						}
						else
						{
							ebs.Add(ep.airedSeason, 1);
						}
					}
				}
				var nbs = r.Select(o => o.airedSeason).Distinct().Count();
				var sc = r.GroupBy(o => o.airedSeason).ToDictionary(o => o.Key, o => o.Count());
				FluentConsole.Yellow.Line($"{r.Count} épisodes");
				foreach (var s in r.GroupBy(o => o.airedSeason))
				{
					FluentConsole.Green.Line($"Saison {s.Key,2} avec {s.Count(),2} épisodes");
					foreach (var e in s)
					{
						var key = (s.Key * 1000) + e.airedEpisodeNumber;
						string en = FormatEpisodeNumber(e, ebs);
						string titre;
						string overview = null;
						if (String.IsNullOrEmpty(e.episodeName))
						{
							var ei = api.EpisodeInfo(e.id, "en");
							titre = String.IsNullOrEmpty(ei.episodeName) ? $"Episode {e.airedEpisodeNumber}" : ei.episodeName.Trim();
							overview = ei.overview;
						}
						else
						{
							titre = e.episodeName.Trim();
						}
						titre = GetSafeFilename(titre);
						var nomep = $"[{s.Key:00}][{en}] - {titre}";
						if (episodes.ContainsKey(key))
						{
						}
						else
						{
							episodes.Add(key, nomep);
							FluentConsole.Yellow.Line(nomep);
							if (String.IsNullOrEmpty(overview))
							{
								overview = e.overview;
							}
							if (!String.IsNullOrEmpty(overview))
							{
								overview = WebUtility.HtmlDecode(overview);
								var a = overview.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
								foreach (var l in a)
								{
									ShowParaInLine(l, 80, 9 + en.Length);
								}
							}
						}
					}
					Console.WriteLine("------------");
				}
				Console.WriteLine($"{r.Count} épisodes");
				Console.Write("Chemin des fichiers à renommer : ");
				var path = Console.ReadLine();
				if (Directory.Exists(path))
				{
					foreach (var f in Directory.GetFiles(path))
					{
						try
						{
							var ext = Path.GetExtension(f);
							var name = Path.GetFileNameWithoutExtension(f);
							int sa, ep;
							if (AnalyzeFilename(name, out sa, out ep))
							{
								var key = (sa * 1000) + ep;
								if (episodes.ContainsKey(key))
								{
									var newname = Path.Combine(Path.GetDirectoryName(f), episodes[key] + ext);
									File.Move(f, newname);
								}
							}
							else
							{
								Console.WriteLine("Fichier non décodé : {0}", f);
							}

						}
						catch (Exception ex)
						{
							FluentConsole.Red.Line($"Exception GetEpisodesFromSerie({f}) : {ex.Message}");
						}
					}
				}
			}
		}

		private static string FormatEpisodeNumber(JF.Episode ep, Dictionary<int, int> x)
		{
			return ep.airedEpisodeNumber.ToString().PadLeft(Math.Max(x[ep.airedSeason].ToString().Length, 2), '0');
		}

		private static string SearchSerie(TheTvDb api, string cmd)
		{
			if (cmd.StartsWith("."))
			{
				cmd = cmd.Substring(1);
			}
			var b = cmd.Split(',');
			string lang;
			if (b.Length == 2)
			{
				lang = b[1].Trim();
			}
			else
			{
				lang = "fr";
			}
			var r = api.Search(b[0].Trim(), lang);
			if (r.data != null)
			{
				foreach (var s in r.data)
				{
					var nom = String.IsNullOrEmpty(s.seriesName) ? $"Série sans nom" : s.seriesName;
					FluentConsole.Green.Line($"Série     : {nom}");
					if (!String.IsNullOrEmpty(s.overview))
					{
						var a = s.overview.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
						foreach (var l in a)
						{
							ShowParaInLine(l, 80, 12);
						}
					}
					Console.WriteLine($"Date      : {s.firstAired}");
					FluentConsole.White.Line($"ID        : {s.id}");
					Console.WriteLine($"Status    : {s.status}");
					if (!String.IsNullOrEmpty(s.banner))
					{
						Console.WriteLine($"Bannière  : {api.ImagePrefix}{s.banner}");
					}
					Console.WriteLine("------------");
				}
			}

			return cmd;
		}

		private static List<string> SplitParaInLine(string l, int width = 80)
		{
			List<string> lines = new List<string>();
			StringBuilder line = new StringBuilder();
			foreach (Match word in Regex.Matches(l, @"\S+", RegexOptions.ECMAScript))
			{
				if (word.Value.Length + line.Length + 1 > width)
				{
					lines.Add(line.ToString());
					line.Length = 0;
				}
				line.Append(String.Format("{0} ", word.Value));
			}

			if (line.Length > 0)
				lines.Add(line.ToString());
			return lines;
		}

		private static void ShowParaInLine(string l, int width = 80, int margin = 0)
		{
			var m = new String(' ', margin);
			var lines = SplitParaInLine(l, width);
			foreach (var line in lines)
			{
				FluentConsole.White.Line($"{m}{line}");
			}
		}

		public static string GetSafeFilename(string filename)
		{
			return WebUtility.HtmlDecode(string.Join("_", filename.Split(Path.GetInvalidFileNameChars())));
		}

		private static bool AnalyzeFilename(string f, out int sa, out int ep)
		{
			bool ret = false;
			sa = 0;
			ep = 0;
			foreach (var re in _re)
			{
				var m = re.Match(f);
				if (m.Success)
				{
					sa = Int32.Parse(m.Groups["s"].Value);
					ep = Int32.Parse(m.Groups["e"].Value);
					ret = true;
					break;
				}
			}
			return ret;
		}

		private static void Configure()
		{
			FluentConsole.Yellow.Line($"Configuration de l'application :");
			FluentConsole.White.Line($"Configuration de l'application :");
		}
	}
}
