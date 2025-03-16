using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using REPOLib.Objects.Sdk;
using TwitchLib.Client;
using TwitchLib.Client.Models;
using TwitchLib.Client.Models.Builders;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace Talkward;

using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;

public class TwitchIntegration
{
    private const string ConfigFileName = "talkward.json";
    private TalkwardConfig _config = TalkwardConfig.Default;

    private HttpClient _http;
    private WebSocketClient _ws;
    private CancellationTokenSource _lifetime = new();
    private TwitchAPI _twitchApi = new();
    private TwitchClient _twitchClient;
    private Timer _twitchTokenRefreshTimer;
    private string? _twitchAuthPromptCode;
    private string? _twitchRefreshToken;

    private string? _currentTwitchUserId;

    private AtomicBoolean _twitchSyncThreadStarted;
    private Thread _twitchSyncThread;
    private AtomicBoolean _twitchChatThreadStarted;
    private Thread _twitchChatThread;
    private AtomicBoolean _twitchAuthorized;

    private ConcurrentDictionary<string, (string? DisplayName, DateTime Queried)> _names
        = new(StringComparer.OrdinalIgnoreCase);


    private readonly string _twitchApiScopes = string.Join(' ', [
        //"channel:read:subscriptions",
        //"moderator:read:followers",
        //"moderator:read:guest_star",
        //"moderator:read:shoutouts",
        "user:bot",
        "channel:bot",
        "user:read:chat",
        "moderator:read:chatters"
    ]);

    public event TwitchChatEventHandler? OnTwitchChatEvent;

    public TwitchIntegration()
    {
        if (File.Exists(ConfigFileName))
        {
            var jsonText = File.ReadAllText(ConfigFileName);
            _config = JsonSerializer.Deserialize<TalkwardConfig>(jsonText)
                      ?? TalkwardConfig.Default;
            _config.ApplyDefaults();
        }

        _http = new HttpClient();

        var settings = _twitchApi.Settings;
        settings.ClientId = _config.ClientId;
        settings.Scopes = _twitchApiScopes.Split(' ')
            .Select(s =>
            {
                var scopeSig = string.Join('_', s.Split(':'));
                if (Enum.TryParse(scopeSig, true, out AuthScopes scope))
                    return scope;
                if (Enum.TryParse($"helix_{scopeSig}", true, out scope))
                    return scope;
                //throw new NotImplementedException(s);
                return (AuthScopes) (-1); //ffs outdated TwitchLib
            })
            .Where(a => (int) a != -1)
            .ToList();

        async Task TwitchDeviceCodeFlowAuth()
        {
            var resp = await _http.PostAsync("https://id.twitch.tv/oauth2/device",
                new FormUrlEncodedContent([
                    KeyValuePair.Create("client_id", _config.ClientId),
                    KeyValuePair.Create("scope", _twitchApiScopes)
                ]));

            var json = await resp.Content.ReadAsStringAsync();
            var jsDoc = JsonDocument.Parse(json).RootElement;
            var message = jsDoc.Get<string?>("message")
                          ?? jsDoc.Get<string?>("error");

            switch (message)
            {
                case null:
                case "":
                    break;
                default:
                    //Logger.LogError($"Twitch DCF error: {message}\n{json}");
                    return;
            }

            var deviceCode = jsDoc.Get<string>("device_code");
            var userCode = jsDoc.Get<string>("user_code");
            var verificationUri = jsDoc.Get<string>("verification_uri");
            var expiresIn = jsDoc.Get<int>("expires_in");
            var interval = jsDoc.Get<int>("interval");
            var intervalMs = interval * 1000;

            _twitchAuthPromptCode = userCode;

            if (_config.OpenTwitchAuthInBrowser)
            {
                var verificationStartInfo = new ProcessStartInfo
                {
                    FileName = verificationUri,
                    UseShellExecute = true
                };

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    verificationStartInfo.FileName = verificationStartInfo.FileName
                        .Replace("&", "^&");

                Process.Start(verificationStartInfo)?.Dispose();
            }

            _twitchAuthorized.Set(false);
            bool success = false;
            var started = DateTime.Now;
            var expirationDate = DateTime.Now + TimeSpan.FromSeconds(expiresIn);
            //var log = LoggerInstance;
            do
            {
                await Task.Delay(intervalMs);
                resp = await _http.PostAsync("https://id.twitch.tv/oauth2/token",
                    new FormUrlEncodedContent([
                        KeyValuePair.Create("client_id", _config.ClientId),
                        KeyValuePair.Create("device_code", deviceCode),
                        KeyValuePair.Create("scope", _twitchApiScopes),
                        KeyValuePair.Create("grant_type", "urn:ietf:params:oauth:grant-type:device_code")
                    ]));

                json = await resp.Content.ReadAsStringAsync();

                //log.Msg($"Response JSON: {json}");

                jsDoc = JsonDocument.Parse(json).RootElement;

                message = jsDoc.Get<string?>("message")
                          ?? jsDoc.Get<string?>("error");

                switch (message)
                {
                    case null:
                    case "":
                        break;
                    case "authorization_pending":
                        var elapsed = Math.Round((DateTime.Now - started).TotalSeconds);
                        //log.Warning($"Twitch DCF authorization still pending after {elapsed}s");
                        continue;
                    case "slow_down":
                        interval += 5;
                        //log.Warning($"Twitch DCF slow down, increasing refresh interval to {interval}s");
                        break;
                    default:
                        //log.Error($"Twitch DCF error while polling for auth: {message}\n{json}");
                        _twitchAuthPromptCode = null;
                        return;
                }

                var status = jsDoc.Get<int>("status");
                switch (status)
                {
                    case > 300:
                        //log.Error($"Twitch DCF auth failed: {json}");
                        continue;
                }

                var accessToken = jsDoc.Get<string?>("access_token");
                var refreshToken = jsDoc.Get<string?>("refresh_token");
                expiresIn = jsDoc.Get<int>("expires_in");

                if (accessToken is null)
                {
                    // abnormal response
                    //log.Error($"Twitch DCF abnormal response: {json}");
                    continue;
                }

                _twitchAuthPromptCode = null;

                settings.AccessToken = accessToken;
                _twitchRefreshToken = refreshToken;

                success = true;
                _twitchAuthorized.Set(true);
                if (!_twitchSyncThreadStarted)
                    _twitchSyncThread.Start(this);
                //log.Msg("Twitch DCF auth successful");

                _twitchTokenRefreshTimer = new Timer(
                    RefreshWorker,
                    this,
                    TimeSpan.FromSeconds(Math.Max(5, expiresIn - 2.5)),
                    Timeout.InfiniteTimeSpan);
            } while (!_twitchAuthorized && DateTime.Now < expirationDate);

            /*if (success == false)
                log.Error("Twitch DCF auth failed");*/
        }

        //TwitchDeviceCodeFlowAuth().GetAwaiter().GetResult();
        ThreadPool.QueueUserWorkItem(_ => TwitchDeviceCodeFlowAuth().GetAwaiter().GetResult());

        _twitchSyncThread = new Thread(TwitchSyncWorker)
        {
            Name = "Twitch Sync Worker",
            IsBackground = true
        };
        _twitchChatThread = new Thread(TwitchChatWorker)
        {
            Name = "Twitch Chat Worker",
            IsBackground = true
        };
    }

