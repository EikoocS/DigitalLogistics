using System.Diagnostics;

namespace DigitalLogistics.Tracing
{
    internal struct TraceTimer
    {
        private readonly bool enabled;
        private bool running;
        private long started;
        private long elapsed;

        internal TraceTimer(bool enabled, bool start = true)
        {
            this.enabled = enabled;
            running = enabled && start;
            started = running ? Stopwatch.GetTimestamp() : 0;
            elapsed = 0;
        }

        internal long ElapsedTicks => elapsed + (running ? Stopwatch.GetTimestamp() - started : 0);

        internal void Resume()
        {
            if (!enabled || running) return;
            started = Stopwatch.GetTimestamp();
            running = true;
        }

        internal void Pause()
        {
            if (!running) return;
            elapsed += Stopwatch.GetTimestamp() - started;
            running = false;
        }

        internal static double Milliseconds(double ticks) => ticks * (1000d / Stopwatch.Frequency);
    }
}
