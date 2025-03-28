using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using BepInEx.Logging;
using REPOLib.Objects.Sdk;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Client;
using TwitchLib.Client.Models;
using TwitchLib.Client.Models.Builders;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using UnityEngine;

namespace Talkward;

using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;

public class TwitchIntegration
{
    private static ManualLogSource Logger => Plugin.Logger!;
    
    private readonly TwitchConfig _config;

    public event TwitchChatEventHandler? OnTwitchChatEvent;

    private readonly HttpClient _http = new();
    
    private readonly CancellationTokenSource _lifetime = new();
    
    private readonly TwitchAPI _api = new();
    
    private Timer? _twitchTokenRefreshTimer;
    
    private string? _twitchAuthPromptCode;

    private string? _twitchRefreshToken;

    private string? _currentTwitchUserId;
    
    private AtomicBoolean _syncThreadStarted = default;

    private readonly Thread _syncThread;
    
    private AtomicBoolean _chatThreadStarted = default;
    
    private readonly Thread _chatThread;
    
    private AtomicBoolean _authorized;

    public string TwitchAuthPromptCode => _twitchAuthPromptCode ?? "";
    public string TwitchRefreshToken => _twitchRefreshToken ?? "";
    public string? CurrentTwitchUserId => _currentTwitchUserId ?? "";
    public bool SyncThreadStarted => _syncThreadStarted;
    public bool ChatThreadStarted => _chatThreadStarted;
    
    public bool Authorized => _authorized;

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

    public TwitchIntegration(TwitchConfig cfg)
    {
        _config = cfg;

        var settings = _api.Settings;
        settings.ClientId = cfg.ClientId;
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
                    KeyValuePair.Create("client_id", cfg.ClientId),
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

            if (cfg.OpenAuthInBrowser)
                Application.OpenURL(verificationUri);

            _authorized.Set(false);
            var started = DateTime.Now;
            var expirationDate = DateTime.Now + TimeSpan.FromSeconds(expiresIn);
            
            do
            {
                await Task.Delay(intervalMs);
                resp = await _http.PostAsync("https://id.twitch.tv/oauth2/token",
                    new FormUrlEncodedContent([
                        KeyValuePair.Create("client_id", cfg.ClientId),
                        KeyValuePair.Create("device_code", deviceCode),
                        KeyValuePair.Create("scope", _twitchApiScopes),
                        KeyValuePair.Create("grant_type", "urn:ietf:params:oauth:grant-type:device_code")
                    ]));

                json = await resp.Content.ReadAsStringAsync();

                Logger.LogInfo($"Response JSON: {json}");

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
                        Logger.LogWarning($"Twitch DCF authorization still pending after {elapsed}s");
                        continue;
                    case "slow_down":
                        interval += 5;
                        Logger.LogWarning($"Twitch DCF slow down, increasing refresh interval to {interval}s");
                        break;
                    default:
                        Logger.LogError($"Twitch DCF error while polling for auth: {message}\n{json}");
                        _twitchAuthPromptCode = null;
                        return;
                }

                var status = jsDoc.Get<int>("status");
                switch (status)
                {
                    case > 300:
                        Logger.LogError($"Twitch DCF auth failed: {json}");
                        continue;
                }

                var accessToken = jsDoc.Get<string?>("access_token");
                var refreshToken = jsDoc.Get<string?>("refresh_token");
                expiresIn = jsDoc.Get<int>("expires_in");

                if (accessToken is null)
                {
                    // abnormal response
                    Logger.LogError($"Twitch DCF abnormal response: {json}");
                    continue;
                }

                _twitchAuthPromptCode = null;

                settings.AccessToken = accessToken;
                _twitchRefreshToken = refreshToken;

                _authorized.Set(true);
                if (!_syncThreadStarted)
                    _syncThread!.Start(this);
                Logger.LogInfo("Twitch DCF auth successful");

                _twitchTokenRefreshTimer = new Timer(
                    RefreshWorker,
                    this,
                    TimeSpan.FromSeconds(Math.Max(5, expiresIn - 2.5)),
                    Timeout.InfiniteTimeSpan);
            } while (!_authorized && DateTime.Now < expirationDate);

