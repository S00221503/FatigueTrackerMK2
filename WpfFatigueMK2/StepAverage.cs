using System;
using System.Timers;

public class Class1
{
    public class StepTracker
    {
        private System.Timers.Timer _timer;
        private int _stepCount;
        private int _durationInSeconds;

        public int LastAverageSteps { get; private set; } // Stores the last 1-minute average
        public int QuarterAverageSteps { get; private set; } // Stores the 3-minute average
        private Queue<int> _lastThreeAverages; // Keeps track of the last three 1-minute averages

        public event Action<int, int, string> ComparisonResult; // Notify about the comparison result

        public StepTracker()
        {
            _durationInSeconds = 10; // 1 minute
            _timer = new System.Timers.Timer(10000); // 60 seconds (1 minute)
            _timer.Elapsed += OnTimerElapsed;

            _lastThreeAverages = new Queue<int>(); // Initialize the queue
        }

        public void StartTracking()
        {
            _stepCount = 0; // Reset steps when starting
            _lastThreeAverages.Clear(); // Clear historical averages
            _timer.Start();
        }

        public void StopTracking()
        {
            _timer.Stop();
        }

        public void AddSteps(int steps)
        {
            _stepCount += steps; // Add steps to the total
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // Calculate the current average
            LastAverageSteps = _stepCount / (_durationInSeconds / 60); // Steps per minute
            _stepCount = 0; // Reset step count for the next interval

            // Update the queue with the latest 1-minute average
            _lastThreeAverages.Enqueue(LastAverageSteps);
            if (_lastThreeAverages.Count > 3)
            {
                _lastThreeAverages.Dequeue(); // Remove oldest average to maintain rolling 3-minute average
            }

            // Calculate the quarter average
            QuarterAverageSteps = _lastThreeAverages.Count > 0 ? (int)Math.Floor((double)_lastThreeAverages.Sum() / _lastThreeAverages.Count) : 0;

            // Compare the current step average with the quarter average
            string comparison = GetComparisonResult(LastAverageSteps, QuarterAverageSteps);

            // Notify via event if necessary
            ComparisonResult?.Invoke(LastAverageSteps, QuarterAverageSteps, comparison);
        }

        private string GetComparisonResult(int currentAvg, int quarterAvg)
        {
            int differencePercentage = quarterAvg == 0 ? 0 : Math.Abs((currentAvg - quarterAvg) * 100 / quarterAvg);

            if (differencePercentage <= 20)
                return "Expected (0 - 20% difference)";
            else if (differencePercentage <= 60)
                return "Keep an eye on it (21 - 60% difference)";
            else
                return "Look to replace (61%+ difference)";
        }
    }
}