    private void Shutdown()
    {
        _lifetime.Cancel();
        _twitchSyncThread.Interrupt();
        _twitchChatThread.Interrupt();
    }

    private static void RefreshWorker(object? o)
    {
        var self = (TwitchIntegration) o!;
        if (self._twitchRefreshToken is null) return;

        static async Task RefreshToken(TwitchIntegration self)
        {
            //var log = self.LoggerInstance;
            self._twitchAuthorized.Set(false);

            //log.Msg("Refreshing Twitch auth...");

            var refreshResp = await self._http.PostAsync("https://id.twitch.tv/oauth2/token",
                new FormUrlEncodedContent([
                    KeyValuePair.Create("client_id", self._config.ClientId),
                    KeyValuePair.Create("refresh_token", self._twitchRefreshToken),
                    KeyValuePair.Create("grant_type", "refresh_token")
                ]));

            var json = await refreshResp.Content.ReadAsStringAsync();
            var jsDoc = JsonDocument.Parse(json).RootElement;
            var error = jsDoc.Get<string?>("error")
                        ?? jsDoc.Get<string?>("message");
            switch (error)
            {
                case null:
                case "":
                    break;
                default:
                    //log.Error($"Twitch auth refresh error: {error}");
                    return;
            }

            var newAccessToken = jsDoc.Get<string?>("access_token");
            var newRefreshToken = jsDoc.Get<string?>("refresh_token");
            var expiresIn = jsDoc.Get<int>("expires_in");
            var refreshTime = TimeSpan.FromSeconds(Math.Max(5, expiresIn - 2));

            //log.Msg("Twitch auth refresh successful");
            self._twitchApi.Settings.AccessToken = newAccessToken;
            self._twitchRefreshToken = newRefreshToken;
            self._twitchTokenRefreshTimer.Change(refreshTime, Timeout.InfiniteTimeSpan);
            self._twitchAuthorized.Set(true);
        }

        RefreshToken(self).GetAwaiter().GetResult();
    }

