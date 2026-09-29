using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GuildMaster.Sandbox
{
    /// <summary>
    /// Замер кадра в Play Mode: длина кадра и время скриптов (от самого раннего Update до самого позднего LateUpdate — такты
    /// симуляции и перерисовка интерфейса). Через заданное время пишет в лог среднее, 95-й перцентиль и максимум и удаляет себя.
    /// Добавляется из меню GuildMaster → Sandbox → UI, в сцену не сохраняется.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class FrameMeter : MonoBehaviour
    {
        public float Seconds = 10f;
        public string Label = string.Empty;

        private readonly List<float> frames = new List<float>();
        private readonly List<float> work = new List<float>();
        private readonly Stopwatch watch = new Stopwatch();
        private float started;

        private void OnEnable()
        {
            started = Time.unscaledTime;
            var late = gameObject.AddComponent<FrameMeterLate>();
            late.Meter = this;
        }

        private void Update()
        {
            if (Time.frameCount > 1) frames.Add(Time.unscaledDeltaTime * 1000f);
            watch.Restart();
        }

        internal void EndOfScripts()
        {
            work.Add((float)watch.Elapsed.TotalMilliseconds);
            if (Time.unscaledTime - started < Seconds) return;

            Debug.Log($"[FrameMeter] {Label} frames={frames.Count} frame ms avg={Avg(frames):0.0} p95={P95(frames):0.0} max={Max(frames):0.0}; " +
                      $"scripts ms avg={Avg(work):0.00} p95={P95(work):0.00} max={Max(work):0.00}");
            Destroy(GetComponent<FrameMeterLate>());
            Destroy(this);
        }

        private static float Avg(List<float> values)
        {
            if (values.Count == 0) return 0;
            float sum = 0;
            foreach (float value in values) sum += value;
            return sum / values.Count;
        }

        private static float P95(List<float> values)
        {
            if (values.Count == 0) return 0;
            var sorted = new List<float>(values);
            sorted.Sort();
            return sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * 0.95f))];
        }

        private static float Max(List<float> values)
        {
            float max = 0;
            foreach (float value in values) max = Mathf.Max(max, value);
            return max;
        }
    }

    [DefaultExecutionOrder(10000)]
    internal sealed class FrameMeterLate : MonoBehaviour
    {
        public FrameMeter Meter;

        private void LateUpdate()
        {
            if (Meter != null) Meter.EndOfScripts();
        }
    }
}
