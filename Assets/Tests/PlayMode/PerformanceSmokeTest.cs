using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CompanionIntelligence.Tests
{
    public class PerformanceSmokeTest
    {
        [UnityTest]
        public IEnumerator AverageFrameTime_IsReasonable()
        {
            Application.targetFrameRate = 60;

            const int warmupFrames = 30;
            const int sampleFrames = 120;
            const float maxAverageFrameTime = 1.0f / 15.0f; // 15 FPS minimum

            for (int i = 0; i < warmupFrames; i++)
            {
                yield return null;
            }

            float totalDelta = 0f;
            for (int i = 0; i < sampleFrames; i++)
            {
                totalDelta += Time.deltaTime;
                yield return null;
            }

            float averageFrameTime = totalDelta / sampleFrames;
            Assert.LessOrEqual(averageFrameTime, maxAverageFrameTime,
                $"Average frame time too high: {averageFrameTime:F4}s");
        }
    }
}
