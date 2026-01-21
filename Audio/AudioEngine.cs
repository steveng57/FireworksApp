using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Windows.Media;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

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

    public bool UseSoftwareMixer { get; set; } = true;

    public bool EnableDiagnostics { get; set; }

    private SoftwareMixer? _mixer;

    public AudioEngine()
    {
        EnsureVoices();
        TryEnsureMixer();
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
        if (UseSoftwareMixer && TryEnsureMixer() && _mixer is not null)
        {
            if (_mixer.TryPlay(s.Type, MasterVolume * s.Gain, s.Loop, MaxVoices, EnableDiagnostics))
            {
                if (EnableDiagnostics)
                    Debug.WriteLine($"[Audio] MixerPlay {s.Type} vol={MasterVolume * s.Gain:0.00}");
                return;
            }
        }

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

    private bool TryEnsureMixer()
    {
        if (!UseSoftwareMixer)
            return false;

        if (_mixer is { IsDisposed: false })
            return true;

        try
        {
            _mixer = new SoftwareMixer();
            return true;
        }
        catch (Exception ex)
        {
            if (EnableDiagnostics)
                Debug.WriteLine($"[Audio] Mixer init failed: {ex}");
            _mixer = null;
            UseSoftwareMixer = false;
            return false;
        }
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
        string? path = ResolvePath(type);
        if (path is null)
            return null;

        return new Uri(path, UriKind.Absolute);
    }

    private static string? ResolvePath(SoundEventType type)
    {
        // Expected files under `Assets/Audio/` and copied to output directory.
        string name = type switch
        {
            SoundEventType.ShellLaunch => "launch.wav",
            SoundEventType.ShellBurst => "burst.wav",
            SoundEventType.Crackle => "crackle.wav",
            SoundEventType.FastCrackle => "fastcrackle.wav",
            SoundEventType.FinaleCluster => "finale_cluster.wav",
            SoundEventType.SpokeWheelPop => "spoke_wheel_pop.wav",
            _ => ""
        };

        if (string.IsNullOrWhiteSpace(name))
            return null;

        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Audio", name);
        if (!File.Exists(path))
            return null;

        return path;
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

        _mixer?.Dispose();
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

    private sealed class SoftwareMixer : ISampleProvider, IDisposable
    {
        private const int SampleRate = 44100;

        private readonly object _lock = new();
        private readonly List<MixerVoice> _voices = new();
        private readonly Dictionary<SoundEventType, MixerClip> _clips = new();
        private readonly WaveOutEvent _output;
        private bool _disposed;

        public SoftwareMixer()
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1);
            _output = new WaveOutEvent
            {
                DesiredLatency = 80,
                NumberOfBuffers = 2
            };
            _output.Init(this);
            _output.Play();
        }

        public WaveFormat WaveFormat { get; }

        public bool IsDisposed => _disposed;

        public int Read(float[] buffer, int offset, int count)
        {
            if (_disposed)
                return 0;

            var span = buffer.AsSpan(offset, count);
            span.Clear();

            lock (_lock)
            {
                for (int i = 0; i < _voices.Count; i++)
                {
                    var v = _voices[i];
                    if (!v.Active)
                        continue;

                    int remaining = v.Clip.Samples.Length - v.Position;
                    if (remaining <= 0)
                    {
                        if (v.Loop)
                        {
                            v.Position = 0;
                            remaining = v.Clip.Samples.Length;
                        }
                        else
                        {
                            v.Active = false;
                            _voices[i] = v;
                            continue;
                        }
                    }

                    int toCopy = Math.Min(count, remaining);
                    var clipSpan = v.Clip.Samples.AsSpan(v.Position, toCopy);
                    for (int s = 0; s < toCopy; s++)
                    {
                        span[s] += clipSpan[s] * v.Gain;
                    }
                    v.Position += toCopy;
                    if (v.Position >= v.Clip.Samples.Length && !v.Loop)
                        v.Active = false;

                    _voices[i] = v;
                }

                for (int i = _voices.Count - 1; i >= 0; i--)
                {
                    if (!_voices[i].Active)
                        _voices.RemoveAt(i);
                }
            }

            float peak = 0.0f;
            for (int i = 0; i < count; i++)
            {
                float p = MathF.Abs(span[i]);
                if (p > peak)
                    peak = p;
            }

            if (peak > 0.99f)
            {
                float scale = 0.99f / peak;
                for (int i = 0; i < count; i++)
                {
                    span[i] *= scale;
                }
            }

            return count;
        }

        public bool TryPlay(SoundEventType type, float gain, bool loop, int maxVoices, bool diagnostics)
        {
            if (_disposed || gain <= 0.0001f)
                return false;

            var clip = GetClip(type, diagnostics);
            if (!clip.HasValue || clip.Value.Samples.Length == 0)
                return false;

            var voice = new MixerVoice(clip.Value, gain, loop);

            lock (_lock)
            {
                if (_voices.Count >= maxVoices)
                {
                    int idx = FindWeakestVoice();
                    if (idx < 0)
                        return false;

                    if (_voices[idx].Gain > gain)
                        return false;

                    _voices[idx] = voice;
                    return true;
                }

                _voices.Add(voice);
            }

            return true;
        }

        private int FindWeakestVoice()
        {
            float min = float.MaxValue;
            int idx = -1;
            for (int i = 0; i < _voices.Count; i++)
            {
                if (_voices[i].Gain < min)
                {
                    min = _voices[i].Gain;
                    idx = i;
                }
            }
            return idx;
        }

        private MixerClip? GetClip(SoundEventType type, bool diagnostics)
        {
            lock (_lock)
            {
                if (_clips.TryGetValue(type, out var cached))
                    return cached;
            }

            string? path = ResolvePath(type);
            if (path is null)
                return null;

            try
            {
                using var reader = new AudioFileReader(path);
                ISampleProvider provider = reader;

                if (provider.WaveFormat.Channels == 2)
                {
                    provider = new StereoToMonoSampleProvider(provider);
                }
                else if (provider.WaveFormat.Channels != 1)
                {
                    provider = new MultiplexingSampleProvider(new[] { provider }, 1);
                }

                if (provider.WaveFormat.SampleRate != SampleRate)
                {
                    provider = new WdlResamplingSampleProvider(provider, SampleRate);
                }

                var samples = new List<float>(reader.Length > 0 ? (int)(reader.Length / sizeof(float)) : 4096);
                var buffer = new float[SampleRate];
                int read;
                while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < read; i++)
                    {
                        samples.Add(buffer[i]);
                    }
                }

                var clip = new MixerClip(samples.ToArray());

                lock (_lock)
                {
                    _clips[type] = clip;
                }

                return clip;
            }
            catch (Exception ex)
            {
                if (diagnostics)
                    Debug.WriteLine($"[Audio] Mixer load failed for {type}: {ex}");
                return null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            lock (_lock)
            {
                _voices.Clear();
                _clips.Clear();
            }
            _output.Dispose();
        }

        private struct MixerClip
        {
            public MixerClip(float[] samples)
            {
                Samples = samples;
            }

            public float[] Samples { get; }
        }

        private struct MixerVoice
        {
            public MixerVoice(MixerClip clip, float gain, bool loop)
            {
                Clip = clip;
                Gain = gain;
                Loop = loop;
                Position = 0;
                Active = true;
            }

            public MixerClip Clip { get; }
            public float Gain { get; }
            public bool Loop { get; }
            public int Position { get; set; }
            public bool Active { get; set; }
        }
    }
}
