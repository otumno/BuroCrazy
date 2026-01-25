using NUnit.Framework;
using UnityEngine;
using Characters;
using Data.Visuals;

namespace Tests
{
    public class ClientArchetypeTests
    {
        [Test]
        public void GetRandomThought_ReturnsFromPool()
        {
            var archetype = ScriptableObject.CreateInstance<ClientArchetype>();
            archetype.thoughtPool = new System.Collections.Generic.List<string>
            {
                "Долго же я ждал...",
                "Надеюсь, быстро сделают",
                "Интересно, сколько это займет времени"
            };

            var result = archetype.GetRandomThought();

            Assert.IsNotEmpty(result);
            Assert.IsTrue(archetype.thoughtPool.Contains(result));
        }

        [Test]
        public void GetRandomThought_ReturnsDefault_WhenPoolEmpty()
        {
            var archetype = ScriptableObject.CreateInstance<ClientArchetype>();
            archetype.thoughtPool = new System.Collections.Generic.List<string>();

            var result = archetype.GetRandomThought();

            Assert.AreEqual("...", result);
        }

        [Test]
        public void GetRandomGreeting_ReturnsFromPool()
        {
            var archetype = ScriptableObject.CreateInstance<ClientArchetype>();
            archetype.greetingLines = new System.Collections.Generic.List<string>
            {
                "Здравствуйте!",
                "Добрый день!",
                "Приветствую!"
            };

            var result = archetype.GetRandomGreeting();

            Assert.IsNotEmpty(result);
            Assert.IsTrue(archetype.greetingLines.Contains(result));
        }

        [Test]
        public void GetRandomColor_ReturnsValidColor()
        {
            var hair = ScriptableObject.CreateInstance<HairStyleData>();
            var brownColor = new Color(0.6f, 0.4f, 0.2f);
            hair.allowedColors = new System.Collections.Generic.List<Color>
            {
                Color.black,
                brownColor,
                Color.red
            };

            var result = hair.GetRandomColor();

            Assert.IsTrue(hair.allowedColors.Contains(result));
        }
    }
}
