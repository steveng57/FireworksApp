using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Windows.Media;

namespace FireworksApp.Audio;

public sealed class AudioEngine : IDisposable
{
    private readonly Queue<Scheduled> _scheduled = new();

    private readonly List<Voice> _voices = new();
    private int _nextVoice;

    public Vector3 ListenerPosition { get; set; }

    public float MasterVolume { get; set; } = 0.8f;

    public float SpeedOfSoundMetersPerSecond { get; set; } = 343.0f;

    public float MaxDistanceMeters { get; set; } = 800.0f;

    public int MaxVoices { get; set; } = 16;

    public bool EnableDiagnostics { get; set; }

    public AudioEngine()
    {
        EnsureVoices();
    }

    public void Enqueue(in SoundEvent ev)
    {
        var delay = ev.Delay ?? ComputeDelay(ev.Position);
        float gain = ev.Gain * ComputeAttenuation(ev.Position);

        if (gain <= 0.0001f)
            return;

        _scheduled.Enqueue(new Scheduled(ev.Type, delay, gain, ev.Loop));
    }

    public void Update(TimeSpan dt)
    {
        if (dt <= TimeSpan.Zero || _scheduled.Count == 0)
            return;

        int n = _scheduled.Count;
        for (int i = 0; i < n; i++)
        {
            var s = _scheduled.Dequeue();
            var remaining = s.Delay - dt;
            if (remaining <= TimeSpan.Zero)
            {
                PlayNow(s);
            }
            else
            {
                s.Delay = remaining;
                _scheduled.Enqueue(s);
            }
        }
    }

    private TimeSpan ComputeDelay(Vector3 sourcePosition)
    {
        float d = Vector3.Distance(ListenerPosition, sourcePosition);
        return TimeSpan.FromSeconds(d / global::System.Math.Max(1.0f, SpeedOfSoundMetersPerSecond));
    }

    private float ComputeAttenuation(Vector3 sourcePosition)
    {
        float d = Vector3.Distance(ListenerPosition, sourcePosition);
        if (d >= MaxDistanceMeters)
            return 0.0f;

        // Simple inverse-distance rolloff (clamped).
        float att = 1.0f / MathF.Max(1.0f, d);
        // Normalize so 1m ~= 1.0 and fade out toward MaxDistance.
        float fade = 1.0f - (d / MaxDistanceMeters);
        if (fade < 0.0f) fade = 0.0f;
        return MathF.Min(1.0f, att * 30.0f) * fade;
    }

    private void PlayNow(Scheduled s)
    {
        // Minimal placeholder playback: uses a single MediaPlayer instance.
        // This keeps the audio system decoupled; can be replaced with a polyphonic backend later.
        Uri? uri = ResolveUri(s.Type);
        if (uri is null)
            return;

        EnsureVoices();
        var voice = AcquireVoice();

        voice.Player.MediaEnded -= voice.OnMediaEnded;
        voice.Player.MediaEnded += voice.OnMediaEnded;

        voice.Loop = s.Loop;
        voice.InUse = true;
        voice.Type = s.Type;

        voice.Player.Volume = global::System.Math.Clamp(MasterVolume * s.Gain, 0.0, 1.0);
        voice.Player.Open(uri);
        voice.Player.Play();

        if (EnableDiagnostics)
            Debug.WriteLine($"[Audio] Play {s.Type} vol={voice.Player.Volume:0.00} uri={uri} voice={voice.Id}");
    }

    private void EnsureVoices()
    {
        if (_voices.Count >= MaxVoices)
            return;

        while (_voices.Count < MaxVoices)
        {
            int id = _voices.Count;
            var mp = new MediaPlayer();
            var v = new Voice(id, mp, OnVoiceMediaEnded, OnVoiceMediaFailed);
            _voices.Add(v);
        }
    }

    private Voice AcquireVoice()
    {
        // First try: find an idle voice.
        for (int i = 0; i < _voices.Count; i++)
        {
            var v = _voices[i];
            if (!v.InUse)
                return v;
        }

        // Otherwise steal round-robin.
        var stolen = _voices[_nextVoice++ % _voices.Count];
        try
        {
            stolen.Player.Stop();
            stolen.Player.Close();
        }
        catch
        {
            // ignore
        }
        stolen.InUse = false;
        stolen.Loop = false;
        return stolen;
    }

    private void OnVoiceMediaEnded(Voice v)
    {
        if (v.Loop)
        {
            v.Player.Position = TimeSpan.Zero;
            v.Player.Play();
            return;
        }

        v.InUse = false;
        v.Type = null;
        v.Player.Close();
    }

    private void OnVoiceMediaFailed(Voice v, Exception ex)
    {
        if (EnableDiagnostics)
            Debug.WriteLine($"[Audio] MediaFailed voice={v.Id}: {ex}");
        v.InUse = false;
        v.Type = null;
        try { v.Player.Close(); } catch { }
    }

    private static Uri? ResolveUri(SoundEventType type)
    {
        // Expected files under `Assets/Audio/` and copied to output directory.
        string name = type switch
        {
            SoundEventType.ShellLaunch => "launch.wav",
            SoundEventType.ShellBurst => "burst.wav",
            SoundEventType.Crackle => "crackle.wav",
            SoundEventType.FinaleCluster => "finale_cluster.wav",
            _ => ""
        };

        if (string.IsNullOrWhiteSpace(name))
            return null;

        // Use file URI from output directory to avoid pack:// resolution issues.
        // This works as long as the files are copied by the .csproj item.
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Audio", name);
        if (!File.Exists(path))
            return null;

        return new Uri(path, UriKind.Absolute);
    }

    public void Dispose()
    {
        foreach (var v in _voices)
        {
            try
            {
                v.Player.MediaEnded -= v.OnMediaEnded;
                v.Player.MediaFailed -= v.OnMediaFailed;
                v.Player.Close();
            }
            catch
            {
                // ignore
            }
        }
        _voices.Clear();
    }

    private struct Scheduled
    {
        public SoundEventType Type;
        public TimeSpan Delay;
        public float Gain;
        public bool Loop;

        public Scheduled(SoundEventType type, TimeSpan delay, float gain, bool loop)
        {
            Type = type;
            Delay = delay;
            Gain = gain;
            Loop = loop;
        }
    }

    private sealed class Voice
    {
        public int Id { get; }
        public MediaPlayer Player { get; }
        public bool InUse;
        public bool Loop;
        public SoundEventType? Type;

        public EventHandler OnMediaEnded { get; }
        public EventHandler<ExceptionEventArgs> OnMediaFailed { get; }

        public Voice(int id, MediaPlayer player, Action<Voice> ended, Action<Voice, Exception> failed)
        {
            Id = id;
            Player = player;

            OnMediaEnded = (_, _) => ended(this);
            OnMediaFailed = (_, args) => failed(this, args.ErrorException);

            Player.MediaEnded += OnMediaEnded;
            Player.MediaFailed += OnMediaFailed;
        }
    }
}
