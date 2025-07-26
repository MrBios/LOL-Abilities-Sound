using LeagueToolkit;
using LeagueToolkit.Core.Wad;
using LOL_abilities_sound;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

public class LcuApi
{
    private HttpClient _client;
    private bool connected = false;

    public bool Connect()
    {
        var (handler, port, token) = GetLcuConnection();
        if (handler == null)
        {
            connected = false;
            return false;
        }

        _client = new HttpClient(handler)
        {
            BaseAddress = new Uri($"https://127.0.0.1:{port}")
        };
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"riot:{token}"));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        connected = true;
        return true;
    }

    public async Task<(bool InGame, int championId, (string Name, string Square, string Splash))> GetCurrentGameStatusAsync()
    {
        var response = await _client.GetAsync("/lol-gameflow/v1/session");
        if (!response.IsSuccessStatusCode)
            return (false, 0, (null, null, null));

        var session = JObject.Parse(await response.Content.ReadAsStringAsync());

        var phase = session["phase"]?.ToString();

        if (phase != "InProgress")
            return (false, 0, (null, null, null));

        // Removed Clipboard.SetText() call that was causing STA thread issues
        // Debug output instead if needed for debugging
        System.Diagnostics.Debug.WriteLine($"Game session data: {session.ToString(Formatting.Indented)}");
        
        var teamOne = session["gameData"]?["teamOne"] as JArray;
        var teamTwo = session["gameData"]?["teamTwo"] as JArray;
        var allPlayers = teamOne?.Concat(teamTwo ?? new JArray()) ?? Enumerable.Empty<JToken>();
        var summonerIdStr = await GetCurrentSummonerIdAsync();
        if (!long.TryParse(summonerIdStr, out long mySummonerId))
            return (false, 0, (null, null, null));
        var champId = allPlayers
            .Where(player => player["summonerId"] != null && long.TryParse(player["summonerId"].ToString(), out long sid) && sid == mySummonerId)
            .Select(player => player["championId"]?.ToString())
            .FirstOrDefault(id => !string.IsNullOrEmpty(id));
        var championName = (await GetAllChampionsAsync())[champId];
        return (true, int.Parse(champId), championName);
    }

    public async Task<Dictionary<string, (string Name, string Square, string Splash)>> GetAllChampionsAsync()
    {
        Connect();
        if (!connected)
        {
            try
            {
                if (File.Exists("last_champions.json"))
                {
                    return JsonConvert.DeserializeObject<Dictionary<string, (string, string, string)>>(File.ReadAllText("last_champions.json"));
                }
                return new Dictionary<string, (string, string, string)>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading cached champions data: {ex.Message}");
                return new Dictionary<string, (string, string, string)>();
            }
        }
        else
        {
            var summonerId = await GetCurrentSummonerIdAsync();
            if (summonerId == null)
                return null;

            var response = await _client.GetAsync($"/lol-champions/v1/inventories/{summonerId}/champions-minimal");
            if (!response.IsSuccessStatusCode)
                return null;

            var champsArray = JArray.Parse(await response.Content.ReadAsStringAsync());

            var dict = new Dictionary<string, (string, string, string)>();
            foreach (var champ in champsArray)
            {
                if (champ["id"] != null && champ["name"] != null)
                {
                    if (int.TryParse(champ["id"]?.ToString(), out int id))
                    {
                        dict[id.ToString()] = (champ["name"]!.ToString(), champ["squarePortraitPath"]!.ToString(), champ["baseLoadScreenPath"]!.ToString());
                    }
                }
            }

            try
            {
                File.WriteAllText("last_champions.json", JsonConvert.SerializeObject(dict, Formatting.Indented));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving champions data to cache: {ex.Message}");
            }
            
            return dict;
        }
    }

    public static async Task<BitmapImage> LoadChampionImageFromWadAsync(string lcuAssetPath, bool splash)
    {
        try
        {
            var relativePath = lcuAssetPath
                .ToLower()
                .Replace("/lol-game-data/assets/", "/plugins/rcp-be-lol-game-data/global/default/", StringComparison.OrdinalIgnoreCase)
                .Replace("\\", "/")
                .TrimStart('/');

            var wadDirectory = Path.Combine(Config.LolDirectoryPath, "Plugins", "rcp-be-lol-game-data");
            if (!Directory.Exists(wadDirectory))
            {
                return splash ? Config.noPhotoSplashSource() : Config.noIconSource();
            }

            var wadFiles = Directory.GetFiles(wadDirectory, "*.wad", SearchOption.AllDirectories);

            foreach (var wadPath in wadFiles)
            {
                try
                {
                    using var wad = new WadFile(wadPath);
                    (bool, WadChunk) chunk2;
                    try
                    {
                        chunk2 = (true, wad.FindChunk(relativePath));
                    }
                    catch
                    {
                        chunk2 = (false, default);
                    }

                    if (chunk2.Item1)
                    {
                        var image = wad.OpenChunk(chunk2.Item2);
                        if (image != null)
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.StreamSource = image;
                            bitmap.EndInit();
                            bitmap.Freeze(); // Для использования в разных потоках WPF
                            image.Dispose();
                            return bitmap;
                        }
                    }
                }
                catch
                {
                    // Продолжаем поиск в других WAD файлах
                    continue;
                }
            }

            return splash ? Config.noPhotoSplashSource() : Config.noIconSource();
        }
        catch
        {
            return splash ? Config.noPhotoSplashSource() : Config.noIconSource();
        }
    }

    private async Task<string> GetCurrentSummonerIdAsync()
    {
        var response = await _client.GetAsync("/lol-summoner/v1/current-summoner");
        if (!response.IsSuccessStatusCode)
            return null;

        var summoner = JObject.Parse(await response.Content.ReadAsStringAsync());
        return summoner["summonerId"]?.ToString();
    }

    private static (HttpClientHandler, string port, string token) GetLcuConnection()
    {
        var proc = Process.GetProcessesByName("LeagueClientUx").FirstOrDefault();
        if (proc == null)
            return (null, null, null);

        var commandLine = GetCommandLine(proc);
        var port = ExtractValue(commandLine, "--app-port=");
        var token = ExtractValue(commandLine, "--remoting-auth-token=");

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        return (handler, port, token);
    }

    private static string ExtractValue(string cmd, string key)
    {
        var start = cmd.IndexOf(key) + key.Length;
        var end = cmd.IndexOf(' ', start) - 1;
        return end == -1 ? cmd.Substring(start) : cmd.Substring(start, end - start);
    }

    private static string GetCommandLine(Process process)
    {
        using var searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {process.Id}");
        foreach (ManagementObject obj in searcher.Get())
            return obj["CommandLine"]?.ToString();
        return null;
    }
}
