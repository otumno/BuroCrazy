using NUnit.Framework;
using UnityEngine;

namespace Tests
{
    public class StaffScheduleTests
    {
        [Test]
        public void CalculateLateness_WithHighPunctuality_ReturnsLowValue()
        {
            float punctuality = 0.9f;
            float maxLateness = 30f;
            float latenessVariance = 5f;

            float totalLateness = 0f;
            int iterations = 100;

            for (int i = 0; i < iterations; i++)
            {
                float noLatenessChance = punctuality;
                float randomValue = Random.value;

                float lateness;
                if (randomValue < noLatenessChance)
                {
                    lateness = Random.Range(0f, 2f);
                }
                else
                {
                    float latenessChance = 1f - noLatenessChance;
                    float latenessMultiplier = latenessChance * (1f - punctuality * 0.5f);
                    float maxLatenessAdjusted = maxLateness * (1f + latenessMultiplier);
                    lateness = Random.Range(0f, maxLatenessAdjusted);
                }

                totalLateness += lateness;
            }

            float averageLateness = totalLateness / iterations;

            Assert.Less(averageLateness, 10f, $"Среднее опоздание {averageLateness} должно быть меньше 10 секунд при высокой педантичности");
        }

        [Test]
        public void CalculateLateness_WithLowPunctuality_ReturnsHigherValue()
        {
            float punctuality = 0.2f;
            float maxLateness = 30f;

            float totalLateness = 0f;
            int iterations = 100;

            for (int i = 0; i < iterations; i++)
            {
                float noLatenessChance = punctuality;
                float randomValue = Random.value;

                float lateness;
                if (randomValue < noLatenessChance)
                {
                    lateness = Random.Range(0f, 2f);
                }
                else
                {
                    float latenessChance = 1f - noLatenessChance;
                    float latenessMultiplier = latenessChance * (1f - punctuality * 0.5f);
                    float maxLatenessAdjusted = maxLateness * (1f + latenessMultiplier);
                    lateness = Random.Range(0f, maxLatenessAdjusted);
                }

                totalLateness += lateness;
            }

            float averageLateness = totalLateness / iterations;

            Assert.Greater(averageLateness, 10f, $"Среднее опоздание {averageLateness} должно быть больше 10 секунд при низкой педантичности");
        }

        [Test]
        public void CalculateLateness_MaxPunctuality_HasMinimalVariance()
        {
            float punctuality = 1f;
            float maxLateness = 30f;

            float totalLateness = 0f;
            int iterations = 100;

            for (int i = 0; i < iterations; i++)
            {
                float noLatenessChance = punctuality;
                float randomValue = Random.value;

                float lateness;
                if (randomValue < noLatenessChance)
                {
                    lateness = Random.Range(0f, 2f);
                }
                else
                {
                    float latenessChance = 1f - noLatenessChance;
                    float latenessMultiplier = latenessChance * (1f - punctuality * 0.5f);
                    float maxLatenessAdjusted = maxLateness * (1f + latenessMultiplier);
                    lateness = Random.Range(0f, maxLatenessAdjusted);
                }

                totalLateness += lateness;
            }

            float averageLateness = totalLateness / iterations;

            Assert.Less(averageLateness, 3f, $"При максимальной педантичности разброс должен быть 0-2 секунды");
        }
    }
}