    private async Task<string?> GetCurrentTwitchUserId()
    {
        var twitch = _twitchApi;
        var resp = await twitch.Helix.Users.GetUsersAsync();
        return resp.Users.FirstOrDefault()?.Id;
    }

    private static void TwitchChatWorker(object? o)
        => ((TwitchIntegration) o!).HandleTwitchChat();

    private static void TwitchSyncWorker(object? o)
        => ((TwitchIntegration) o!).HandleTwitchSync();


    private void WaitForTwitchAuth(int msIntervals)
    {
        while (!_twitchAuthorized)
            Thread.Sleep(msIntervals);

        for (;;)
        {
            var settings = _twitchApi.Settings;

            if (settings.ClientId is not null
                && settings.AccessToken is not null)
                break; // good to go

            Thread.Sleep(msIntervals);
        }
    }

    private void HandleTwitchChat()
    {
        WaitForTwitchAuth(1000);

        var cfg = _config;
        var chatter = _currentTwitchUserId
            ??= GetCurrentTwitchUserId().GetAwaiter().GetResult();

        if (chatter is null) return;

        var clientOptions = new ClientOptions
        {
            MessagesAllowedInPeriod = 100,
            ThrottlingPeriod = TimeSpan.FromSeconds(30),
        };

        _ws = new WebSocketClient(clientOptions);
        _twitchClient = new TwitchClient(_ws);
        
        _twitchClient.Initialize(new ConnectionCredentials(chatter, _twitchApi.Settings.AccessToken));

        _twitchClient.OnUserJoined += (_, e) =>
            _names.TryAdd(e.Username, (null, DateTime.Now));

        _twitchClient.OnExistingUsersDetected += (_, e) =>
        {
            foreach (var user in e.Users)
                _names.TryAdd(user, (null, DateTime.Now));
        };

        _twitchClient.OnUserLeft += (_, e)
            => _names.TryRemove(e.Username, out var _);

        _twitchClient.OnMessageReceived += (_, e) =>
        {
            var msg = e.ChatMessage;
            _names[msg.Username] = (msg.DisplayName, DateTime.Now);

            var args = new TwitchChatEventArgs(msg);
            OnTwitchChatEvent?.Invoke(this, args);
        };

        _twitchClient.OnJoinedChannel += (_, e)
            => chatter = e.BotUsername;

        _twitchClient.OnLeftChannel += (_, e) =>
        {
            if (e.BotUsername == chatter) _twitchClient.Disconnect();
        };

        _twitchClient.Connect();

        _twitchClient.JoinChannel(cfg.BroadcasterId ?? chatter);
    }

