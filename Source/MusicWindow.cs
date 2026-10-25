using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Orbit.Controls;
#if WINDOWS
using NAudio.CoreAudioApi;
#endif
#if WINDOWS
using Windows.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;
#endif

namespace Orbit;

public sealed class MusicWindow : SettingsWindowBase
{
#if WINDOWS
    private static GlobalSystemMediaTransportControlsSession? _activeWindowsSession;
    private static string? _cachedWindowsArtworkKey;
    private static byte[]? _cachedWindowsArtwork;
#endif
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly TextBlock _trackText;
    private readonly TextBlock _artistText;
    private readonly TextBlock _albumText;
    private readonly TextBlock _statusText;
    private readonly Button _playPauseButton;
    private readonly Button _shuffleButton;
    private readonly Button _repeatButton;
    private readonly Image _albumArt;
    private readonly OrbitIcon _artPlaceholder;
    private readonly MusicSlider _positionSlider;
    private readonly MusicSlider _volumeSlider;
    private readonly TextBlock _positionText;
    private readonly TextBlock _durationText;
    private readonly DispatcherTimer _seekTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _volumeTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private string? _artUrl;
    private string? _loadedArtworkKey;
    private string? _artworkTrackKey;
    private bool _isPlaying;
    private bool _busy;
    private bool _updatingControls;
    private bool _shuffle;
    private string _repeat = "None";
    private string _trackId = "";
    private MediaSession _session = MediaSession.Empty;

