using System.Collections.Concurrent;
using System.Net.Http;
using BepInEx.Logging;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TwitchLib.Api.Helix.Models.EventSub;
using TwitchLib.EventSub.Websockets.Core.Models;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets.Extensions;
using TwitchUserInfo = TwitchLib.Api.Helix.Models.Users.GetUsers.User;

namespace Talkward;

public abstract class TwitchIntegration
{
    private static readonly AuthScopes InvalidAuthScope = (AuthScopes) (-1);
    private static ManualLogSource Logger => Plugin.Logger!;

    private readonly TwitchConfig _config;

    public event TwitchChatEventHandler? OnTwitchChatEvent;

    private readonly HttpClient _http = new();

    private readonly CancellationTokenSource _lifetime = new();

    private readonly TwitchAPI _api = new();

    private Timer? _twitchTokenRefreshTimer;

    private string? _twitchAuthPromptCode;

    private string? _twitchRefreshToken;

    private AtomicBoolean _syncThreadStarted = default;

    private readonly Thread _syncThread;

    private AtomicBoolean _chatThreadStarted = default;

    private readonly Thread _chatThread;

    private AtomicBoolean _authorized;

    private EventSubWebsocketClient? _eventSubClient;

    public string TwitchAuthPromptCode => _twitchAuthPromptCode ?? "";
    public string TwitchRefreshToken => _twitchRefreshToken ?? "";

    public string? CurrentTwitchUserId
        => (_currentTwitchUserInfo ?? UpdateCurrentTwitchUserInfo().GetAwaiter().GetResult())?.Id;

    public string? CurrentTwitchUserLogin
        => (_currentTwitchUserInfo ?? UpdateCurrentTwitchUserInfo().GetAwaiter().GetResult())?.Login;

    public bool SyncThreadStarted => _syncThreadStarted;
    public bool ChatThreadStarted => _chatThreadStarted;

    public bool Authorized => _authorized;

    private ConcurrentDictionary<string, (string? DisplayName, DateTime Queried)> _names
        = new(StringComparer.OrdinalIgnoreCase);


    private readonly string _twitchApiScopes = string.Join(' ',
        "channel:read:subscriptions",
        "channel:read:vips",
        "channel:read:hype_train",
        "channel:read:charity",
        "channel:read:goals",
        "channel:read:redemptions",
        "moderator:read:followers",
        "moderator:read:guest_star",
        "moderator:read:shoutouts",
        "moderator:read:chatters",
        "user:bot", "channel:bot",
        "user:read:chat",
        "user:write:chat"
    );

    private string? _broadcasterId;

    private readonly IServiceProvider _serviceProvider
        = new ServiceCollection()
            .AddLogging(builder
                => builder
                    .ClearProviders()
                    .AddProvider(new PluginLoggerProvider()))
            .AddTwitchLibEventSubWebsockets()
            .BuildServiceProvider();