    private void HandleTwitchSync()
    {
        WaitForTwitchAuth(1000);

        var cfg = _config;
        var broadcaster = cfg.BroadcasterId ?? (_currentTwitchUserId
            ??= GetCurrentTwitchUserId().GetAwaiter().GetResult());
        if (broadcaster is null) return;

        var moderator = cfg.ModeratorId ?? broadcaster;

        do
        {
            async Task AsyncWork()
            {
                var twitch = _twitchApi;
                var (total, cursor, userLogins)
                    = await GetUserLogins(cfg, twitch, broadcaster, moderator);
                var pageCount = 1;

                void SyncWork()
                {
                    // update last seen for chatters
                    foreach (var userLogin in userLogins)
                    {
                        // convert to sentence case
                        _names.AddOrUpdate(userLogin,
                            static k => (null, DateTime.Now),
                            static (k, v) => (v.DisplayName, DateTime.Now));
                    }

                    // remove old chatters
                    var toRemove = new string[Math.Min(_names.Count, 32)];
                    {
                        rescan:
                        var toRemoveCount = 0;
                        foreach (var chatter in _names)
                        {
                            var queried = chatter.Value.Queried;
                            if (DateTime.Now - queried > TimeSpan.FromMinutes(30))
                                toRemove[toRemoveCount++] = chatter.Key;
                            if (toRemoveCount < toRemove.Length)
                                continue;

                            for (var i = 0; i < toRemoveCount; i++)
                                _names.TryRemove(toRemove[i], out _);
                            goto rescan;
                        }

                        for (var i = 0; i < toRemoveCount; i++)
                            _names.TryRemove(toRemove[i], out _);
                    }
                }

                for (;;)
                {
                    SyncWork();

                    if (_names.Count > cfg.EnoughChatters
                        || pageCount * 100 > total
                        || string.IsNullOrEmpty(cursor))
                        break;

                    (total, cursor, userLogins)
                        = await GetUserLogins(cfg, twitch, broadcaster, moderator, after: cursor);
                    pageCount++;
                }

                // update up to 100 display names at a time
                var logins = new List<string>(100);
                for (;;)
                {
                    foreach (var (name, (displayName, queried)) in _names)
                    {
                        if (displayName is not null) continue;
                        logins.Add(name);
                    }

                    if (logins.Count == 0) break;

                    var resp = await twitch.Helix.Users.GetUsersAsync(null, logins);

                    {
                        void UpdateChatter(string login, string displayName)
                        {
                            _names.AddOrUpdate(login,
                                static (k, x) => (x.displayName, DateTime.Now),
                                static (_, _, x) => x.self._config.DisplayNameTransforms is null
                                    ? (x.displayName, DateTime.Now)
                                    : (TalkwardConfigHelpers.Transform(x.self._config, x.displayName), DateTime.Now),
                                (self: this, displayName));
                        }

                        foreach (var user in resp.Users)
                            UpdateChatter(user.Login, user.DisplayName);
                    }

                    logins.Clear();
                }
            }

            if (_twitchAuthorized)
                AsyncWork().GetAwaiter().GetResult();


            try
            {
                if (_names.Count == 0)
                    Thread.Sleep(10000);
                else if (_names.Count >= cfg.EnoughChatters)
                    Thread.Sleep(60000);
                else
                    Thread.Sleep(30000);
            }
            catch (ThreadInterruptedException)
            {
                break;
            }
        } while (!_lifetime.IsCancellationRequested);
    }

    private static async Task<(int total, string cursor, IEnumerable<string> userLogins)>
        GetUserLogins(TalkwardConfig config, TwitchAPI twitchAPI, string broadcaster, string moderator,
            string? after = null)
    {
        int total;
        string cursor;
        IEnumerable<string> userLogins;
        {
            var chattersResp = await twitchAPI.Helix.Chat.GetChattersAsync(broadcaster, moderator, after: after);
            total = chattersResp.Total;
            cursor = chattersResp.Pagination.Cursor;
            userLogins = chattersResp.Data.Select(x => x.UserLogin);
        }
        return (total, cursor, userLogins);
    }
}

public delegate void TwitchChatEventHandler(object sender, TwitchChatEventArgs args);

public class TwitchChatEventArgs : EventArgs
{
    public ChatMessage Data { get; }

    public TwitchChatEventArgs(ChatMessage msg)
        => Data = msg;

    public string? UserName => Data.Username;
    public string? DisplayName => Data.DisplayName;
    public string? Message => Data.Message;
    public string? MessageId => Data.Id;
    public string? Channel => Data.Channel;
    public string? UserId => Data.UserId;
    public string? Color => Data.ColorHex;

    // paid messages
    public bool IsBitsReward => Data.Bits > 0;
    public int Bits => Data.Bits;

    // channel points
    public bool IsChannelPointsReward => IsHighlighted
                                         || IsSkippingSubMode
                                         || IsCustomReward;

    public bool IsHighlighted => Data.IsHighlighted;
    public bool IsSkippingSubMode => Data.IsSkippingSubMode;
    public bool IsCustomReward => Data.CustomRewardId != null;
    public string? CustomReward => Data.CustomRewardId;

    public bool IsMe => Data.IsMe;
    public bool IsBroadcaster => Data.IsBroadcaster;
    public bool IsSubscriber => Data.IsSubscriber;
    public bool IsModerator => Data.IsModerator;
    public bool IsTurbo => Data.IsTurbo;
    public bool IsStaff => Data.IsStaff;
    public bool IsReply => Data.ChatReply != null;
    public bool IsVip => Data.IsVip;
    public bool IsPartner => Data.IsPartner;
    public bool IsFirstMessage => Data.IsFirstMessage;

    private ChatReply? RepliedTo => Data.ChatReply;

    private string? _userType;
    public string? UserType => _userType ??= Data.UserType.ToString();

    private DateTimeOffset? _sent;

    public DateTimeOffset Sent
        => _sent ??= DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(Data.TmiSentTs));
}