    public MusicWindow()
    {
        Height = 360;
        MinHeight = 360;
        MaxHeight = 360;
        _trackText = new TextBlock { Text = "No media session", FontSize = 15, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        _artistText = new TextBlock { Text = "Start playback in a music app", FontSize = 12, Foreground = OrbitTheme.Muted, TextTrimming = TextTrimming.CharacterEllipsis };
        _albumText = new TextBlock { Text = "", FontSize = 11, Foreground = OrbitTheme.Muted, TextTrimming = TextTrimming.CharacterEllipsis };
        _statusText = new TextBlock { Text = "Looking for a player…", FontSize = 10, Foreground = OrbitTheme.Muted, TextTrimming = TextTrimming.CharacterEllipsis };
        _albumArt = new Image { Width = 66, Height = 66, Stretch = Avalonia.Media.Stretch.UniformToFill };
        _artPlaceholder = new OrbitIcon(OrbitIconKind.Music) { Width = 30, Height = 30, Stroke = OrbitTheme.Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _shuffleButton = CreateButton(OrbitIconKind.Shuffle, "Shuffle", 34);
        _playPauseButton = CreateButton(OrbitIconKind.Play, "Play or pause", 44);
        _repeatButton = CreateButton(OrbitIconKind.Repeat, "Repeat", 34);
        var previous = CreateButton(OrbitIconKind.Previous, "Previous track", 38);
        var next = CreateButton(OrbitIconKind.Next, "Next track", 38);
        _positionSlider = new MusicSlider { Minimum = 0, Maximum = 1, IsEnabled = false, Height = 14, VerticalAlignment = VerticalAlignment.Center };
        _volumeSlider = new MusicSlider { Minimum = 0, Maximum = 100, Value = 70, Width = 104, Height = 16, VerticalAlignment = VerticalAlignment.Center, IsEnabled = false };
        _positionText = new TextBlock { Text = "0:00", FontSize = 10, Foreground = OrbitTheme.Muted, VerticalAlignment = VerticalAlignment.Center };
        _durationText = new TextBlock { Text = "0:00", FontSize = 10, Foreground = OrbitTheme.Muted, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };

        previous.Click += async (_, _) => await SendCommandAsync("previous");
        _playPauseButton.Click += async (_, _) => await SendCommandAsync("play-pause");
        next.Click += async (_, _) => await SendCommandAsync("next");

        _shuffleButton.Click += async (_, _) => await SendCommandAsync("shuffle");
        _repeatButton.Click += async (_, _) => await SendCommandAsync("repeat");
        _positionSlider.ValueChanged += (_, _) =>
        {
            if (!_updatingControls)
            {
                _positionText.Text = FormatTime((long)_positionSlider.Value);
                _seekTimer.Stop();
                _seekTimer.Start();
            }
        };
        _volumeSlider.ValueChanged += (_, _) =>
        {
            if (!_updatingControls)
            {
                _volumeTimer.Stop();
                _volumeTimer.Start();
            }
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 8,
            Children = { _shuffleButton, previous, _playPauseButton, next, _repeatButton }
        };
        var metadata = new StackPanel
        {
            Spacing = 3,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _trackText, _artistText, _albumText }
        };
        var content = new StackPanel
        {
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 4, 0, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("76,*"),
                    Children =
                    {
                        new Border
                        {
                            Width = 70, Height = 70, CornerRadius = new Avalonia.CornerRadius(8),
                            BorderBrush = OrbitTheme.Line, BorderThickness = new Avalonia.Thickness(1),
                            Background = OrbitTheme.Raised, ClipToBounds = true,
                            Child = new Grid { Children = { _albumArt, _artPlaceholder } }
                        },
                        InColumn(new Border { Padding = new Avalonia.Thickness(5, 0, 0, 0), Child = metadata }, 1)
                    }
                },
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                    Children = { _positionText, InColumn(new Border { Height = 14, VerticalAlignment = VerticalAlignment.Center, Child = _positionSlider }, 1), InColumn(new Border { Child = _durationText }, 2) }
                },
                buttons,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 7,
                    Children = { new OrbitIcon(OrbitIconKind.Volume) { Width = 16, Height = 16, Stroke = OrbitTheme.Muted, VerticalAlignment = VerticalAlignment.Center }, _volumeSlider }
                },
                _statusText
            }
        };
        SetContent("Music controls", content);
        _refreshTimer.Tick += async (_, _) => await RefreshSessionAsync();
        _seekTimer.Tick += async (_, _) => { _seekTimer.Stop(); await SendCommandAsync("seek"); };
        _volumeTimer.Tick += async (_, _) => { _volumeTimer.Stop(); await SendCommandAsync("volume"); };
        Opened += async (_, _) => { _refreshTimer.Start(); await RefreshSessionAsync(); };
        Closed += (_, _) => { _refreshTimer.Stop(); _seekTimer.Stop(); _volumeTimer.Stop(); };
    }

    private static Button CreateButton(OrbitIconKind iconKind, string tooltip, double size)
    {
        var button = new Button
        {
            Content = new OrbitIcon(iconKind) { Width = 18, Height = 18, Stroke = OrbitTheme.Text },
            Width = size,
            Height = size,
            Background = OrbitTheme.Raised,
            BorderBrush = OrbitTheme.Line,
            BorderThickness = new Avalonia.Thickness(1),
            Foreground = OrbitTheme.Text,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(button, tooltip);
        return button;
    }

    private static Control InColumn(Control control, int column)
    {
        Grid.SetColumn(control, column);
        return control;
    }

    private async Task RefreshSessionAsync()
    {
        if (_busy || !IsVisible) return;
        _busy = true;
        try
        {
            var session = await ReadSessionAsync();
            _session = session;
            SetText(_trackText, session.Track);
            SetText(_artistText, session.Artist);
            SetText(_albumText, session.Album.Length > 0 ? $"{session.Album}{(session.TrackNumber > 0 ? $" · Track {session.TrackNumber}" : "")}" : session.TrackNumber > 0 ? $"Track {session.TrackNumber}" : "");
            if (session.IsPlaying.HasValue)
                _isPlaying = session.IsPlaying.Value;
            var playPauseKind = _isPlaying ? OrbitIconKind.Pause : OrbitIconKind.Play;
            UpdatePlayPauseIcon(playPauseKind);
            _playPauseButton.IsEnabled = session.Available;
            foreach (var button in ((StackPanel)_playPauseButton.Parent!).Children.OfType<Button>())
                button.IsEnabled = session.Available;
            _shuffleButton.IsEnabled = session.CanShuffle;
            _repeatButton.IsEnabled = session.CanRepeat;
            _positionSlider.IsEnabled = session.DurationMs > 0;
            _volumeSlider.IsEnabled = session.Volume.HasValue;
            _shuffle = session.Shuffle;
            _repeat = session.LoopStatus;
            _trackId = session.TrackId;
            ((OrbitIcon)_shuffleButton.Content!).Stroke = _shuffle ? OrbitTheme.Accent : OrbitTheme.Text;
            ((OrbitIcon)_repeatButton.Content!).Stroke = _repeat != "None" ? OrbitTheme.Accent : OrbitTheme.Text;
            _updatingControls = true;
            _positionSlider.Maximum = Math.Max(session.DurationMs, 1);
            _positionSlider.Value = Math.Clamp(session.PositionMs, 0, Math.Max(session.DurationMs, 1));
            _volumeSlider.Value = Math.Clamp(session.Volume.GetValueOrDefault() * 100, 0, 100);
            _updatingControls = false;
            _positionText.Text = FormatTime(session.PositionMs);
            _durationText.Text = FormatTime(session.DurationMs);
            SetText(_statusText, session.Available
                ? session.IsPlaying.HasValue
                    ? $"{session.Player} · {(session.IsPlaying.Value ? "Playing" : "Paused")}"
                    : $"{session.Player} · playback state unavailable"
                : "No active media player found. Start playback in any app that publishes a system media session.");
            var trackArtworkKey = $"{session.Player}|{session.Track}";
            if (trackArtworkKey != _artworkTrackKey)
            {
                _artworkTrackKey = trackArtworkKey;
                _loadedArtworkKey = null;
                _artUrl = null;
                _albumArt.Source = null;
                _artPlaceholder.IsVisible = true;
            }

            var artworkKey = session.ArtData is not null
                ? $"{trackArtworkKey}|native"
                : !string.IsNullOrWhiteSpace(session.ArtUrl) ? $"{trackArtworkKey}|{session.ArtUrl}" : null;
            if (artworkKey is not null && artworkKey != _loadedArtworkKey)
            {
                _loadedArtworkKey = artworkKey;
                _artUrl = session.ArtUrl;
                await LoadArtworkAsync(session.ArtData is null ? _artUrl : null, session.ArtData);
            }
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Media controls unavailable: {ex.Message}";
        }
        finally { _busy = false; }
    }

    private static void SetText(TextBlock textBlock, string text)
    {
        if (textBlock.Text != text)
            textBlock.Text = text;
    }

    private void UpdatePlayPauseIcon(OrbitIconKind kind)
    {
        var icon = (OrbitIcon)_playPauseButton.Content!;
        if (icon.Kind != kind)
            icon.Kind = kind;
        icon.Fill = Brushes.Transparent;
        icon.Stroke = OrbitTheme.Text;
    }

    private async Task LoadArtworkAsync(string? url, byte[]? artwork = null)
    {
        _albumArt.Source = null;
        _artPlaceholder.IsVisible = true;
        if (artwork is null && string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Bitmap bitmap;
            if (artwork is not null)
            {
                using var memory = new MemoryStream(artwork);
                bitmap = new Bitmap(memory);
            }
            else if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                await using var stream = File.OpenRead(uri.LocalPath);
                bitmap = new Bitmap(stream);
            }
            else if (Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.Scheme is "http" or "https")
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
                await using var stream = await client.GetStreamAsync(uri);
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory);
                memory.Position = 0;
                bitmap = new Bitmap(memory);
            }
            else return;
            _albumArt.Source = bitmap;
            _artPlaceholder.IsVisible = false;
        }
        catch { _albumArt.Source = null; _artPlaceholder.IsVisible = true; }
    }

    private async Task SendCommandAsync(string command)
    {
        try
        {
            var sent = await SendPlatformCommandAsync(command, _session, _positionSlider.Value, _volumeSlider.Value, _shuffle, _repeat);
            if (!sent)
                throw new InvalidOperationException("The player did not accept the playback command.");
            if (OperatingSystem.IsWindows() && _activeWindowsSession is null && command == "play-pause")
            {
                _isPlaying = !_isPlaying;
                UpdatePlayPauseIcon(_isPlaying ? OrbitIconKind.Pause : OrbitIconKind.Play);
            }
            await Task.Delay(250);
            await RefreshSessionAsync();
        }
        catch (Exception ex) { _statusText.Text = $"Could not control the player: {ex.Message}"; }
    }

    private static async Task<MediaSession> ReadSessionAsync() => OperatingSystem.IsWindows() ? await ReadWindowsSessionAsync() : await Task.Run(ReadLinuxSession);

    private static async Task<MediaSession> ReadWindowsSessionAsync()
    {
#if WINDOWS
        try
        {
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var sessions = manager.GetSessions();
        var session = sessions.FirstOrDefault(item => item.SourceAppUserModelId.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
            ?? sessions.FirstOrDefault(item => item.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            ?? manager.GetCurrentSession();
        if (session is null)
        {
            _activeWindowsSession = null;
            return MediaSession.Empty;
        }
        _activeWindowsSession = session;
        var properties = await session.TryGetMediaPropertiesAsync();
        var playback = session.GetPlaybackInfo();
        var controls = playback.Controls;
        var timeline = session.GetTimelineProperties();
        var artworkKey = $"{session.SourceAppUserModelId}|{properties.Title}|{properties.Artist}|{properties.AlbumTitle}";
        byte[]? artwork = artworkKey == _cachedWindowsArtworkKey ? _cachedWindowsArtwork : null;
        if (artworkKey != _cachedWindowsArtworkKey)
        {
            artwork = null;
            if (properties.Thumbnail is { } thumbnail)
            {
                using var stream = await thumbnail.OpenReadAsync();
                if (stream.Size is > 0 and <= 16 * 1024 * 1024)
                {
                    using var reader = new DataReader(stream.GetInputStreamAt(0));
                    await reader.LoadAsync((uint)stream.Size);
                    artwork = new byte[(int)stream.Size];
                    reader.ReadBytes(artwork);
                }
            }
            _cachedWindowsArtworkKey = artworkKey;
            _cachedWindowsArtwork = artwork;
        }
        var player = session.SourceAppUserModelId.Contains("Spotify", StringComparison.OrdinalIgnoreCase) ? "Spotify" : session.SourceAppUserModelId;
        var trackId = properties.Title ?? "";
        return new(true, trackId, properties.Artist ?? "", playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing, player,
            properties.AlbumTitle ?? "", (int)properties.TrackNumber, "", (long)timeline.Position.TotalMilliseconds,
            (long)timeline.EndTime.TotalMilliseconds, GetWindowsVolume(session.SourceAppUserModelId), playback.IsShuffleActive == true, playback.AutoRepeatMode.ToString() ?? "None", trackId,
            controls.IsShuffleEnabled, controls.IsRepeatEnabled, artwork);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException || exception.HResult == unchecked((int)0x80070005))
        {
            _activeWindowsSession = null;
            return ReadWindowsTitleFallback();
        }
#else
        return MediaSession.Empty;
#endif
    }

#if WINDOWS
    private static MediaSession ReadWindowsTitleFallback()
    {
        var spotify = FindSpotifyWindow();
        if (spotify is null) return MediaSession.Empty;
        var title = spotify.Value.Title;
        if (string.IsNullOrWhiteSpace(title) || title.Equals("Spotify", StringComparison.OrdinalIgnoreCase))
            return new(true, "Spotify is open", "Start playback to show the current song.", null, "Spotify", "", 0, "", Volume: GetWindowsVolume("Spotify"), CanShuffle: false, CanRepeat: false);
        title = Regex.Replace(title, @"\s*[•|]\s*Spotify\s*$", "", RegexOptions.IgnoreCase);
        var separator = title.LastIndexOf(" - ", StringComparison.Ordinal);
        return new(true, separator > 0 ? title[..separator] : title, separator > 0 ? title[(separator + 3)..] : "", null, "Spotify", "", 0, "", Volume: GetWindowsVolume("Spotify"), CanShuffle: false, CanRepeat: false);
    }

    private static double? GetWindowsVolume(string appUserModelId) => SetWindowsVolume(appUserModelId, null);

    private static double? SetWindowsVolume(string appUserModelId, double? volume)
    {
        try
        {
            var appName = appUserModelId.Split('!')[0].Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager.Sessions;
            for (var index = 0; index < sessions.Count; index++)
            {
                var audioSession = sessions[index];
                var processId = audioSession.GetProcessID;
                if (processId == 0) continue;
                try
                {
                    using var process = Process.GetProcessById((int)processId);
                    if (!process.ProcessName.Contains(appName, StringComparison.OrdinalIgnoreCase) &&
                        !audioSession.GetSessionIdentifier.Contains(appName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (volume.HasValue)
                        audioSession.SimpleAudioVolume.Volume = (float)Math.Clamp(volume.Value, 0, 1);
                    return audioSession.SimpleAudioVolume.Volume;
                }
                catch (ArgumentException) { }
            }
        }
        catch { }
        return null;
    }
#endif

    private static MediaSession ReadLinuxSession()
    {
        var output = RunCommand("sh", "-c", "p=$(busctl --user list --no-legend 2>/dev/null | awk '$1 ~ /^org\\.mpris\\.MediaPlayer2\\./ {print $1}' | while read p; do s=$(busctl --user get-property \"$p\" /org/mpris/MediaPlayer2 org.mpris.MediaPlayer2.Player PlaybackStatus 2>/dev/null | awk '{print $2}' | tr -d '\"'); if [ \"$s\" = Playing ]; then echo \"$p\"; break; fi; done); [ -n \"$p\" ] || p=$(busctl --user list --no-legend 2>/dev/null | awk '$1 ~ /^org\\.mpris\\.MediaPlayer2\\./ {print $1; exit}'); [ -n \"$p\" ] || exit 0; meta=$(busctl --user get-property \"$p\" /org/mpris/MediaPlayer2 org.mpris.MediaPlayer2.Player Metadata 2>/dev/null); status=$(busctl --user get-property \"$p\" /org/mpris/MediaPlayer2 org.mpris.MediaPlayer2.Player PlaybackStatus 2>/dev/null | awk '{print $2}' | tr -d '\"'); pos=$(busctl --user get-property \"$p\" /org/mpris/MediaPlayer2 org.mpris.MediaPlayer2.Player Position 2>/dev/null | awk '{print $2}'); vol=$(busctl --user get-property \"$p\" /org/mpris/MediaPlayer2 org.mpris.MediaPlayer2.Player Volume 2>/dev/null | awk '{print $2}'); shuf=$(busctl --user get-property \"$p\" /org/mpris/MediaPlayer2 org.mpris.MediaPlayer2.Player Shuffle 2>/dev/null | awk '{print $2}'); loop=$(busctl --user get-property \"$p\" /org/mpris/MediaPlayer2 org.mpris.MediaPlayer2.Player LoopStatus 2>/dev/null | cut -d '\"' -f2); printf '%s\\n%s\\n%s\\n%s\\n%s\\n%s\\n%s\\n' \"$p\" \"$status\" \"$meta\" \"$pos\" \"$vol\" \"$shuf\" \"$loop\"");
        if (string.IsNullOrWhiteSpace(output)) return MediaSession.Empty;
        var fields = output.Split(new[] { '\r', '\n' }, 7, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var metadata = fields.ElementAtOrDefault(2) ?? "";
        var title = ReadMprisString(metadata, "xesam:title") ?? "Unknown track";
        var album = ReadMprisString(metadata, "xesam:album") ?? "";
        var artistMatch = Regex.Match(metadata, "\\\"xesam:artist\\\"\\s+as\\s+\\d+\\s+\\\"([^\\\"]*)\\\"");
        var artist = artistMatch.Success ? artistMatch.Groups[1].Value : ReadMprisString(metadata, "xesam:artist") ?? "";
        var artUrl = ReadMprisString(metadata, "mpris:artUrl");
        var trackNumber = int.TryParse(Regex.Match(metadata, "\\\"xesam:trackNumber\\\"\\s+i\\s+(\\d+)").Groups[1].Value, out var number) ? number : 0;
        var duration = long.TryParse(Regex.Match(metadata, "\\\"mpris:length\\\"\\s+[tx]\\s+(\\d+)").Groups[1].Value, out var length) ? length / 1000 : 0;
        var trackId = Regex.Match(metadata, "\\\"mpris:trackid\\\"\\s+o\\s+\\\"([^\\\"]+)\\\"").Groups[1].Value;
        var position = long.TryParse(fields.ElementAtOrDefault(3), out var positionUs) ? positionUs / 1000 : 0;
        var volume = double.TryParse(fields.ElementAtOrDefault(4), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var volumeValue) ? volumeValue : (double?)null;
        var shuffle = bool.TryParse(fields.ElementAtOrDefault(5), out var shuffleValue) && shuffleValue;
        return new(true, title, artist, fields.ElementAtOrDefault(1)?.Equals("Playing", StringComparison.OrdinalIgnoreCase) == true, fields.ElementAtOrDefault(0) ?? "Media player", album, trackNumber, artUrl ?? "", position, duration, volume, shuffle, fields.ElementAtOrDefault(6) ?? "None", trackId, true, true);
    }

    private static string? ReadMprisString(string metadata, string key)
    {
        var match = Regex.Match(metadata, "\\\"" + Regex.Escape(key) + "\\\"\\s+s\\s+\\\"((?:\\\\.|[^\\\"])*)\\\"");
        return match.Success ? match.Groups[1].Value.Replace("\\\\\"", "\"").Replace("\\\\\\\\", "\\\\") : null;
    }

    private static string FormatTime(long milliseconds)
    {
        var time = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes}:{time.Seconds:00}";
    }

    private static async Task<bool> SendPlatformCommandAsync(string command, MediaSession session, double position, double volume, bool shuffle, string repeat)
    {
        if (OperatingSystem.IsWindows())
        {
#if WINDOWS
            if (command == "volume")
                return await Task.Run(() => SetWindowsVolume(_activeWindowsSession?.SourceAppUserModelId ?? "Spotify", volume / 100).HasValue);
            if (_activeWindowsSession is { } activeSession)
            {
                return command switch
                {
                    "previous" => await activeSession.TrySkipPreviousAsync(),
                    "next" => await activeSession.TrySkipNextAsync(),
                    "play-pause" => await activeSession.TryTogglePlayPauseAsync(),
                    "shuffle" => await activeSession.TryChangeShuffleActiveAsync(!shuffle),
                    "repeat" => await activeSession.TryChangeAutoRepeatModeAsync(repeat switch
                    {
                        "None" => MediaPlaybackAutoRepeatMode.List,
                        "List" or "Playlist" => MediaPlaybackAutoRepeatMode.Track,
                        _ => MediaPlaybackAutoRepeatMode.None
                    }),
                    "seek" => await activeSession.TryChangePlaybackPositionAsync((long)position * TimeSpan.TicksPerMillisecond),
                    _ => false
                };
            }
#endif
            var action = command switch { "previous" => 12, "next" => 11, "play-pause" => 14, _ => 0 };
            if (action == 0) return false;
            var spotify = FindSpotifyWindow();
            return spotify is not null && PostMessage(spotify.Value.Handle, 0x0319, IntPtr.Zero, new IntPtr(action << 16));
        }
        return await Task.Run(() => SendLinuxCommand(command, session, position, volume, shuffle, repeat));
    }

    private static bool SendLinuxCommand(string command, MediaSession session, double position, double volume, bool shuffle, string repeat)
    {
        if (!session.Available || string.IsNullOrWhiteSpace(session.Player)) return false;
        var path = "/org/mpris/MediaPlayer2";
        var playerInterface = "org.mpris.MediaPlayer2.Player";
        switch (command)
        {
            case "previous":
            case "next":
            case "play-pause":
                RunCommand("busctl", "--user", "call", session.Player, path, playerInterface, command switch { "previous" => "Previous", "next" => "Next", _ => "PlayPause" });
                break;
            case "shuffle":
                RunCommand("busctl", "--user", "set-property", session.Player, path, playerInterface, "Shuffle", "b", (!shuffle).ToString().ToLowerInvariant());
                break;
            case "repeat":
                var nextRepeat = repeat switch { "None" => "Playlist", "Playlist" => "Track", _ => "None" };
                RunCommand("busctl", "--user", "set-property", session.Player, path, playerInterface, "LoopStatus", "s", nextRepeat);
                break;
            case "seek":
                if (string.IsNullOrWhiteSpace(session.TrackId)) return false;
                RunCommand("busctl", "--user", "call", session.Player, path, playerInterface, "SetPosition", "ox", session.TrackId, ((long)position * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture));
                break;
            case "volume":
                RunCommand("busctl", "--user", "set-property", session.Player, path, playerInterface, "Volume", "d", (volume / 100).ToString(System.Globalization.CultureInfo.InvariantCulture));
                break;
            default:
                return false;
        }
        return true;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private static (IntPtr Handle, string Title)? FindSpotifyWindow()
    {
        foreach (var process in Process.GetProcessesByName("Spotify"))
        {
            using (process)
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                    return (process.MainWindowHandle, process.MainWindowTitle);
            }
        }
        return null;
    }

    private static string RunCommand(string fileName, params string[] args)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(fileName) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(4000)) { process.Kill(true); throw new TimeoutException("The system media service did not respond."); }
        var output = outputTask.GetAwaiter().GetResult();
        _ = errorTask.GetAwaiter().GetResult();
        return output;
    }

    private readonly record struct MediaSession(bool Available, string Track, string Artist, bool? IsPlaying, string Player, string Album, int TrackNumber, string ArtUrl, long PositionMs = 0, long DurationMs = 0, double? Volume = null, bool Shuffle = false, string LoopStatus = "None", string TrackId = "", bool CanShuffle = false, bool CanRepeat = false, byte[]? ArtData = null)
    {
        public static MediaSession Empty => new(false, "No media session", "", false, "", "", 0, "");
    }

}
