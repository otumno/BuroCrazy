using NUnit.Framework;
using UnityEngine;
using Characters;

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
        public void GetRandomHairColor_ReturnsValidColor()
        {
            var archetype = ScriptableObject.CreateInstance<ClientArchetype>();
            var brownColor = new Color(0.6f, 0.4f, 0.2f);
            archetype.hairColors = new System.Collections.Generic.List<Color>
            {
                Color.black,
                brownColor,
                Color.red
            };

            var result = archetype.GetRandomHairColor();

            Assert.IsTrue(archetype.hairColors.Contains(result));
        }

        [Test]
        public void GetRandomOutfitColor_ReturnsValidColor()
        {
            var archetype = ScriptableObject.CreateInstance<ClientArchetype>();
            var blueColor = new Color(0.3f, 0.3f, 0.4f);
            archetype.outfitColors = new System.Collections.Generic.List<Color>
            {
                Color.gray,
                blueColor,
                Color.white
            };

            var result = archetype.GetRandomOutfitColor();

            Assert.IsTrue(archetype.outfitColors.Contains(result));
        }
    }
}
