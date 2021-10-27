using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Concise.Observables
{
    [Flags]
    public enum DateTimeResolution
    {
        Second = 0,
        Minute = 1,
        Hour = 2,
        Day = 3,
        LocalTime = 8,
    }

    public class ObservableDateTime: ObservableValue<DateTimeOffset>
    {
        public DateTimeResolution Resolution { get; }

        private DateTimeOffset _value = DateTimeOffset.Now;

        private TimeSpan GetDelay()
        {
            var msDuration = ((Resolution & ~DateTimeResolution.LocalTime) switch
            {
                DateTimeResolution.Second => TimeSpan.FromSeconds(1),
                DateTimeResolution.Minute => TimeSpan.FromMinutes(1),
                DateTimeResolution.Hour => TimeSpan.FromHours(1),
                DateTimeResolution.Day => TimeSpan.FromDays(1),
                _ => throw new Exception("Unexpected Resolution")
            }).TotalMilliseconds;

            var now = DateTimeOffset.Now;

            if ((Resolution & DateTimeResolution.LocalTime) != 0)
                now = now.ToLocalTime();

            var msNow = now.TimeOfDay.TotalMilliseconds;
            var msDelay = (msNow + msDuration) % msDuration;

            return TimeSpan.FromMilliseconds(msDelay);
        }

        private CancellationTokenSource _tickerToken = new();

        private async void Ticker()
        {
            while (!_tickerToken.IsCancellationRequested)
            {
                await Task.Delay(GetDelay(), _tickerToken.Token);
                if (_tickerToken.IsCancellationRequested)
                    break;
                this.SetNeedsUpdate();
            }
        }

        public ObservableDateTime(DateTimeResolution resolution)
        {
            Resolution = resolution;

            Ticker();
        }

        ~ObservableDateTime()
        {
            _tickerToken.Cancel();
        }

        protected override DateTimeOffset GetValueImplementation() =>
            _value;

        protected override bool UpdateValueImplementation()
        {
            _value = DateTimeOffset.Now;
            return true;
        }

        private static ThreadLocal<Dictionary<DateTimeResolution, ObservableDateTime>> _timerCache = new(() => new());

        public static ObservableDateTime By(DateTimeResolution resolution)
        {
            // ensure we are in a valid domain....

            if (ObservableDomain.Current == null)
                throw new Exception("Invalid Thread - must be called in a Valid ObservableDomain");

            // Get the appropriate cache for this thread...
            // (We maintain a separate group of timers for each thread,
            // which is the same as a domain for us.)

            var timerCache = _timerCache.Value;

            if (timerCache.TryGetValue(resolution, out var existing))
                return existing;

            var newTimer = new ObservableDateTime(resolution);
            timerCache[resolution] = newTimer;

            return newTimer;
        }

        public static DateTimeOffset BySecond => By(DateTimeResolution.Second).Value;
        public static DateTimeOffset ByMinute => By(DateTimeResolution.Minute).Value;
        public static DateTimeOffset ByHour => By(DateTimeResolution.Hour).Value;
        public static DateTimeOffset ByGMTDay => By(DateTimeResolution.Day).Value;
        public static DateTimeOffset ByLocalDay => By(DateTimeResolution.Day | DateTimeResolution.LocalTime).Value;
    }
}