            /*if (success == false)
                log.Error("Twitch DCF auth failed");*/
        }

        //TwitchDeviceCodeFlowAuth().GetAwaiter().GetResult();
        ThreadPool.QueueUserWorkItem(_ => TwitchDeviceCodeFlowAuth().GetAwaiter().GetResult());

        _syncThread = new Thread(TwitchSyncWorker)
        {
            Name = "Twitch Sync Worker",
            IsBackground = true
        };
        _chatThread = new Thread(TwitchChatWorker)
        {
            Name = "Twitch Chat Worker",
            IsBackground = true
        };
    }

    private void Shutdown()
    {
        _lifetime.Cancel();
        _syncThread.Interrupt();
        _chatThread.Interrupt();
    }

    private static void RefreshWorker(object? o)
        => ((TwitchIntegration) o!).HandleRefresh();

    private void HandleRefresh()
    {
        if (_twitchRefreshToken is null) return;

        static async Task RefreshToken(TwitchIntegration self)
        {
            self._authorized.Set(false);

            Logger.LogInfo("Refreshing Twitch auth...");

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
                    Logger.LogError($"Twitch auth refresh error: {error}");
                    return;
            }

            var newAccessToken = jsDoc.Get<string?>("access_token");
            var newRefreshToken = jsDoc.Get<string?>("refresh_token");
            var expiresIn = jsDoc.Get<int>("expires_in");
            var refreshTime = TimeSpan.FromSeconds(Math.Max(5, expiresIn - 2));

            Logger.LogInfo("Twitch auth refresh successful");
            self._api.Settings.AccessToken = newAccessToken;
            self._twitchRefreshToken = newRefreshToken;
            self._twitchTokenRefreshTimer!.Change(refreshTime, Timeout.InfiniteTimeSpan);
            self._authorized.Set(true);
        }

        RefreshToken(this).GetAwaiter().GetResult();
    }

    private async Task<string?> GetCurrentTwitchUserId()
        => (await _api.Helix.Users.GetUsersAsync())
            .Users.FirstOrDefault()?.Id;

    private static void TwitchChatWorker(object? o)
        => ((TwitchIntegration) o!).HandleTwitchChat();

    private static void TwitchSyncWorker(object? o)
        => ((TwitchIntegration) o!).HandleTwitchSync();


    private void WaitForTwitchAuth(int msIntervals)
    {
        while (!_authorized)
            Thread.Sleep(msIntervals);

        for (;;)
        {
            var settings = _api.Settings;

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

        var ws = new WebSocketClient(clientOptions);
        var client = new TwitchClient(ws);

        client.Initialize(new ConnectionCredentials(chatter, _api.Settings.AccessToken));

        client.OnUserJoined += (_, e) =>
            _names.TryAdd(e.Username, (null, DateTime.Now));

        client.OnExistingUsersDetected += (_, e) =>
        {
            foreach (var user in e.Users)
                _names.TryAdd(user, (null, DateTime.Now));
        };

        client.OnUserLeft += (_, e)
            => _names.TryRemove(e.Username, out var _);

        client.OnMessageReceived += (_, e) =>
        {
            var msg = e.ChatMessage;
            _names[msg.Username] = (msg.DisplayName, DateTime.Now);

            var args = new TwitchChatEventArgs(msg);
            OnTwitchChatEvent?.Invoke(this, args);
        };

        client.OnJoinedChannel += (_, e)
            => chatter = e.BotUsername;

        client.OnLeftChannel += (_, e) =>
        {
            if (e.BotUsername == chatter) client.Disconnect();
        };

        client.Connect();

        client.JoinChannel(cfg.BroadcasterId ?? chatter);
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
                var (total, cursor, userLogins)
                    = await GetUserLogins(broadcaster, moderator);
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
                        = await GetUserLogins(broadcaster, moderator, after: cursor);
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

                    var resp = await _api.Helix.Users.GetUsersAsync(null, logins);

                    {
                        void UpdateChatter(string login, string displayName)
                        {
                            _names.AddOrUpdate(login,
                                static (k, x) => (x.displayName, DateTime.Now),
                                static (_, _, x) => x.self._config.DisplayNameTransforms is null
                                    ? (x.displayName, DateTime.Now)
                                    : (x.self._config.Transform(x.displayName), DateTime.Now),
                                (self: this, displayName));
                        }

                        foreach (var user in resp.Users)
                            UpdateChatter(user.Login, user.DisplayName);
                    }

                    logins.Clear();
                }
            }

            if (_authorized)
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

    private async Task<(int Total, string Cursor, IEnumerable<string> UserLogins)>
        GetUserLogins(string broadcaster, string moderator, string? after = null)
    {
        int total;
        string cursor;
        IEnumerable<string> userLogins;
        {
            var chattersResp = await _api.Helix.Chat.GetChattersAsync(broadcaster, moderator, after: after);
            total = chattersResp.Total;
            cursor = chattersResp.Pagination.Cursor;
            userLogins = chattersResp.Data.Select(x => x.UserLogin);
        }
        return (total, cursor, userLogins);
    }
}