    private TwitchUserInfo? _currentTwitchUserInfo;

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
                return InvalidAuthScope;
            })
            .Where(a => a != InvalidAuthScope)
            .ToList();

        Logger.LogInfo($"Twitch API scopes: {string.Join(", ", settings.Scopes)}");

        async Task TwitchDeviceCodeFlowAuth()
        {
            var resp = await _http.PostAsync("https://id.twitch.tv/oauth2/device",
                new FormUrlEncodedContent([
                    KeyValuePair.Create("client_id", cfg.ClientId),
                    KeyValuePair.Create("scope", _twitchApiScopes)
                ]));

            var json = await resp.Content.ReadAsStringAsync();
            /*var jsDoc = JsonDocument.Parse(json).RootElement;
            var message = jsDoc.Get<string?>("message")
                          ?? jsDoc.Get<string?>("error");*/
            var jsDoc = Newtonsoft.Json.Linq.JObject.Parse(json);
            var message = jsDoc["message"]?.ToString()
                          ?? jsDoc["error"]?.ToString();

            switch (message)
            {
                case null:
                case "":
                    break;
                default:
                    //Logger.LogError($"Twitch DCF error: {message}\n{json}");
                    return;
            }

            /*var deviceCode = jsDoc.Get<string>("device_code");
            var userCode = jsDoc.Get<string>("user_code");
            var verificationUri = jsDoc.Get<string>("verification_uri");
            var expiresIn = jsDoc.Get<int>("expires_in");
            var interval = jsDoc.Get<int>("interval");*/
            var deviceCode = jsDoc["device_code"]?.ToString();
            var userCode = jsDoc["user_code"]?.ToString();
            var verificationUri = jsDoc["verification_uri"]?.ToString();
            var expiresIn = jsDoc["expires_in"]?.ToObject<int>() ?? 30;
            var interval = jsDoc["interval"]?.ToObject<int>() ?? 30;
            var intervalMs = interval * 1000;

            _twitchAuthPromptCode = userCode;

            if (cfg.OpenAuthInBrowser)
                OpenUri(verificationUri);
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

                /*jsDoc = JsonDocument.Parse(json).RootElement;
                message = jsDoc.Get<string?>("message")
                          ?? jsDoc.Get<string?>("error");*/
                // use Newtonsoft.Json instead for now
                jsDoc = Newtonsoft.Json.Linq.JObject.Parse(json);
                message = jsDoc["message"]?.ToString()
                          ?? jsDoc["error"]?.ToString();

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

                //var status = jsDoc.Get<int>("status");
                var status = jsDoc["status"]?.ToObject<int>() ?? 0;
                switch (status)
                {
                    case > 300:
                        Logger.LogError($"Twitch DCF auth failed: {json}");
                        continue;
                }

                //var accessToken = jsDoc.Get<string?>("access_token");
                //var refreshToken = jsDoc.Get<string?>("refresh_token");
                //expiresIn = jsDoc.Get<int>("expires_in");
                var accessToken = jsDoc["access_token"]?.ToString();
                var refreshToken = jsDoc["refresh_token"]?.ToString();
                expiresIn = jsDoc["expires_in"]?.ToObject<int>() ?? 0;

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
                if (!_chatThreadStarted)
                    _chatThread!.Start(this);
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

    protected abstract void OpenUri(string uri);

    private void Shutdown()
    {
        _lifetime.Cancel();
        _syncThread.Interrupt();
        _chatThread.Interrupt();

        // Disconnect EventSub client if it exists
        if (_eventSubClient != null)
        {
            _eventSubClient.DisconnectAsync().GetAwaiter().GetResult();
        }
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
            /*var jsDoc = JsonDocument.Parse(json).RootElement;
            var error = jsDoc.Get<string?>("error")
                        ?? jsDoc.Get<string?>("message");*/
            var jsDoc = Newtonsoft.Json.Linq.JObject.Parse(json);
            var error = jsDoc["error"]?.ToString()
                        ?? jsDoc["message"]?.ToString();
            switch (error)
            {
                case null:
                case "":
                    break;
                default:
                    Logger.LogError($"Twitch auth refresh error: {error}");
                    return;
            }

            /*var newAccessToken = jsDoc.Get<string?>("access_token");
            var newRefreshToken = jsDoc.Get<string?>("refresh_token");
            var expiresIn = jsDoc.Get<int>("expires_in");*/
            var newAccessToken = jsDoc["access_token"]?.ToString();
            var newRefreshToken = jsDoc["refresh_token"]?.ToString();
            var expiresIn = jsDoc["expires_in"]?.ToObject<int>() ?? 30;
            var refreshTime = TimeSpan.FromSeconds(Math.Max(5, expiresIn - 2));

            Logger.LogInfo("Twitch auth refresh successful");
            self._api.Settings.AccessToken = newAccessToken;
            self._twitchRefreshToken = newRefreshToken;
            self._twitchTokenRefreshTimer!.Change(refreshTime, Timeout.InfiniteTimeSpan);
            self._authorized.Set(true);
        }

        RefreshToken(this).GetAwaiter().GetResult();
    }

    private async Task<TwitchUserInfo?> UpdateCurrentTwitchUserInfo()
    {
        var userInfo = (await _api.Helix.Users.GetUsersAsync())
            .Users.FirstOrDefault();
        if (userInfo is not null) _currentTwitchUserInfo = userInfo;
        return userInfo;
    }

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


        _eventSubClient = _serviceProvider.GetRequiredService<EventSubWebsocketClient>();

        _chatThreadStarted.Set(true);

        // Subscribe to events we're interested in
        _eventSubClient.ChannelChatMessage += OnChannelChatMessage;
        _eventSubClient.ChannelCheer += OnChannelCheer;
        _eventSubClient.ChannelPointsAutomaticRewardRedemptionAdd += OnAutoRewardClaimed;
        _eventSubClient.ChannelPointsCustomRewardRedemptionAdd += OnManualRewardClaimStarted;
        _eventSubClient.ChannelPointsCustomRewardRedemptionUpdate += OnManualRewardClaimedOrRejected;
        
        _eventSubClient.WebsocketConnected += OnWebsocketConnected;
        _eventSubClient.WebsocketDisconnected += OnWebsocketDisconnected;
        _eventSubClient.ErrorOccurred += OnErrorOccurred;


        // try to make sure we have a chatter ID before trying to connect
        var chatterId = CurrentTwitchUserId;
        while (string.IsNullOrWhiteSpace(chatterId))
        {
            Thread.Sleep(1000);
            chatterId = CurrentTwitchUserId;
        }

        do
        {
            if (_eventSubClient.ConnectAsync().GetAwaiter().GetResult())
                break;
            Logger.LogError("Failed to connect to Twitch EventSub websocket");
            Thread.Sleep(5000);
        } while (!_lifetime.IsCancellationRequested);

        // Keep the thread alive
        while (!_lifetime.IsCancellationRequested)
        {
            Thread.Sleep(5000);
        }
    }

    private Task OnManualRewardClaimedOrRejected(object sender, ChannelPointsCustomRewardRedemptionArgs e)
    {
        try
        {
            var notification = e.Notification;
            var payload = notification.Payload;
            var msgEvent = payload.Event;
            if (msgEvent.Status != "fulfilled")
                return Task.CompletedTask;
            
            var timestamp = notification.Metadata.MessageTimestamp;

            // Store user display name
            _names[msgEvent.UserLogin] = (msgEvent.UserName, DateTime.Now);

            // Create a TwitchChatEventArgs and raise the event
            var chatEventArgs = new TwitchChatEventArgs(msgEvent, timestamp);
            Logger.LogMessage($"|Tw> [Auto-Reward] {msgEvent.UserName}: {msgEvent.UserInput}");

            OnTwitchChatEvent?.Invoke(this, chatEventArgs);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error processing chat message: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private Task OnManualRewardClaimStarted(object sender, ChannelPointsCustomRewardRedemptionArgs e)
    {
        try
        {
            var notification = e.Notification;
            var payload = notification.Payload;
            var msgEvent = payload.Event;
            if (msgEvent.Status != "fulfilled")
                return Task.CompletedTask;
            
            var timestamp = notification.Metadata.MessageTimestamp;

            // Store user display name
            _names[msgEvent.UserLogin] = (msgEvent.UserName, DateTime.Now);

            // Create a TwitchChatEventArgs and raise the event
            var chatEventArgs = new TwitchChatEventArgs(msgEvent, timestamp);
            Logger.LogMessage($"|Tw> [Manual Reward] {msgEvent.UserName}: {msgEvent.UserInput}");

            OnTwitchChatEvent?.Invoke(this, chatEventArgs);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error processing chat message: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private Task OnAutoRewardClaimed(object sender, ChannelPointsAutomaticRewardRedemptionArgs e)
    {
        try
        {
            var notification = e.Notification;
            var timestamp = notification.Metadata.MessageTimestamp;
            var payload = notification.Payload;
            var msgEvent = payload.Event;

            // Store user display name
            _names[msgEvent.UserLogin] = (msgEvent.UserName, DateTime.Now);

            // Create a TwitchChatEventArgs and raise the event
            var chatEventArgs = new TwitchChatEventArgs(msgEvent, timestamp);
            Logger.LogMessage($"|Tw> [Manual Reward] {msgEvent.UserName}: {msgEvent.Message}");

            OnTwitchChatEvent?.Invoke(this, chatEventArgs);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error processing chat message: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private Task OnChannelCheer(object sender, ChannelCheerArgs e)
    {
        try
        {
            var notification = e.Notification;
            var timestamp = notification.Metadata.MessageTimestamp;
            var payload = notification.Payload;
            var msgEvent = payload.Event;

            // Store user display name
            _names[msgEvent.UserLogin] = (msgEvent.UserName, DateTime.Now);

            // Create a TwitchChatEventArgs and raise the event
            var chatEventArgs = new TwitchChatEventArgs(msgEvent, timestamp);
            Logger.LogMessage($"|Tw> [Cheer] {msgEvent.UserName}: {msgEvent.Message}");

            OnTwitchChatEvent?.Invoke(this, chatEventArgs);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error processing chat message: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private Task OnChannelChatMessage(object sender, ChannelChatMessageArgs e)
    {
        try
        {
            var notification = e.Notification;
            var timestamp = notification.Metadata.MessageTimestamp;
            var payload = notification.Payload;
            var msgEvent = payload.Event;

            // Store user display name
            _names[msgEvent.ChatterUserLogin] = (msgEvent.ChatterUserName, DateTime.Now);

            // Create a TwitchChatEventArgs and raise the event
            var chatEventArgs = new TwitchChatEventArgs(msgEvent, timestamp);
            Logger.LogMessage($"|Tw> {msgEvent.ChatterUserName}: {msgEvent.Message.Text}");

            OnTwitchChatEvent?.Invoke(this, chatEventArgs);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error processing chat message: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private async Task OnWebsocketConnected(object sender, WebsocketConnectedArgs e)
    {
        Logger.LogInfo($"Connected to Twitch EventSub websocket. Session ID: {_eventSubClient?.SessionId}");
        if (e.IsRequestedReconnect)
            Logger.LogWarning("Twitch EventSub IsRequestedReconnect");

        var chatterId = CurrentTwitchUserId;

        while (string.IsNullOrWhiteSpace(chatterId))
        {
            Thread.Sleep(10);
            chatterId = CurrentTwitchUserId;
        }

        var subResponses = await Task.WhenAll(
            _api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.chat.message", "1",
                new Dictionary<string, string>
                    {["broadcaster_user_id"] = _broadcasterId ?? chatterId, ["user_id"] = chatterId},
                EventSubTransportMethod.Websocket,
                websocketSessionId: _eventSubClient!.SessionId,
                clientId: _api.Settings.ClientId,
                accessToken: _api.Settings.AccessToken),
            _api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.cheer", "1",
                new Dictionary<string, string> {["broadcaster_user_id"] = _broadcasterId ?? chatterId},
                EventSubTransportMethod.Websocket,
                websocketSessionId: _eventSubClient.SessionId,
                clientId: _api.Settings.ClientId,
                accessToken: _api.Settings.AccessToken),
            _api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.channel_points_automatic_reward_redemption.add", "2",
                new Dictionary<string, string> {["broadcaster_user_id"] = _broadcasterId ?? chatterId},
                EventSubTransportMethod.Websocket,
                websocketSessionId: _eventSubClient.SessionId,
                clientId: _api.Settings.ClientId,
                accessToken: _api.Settings.AccessToken),
            _api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.channel_points_custom_reward_redemption.add", "1",
                new Dictionary<string, string> {["broadcaster_user_id"] = _broadcasterId ?? chatterId},
                EventSubTransportMethod.Websocket,
                websocketSessionId: _eventSubClient.SessionId,
                clientId: _api.Settings.ClientId,
                accessToken: _api.Settings.AccessToken),
            _api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.channel_points_custom_reward_redemption.update", "1",
                new Dictionary<string, string> {["broadcaster_user_id"] = _broadcasterId ?? chatterId},
                EventSubTransportMethod.Websocket,
                websocketSessionId: _eventSubClient.SessionId,
                clientId: _api.Settings.ClientId,
                accessToken: _api.Settings.AccessToken)
        );

        var subsResponseCheck = await _api.Helix.EventSub.GetEventSubSubscriptionsAsync();

        var subs = subResponses
            .SelectMany(x => x.Subscriptions)
            .ToDictionary(x => $"{x.Type}:v{x.Version}");

        var subsCheck = subsResponseCheck.Subscriptions
            .ToDictionary(x => $"{x.Type}:v{x.Version}");

        foreach (var key in subs.Keys.Concat(subsCheck.Keys).Distinct())
        {
            if (!subs.ContainsKey(key))
                Logger.LogError($"No initial EventSub response for {key}");
            if (!subsCheck.ContainsKey(key))
                Logger.LogError($"EventSub {key} missing from check");
        }
    }

    private async Task OnWebsocketDisconnected(object sender, EventArgs e)
    {
        Logger.LogWarning("Disconnected from Twitch EventSub websocket. Scheduling reconnect...");
        ThreadPool.QueueUserWorkItem(_ =>
        {
            Thread.Sleep(250); // Wait before reconnecting
            Logger.LogWarning("Attempting to reconnect to Twitch EventSub websocket...");
            _eventSubClient!.ReconnectAsync()
                .GetAwaiter().GetResult();
        });
    }

    private Task OnErrorOccurred(object sender, ErrorOccuredArgs e)
    {
        Logger.LogError($"Twitch EventSub error: {e.Exception.Message}");
        return Task.CompletedTask;
    }

    private void HandleTwitchSync()
    {
        WaitForTwitchAuth(1000);

        var cfg = _config;
        _broadcasterId = cfg.BroadcasterId;
        if (string.IsNullOrWhiteSpace(_broadcasterId))
        {
            _broadcasterId = CurrentTwitchUserId;
            while (string.IsNullOrWhiteSpace(_broadcasterId))
            {
                Thread.Sleep(1000);
                _broadcasterId = CurrentTwitchUserId;
            }
        }

        if (string.IsNullOrWhiteSpace(_broadcasterId))
            return;

        var moderatorId = cfg.ModeratorId;
        if (string.IsNullOrWhiteSpace(moderatorId))
            moderatorId = _broadcasterId;

        do
        {
            async Task AsyncWork()
            {
                var broadcasterId = _broadcasterId;
                while (string.IsNullOrWhiteSpace(broadcasterId))
                {
                    await Task.Delay(100);
                    broadcasterId = _broadcasterId;
                }

                var (total, cursor, userLogins)
                    = await GetUserLogins(broadcasterId, moderatorId);
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
                        = await GetUserLogins(broadcasterId, moderatorId, after: cursor);
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
        if (string.IsNullOrWhiteSpace(broadcaster))
            throw new ArgumentNullException(nameof(broadcaster));
        if (string.IsNullOrWhiteSpace(moderator))
            throw new ArgumentNullException(nameof(moderator));

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