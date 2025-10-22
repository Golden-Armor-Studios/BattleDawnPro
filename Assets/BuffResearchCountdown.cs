using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace UI
{
    public class BuffResearchCountdown : MonoBehaviour
    {
        public string BuffId;
        public string StartTimeIso;
        public string FinishTimeIso;
        public TMP_Text CountdownText;
        public Action<string> OnComplete;

        private bool hasNotified;

        private void OnEnable()
        {
            hasNotified = false;
        }

        private void Update()
        {
            if (CountdownText == null || string.IsNullOrEmpty(FinishTimeIso))
            {
                if (CountdownText != null)
                {
                    CountdownText.text = string.Empty;
                }
                return;
            }

            if (!DateTime.TryParseExact(FinishTimeIso, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var finish))
            {
                CountdownText.text = string.Empty;
                return;
            }

            var remaining = finish - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                CountdownText.text = "Done";
                if (!hasNotified)
                {
                    hasNotified = true;
                    (OnComplete ?? ResearchUI.NotifyBuffResearchComplete)?.Invoke(BuffId);
                }
                return;
            }

            CountdownText.text = FormatTime(remaining);
        }

        private static string FormatTime(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}", (int)timeSpan.TotalHours, timeSpan.Minutes, timeSpan.Seconds);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}", timeSpan.Minutes, timeSpan.Seconds);
        }
    }
}
