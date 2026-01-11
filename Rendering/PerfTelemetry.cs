using System;
using System.Diagnostics;

namespace FireworksApp.Rendering;

internal sealed class PerfTelemetry
{
    private long _lastReportTimestamp;
    private long _lastAllocatedBytes;
    private int _lastGen0;
    private int _lastGen1;
    private int _lastGen2;

    private long _frameCount;

    private double _uploadMsAccum;
    private double _uploadMsMax;
    private long _uploadBytesAccum;
    private long _uploadCalls;

    private double _mapMsAccum;
    private double _mapMsMax;
    private long _mapCalls;

    private long _queuedParticles;
    private long _droppedParticles;

    public bool Enabled { get; set; } = true;

    public void RecordQueue(long queuedParticles, long droppedParticles)
    {
        if (!Enabled)
            return;

        _queuedParticles = queuedParticles;
        _droppedParticles += droppedParticles;
    }

    public void RecordUpload(TimeSpan elapsed, int bytes)
    {
        if (!Enabled)
            return;

        double ms = elapsed.TotalMilliseconds;
        _uploadMsAccum += ms;
        _uploadMsMax = System.MathF.Max((float)_uploadMsMax, (float)ms);
        _uploadBytesAccum += bytes;
        _uploadCalls++;
    }

    public void RecordMap(double elapsedMilliseconds)
    {
        if (!Enabled)
            return;

        _mapMsAccum += elapsedMilliseconds;
        _mapMsMax = System.MathF.Max((float)_mapMsMax, (float)elapsedMilliseconds);
        _mapCalls++;
    }

    public void RecordUpload(double elapsedMilliseconds, int bytes)
    {
        if (!Enabled)
            return;

        _uploadMsAccum += elapsedMilliseconds;
        _uploadMsMax = System.MathF.Max((float)_uploadMsMax, (float)elapsedMilliseconds);
        _uploadBytesAccum += bytes;
        _uploadCalls++;
    }

    public void Tick(string source, Action? appendDetails)
    {
        if (!Enabled)
            return;

        _frameCount++;

        long now = Stopwatch.GetTimestamp();
        if (_lastReportTimestamp == 0)
        {
            _lastReportTimestamp = now;
            _lastAllocatedBytes = GC.GetTotalAllocatedBytes(precise: false);
            _lastGen0 = GC.CollectionCount(0);
            _lastGen1 = GC.CollectionCount(1);
            _lastGen2 = GC.CollectionCount(2);
            return;
        }

        double seconds = (now - _lastReportTimestamp) / (double)Stopwatch.Frequency;
        if (seconds < 1.0)
            return;

        long allocatedNow = GC.GetTotalAllocatedBytes(precise: false);
        long allocDelta = allocatedNow - _lastAllocatedBytes;
        _lastAllocatedBytes = allocatedNow;

        int gen0 = GC.CollectionCount(0);
        int gen1 = GC.CollectionCount(1);
        int gen2 = GC.CollectionCount(2);
        int gen0Delta = gen0 - _lastGen0;
        int gen1Delta = gen1 - _lastGen1;
        int gen2Delta = gen2 - _lastGen2;
        _lastGen0 = gen0;
        _lastGen1 = gen1;
        _lastGen2 = gen2;

        double fps = _frameCount / seconds;
        _frameCount = 0;

        double uploadAvgMs = _uploadCalls > 0 ? (_uploadMsAccum / _uploadCalls) : 0.0;
        double uploadMaxMs = _uploadMsMax;
        double uploadMbPerSec = seconds > 1e-6 ? (_uploadBytesAccum / (1024.0 * 1024.0)) / seconds : 0.0;

        double mapAvgMs = _mapCalls > 0 ? (_mapMsAccum / _mapCalls) : 0.0;
        double mapMaxMs = _mapMsMax;

        _uploadMsAccum = 0.0;
        _uploadMsMax = 0.0;
        _uploadBytesAccum = 0;
        _uploadCalls = 0;

        _mapMsAccum = 0.0;
        _mapMsMax = 0.0;
        _mapCalls = 0;

        _lastReportTimestamp = now;

        Debug.Write($"[Perf] {source} fps={fps:F1} alloc={allocDelta / (1024.0 * 1024.0):F2}MB/s GC(0/1/2)+={gen0Delta}/{gen1Delta}/{gen2Delta} map(avg/max)={mapAvgMs:F3}/{mapMaxMs:F3}ms upload(avg/max)={uploadAvgMs:F3}/{uploadMaxMs:F3}ms upload={uploadMbPerSec:F2}MB/s");
        if (_queuedParticles > 0 || _droppedParticles > 0)
            Debug.Write($" queue(pend/drop)={_queuedParticles}/{_droppedParticles}");
        appendDetails?.Invoke();
        Debug.WriteLine(string.Empty);

        _queuedParticles = 0;
        _droppedParticles = 0;
    }
}
