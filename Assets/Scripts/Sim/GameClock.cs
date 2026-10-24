using System;

namespace Ironfront.Sim
{
    /// <summary>
    /// Fixed-step game clock. One tick = 15 game minutes. Real-time with pause, speeds 1–5.
    /// </summary>
    public sealed class GameClock
    {
        /// <summary>Fall Gelb: German ground forces cross the border at dawn, 10 May 1940.</summary>
        public static readonly DateTime Start = new DateTime(1940, 5, 10, 4, 35, 0);
        public const int MinutesPerTick = 15;
        public const int MaxSpeed = 5;
        /// <summary>Ticks per real second at each speed. Speed 1 = 15 game-min per second (a day in ~1.6 min); speed 5 = a day in 6 s.</summary>
        static readonly double[] TicksPerSecond = { 0, 1, 2, 4, 8, 16 };

        public DateTime Now { get; private set; }
        public long Tick { get; private set; }
        public int Speed { get; private set; }
        public bool Paused { get; private set; }

        /// <summary>Progress towards the next tick, 0..1. Use it to interpolate movement smoothly between ticks.</summary>
        public double Fraction { get { return acc; } }

        /// <summary>Raised once per simulated tick, after the time has advanced.</summary>
        public event Action<long> Ticked;

        double acc;

        public GameClock()
        {
            Now = Start;
            Speed = 1;
            Paused = true;
        }

        public void SetSpeed(int speed) { Speed = Math.Max(1, Math.Min(MaxSpeed, speed)); }
        public void SetPaused(bool paused) { Paused = paused; }
        public void TogglePause() { Paused = !Paused; }

        /// <summary>Advance by real elapsed seconds. Runs at most 64 ticks per call so a hitch never freezes the game.</summary>
        public void Advance(double realSeconds)
        {
            if (Paused || realSeconds <= 0) return;
            acc += realSeconds * TicksPerSecond[Speed];
            int n = 0;
            while (acc >= 1.0 && n < 64) { acc -= 1.0; Step(); n++; }
            if (acc >= 1.0) acc = 0;
        }

        public void Step()
        {
            Tick++;
            Now = Start.AddMinutes(Tick * MinutesPerTick);
            var h = Ticked;
            if (h != null) h(Tick);
        }

        /// <summary>0 = full daylight, 1 = full night. Rough northern-France values for May–June (local time), with twilight ramps.</summary>
        public float Darkness
        {
            get
            {
                double h = Now.TimeOfDay.TotalHours;
                const double dawn0 = 4.0, dawn1 = 5.5, dusk0 = 21.0, dusk1 = 22.5;
                if (h < dawn0 || h >= dusk1) return 1f;
                if (h < dawn1) return (float)(1 - (h - dawn0) / (dawn1 - dawn0));
                if (h >= dusk0) return (float)((h - dusk0) / (dusk1 - dusk0));
                return 0f;
            }
        }

        public bool IsNight { get { return Darkness > 0.5f; } }
    }
}
