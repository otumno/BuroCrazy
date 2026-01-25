using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Data;
using Characters;

namespace Tests
{
    public class ArchetypeDatabaseTests
    {
        [Test]
        public void GetRandomArchetype_ReturnsNotNull_WhenDatabasePopulated()
        {
            var db = ScriptableObject.CreateInstance<ArchetypeDatabase>();
            var archetype1 = ScriptableObject.CreateInstance<ClientArchetype>();
            archetype1.archetypeID = "elderly";
            var archetype2 = ScriptableObject.CreateInstance<ClientArchetype>();
            archetype2.archetypeID = "worker";

            db.allArchetypes = new List<ClientArchetype> { archetype1, archetype2 };
            db.spawnWeights = new List<ArchetypeDatabase.ArchetypeWeight>
            {
                new ArchetypeDatabase.ArchetypeWeight { archetype = archetype1, weight = 10f },
                new ArchetypeDatabase.ArchetypeWeight { archetype = archetype2, weight = 10f }
            };

            var result = db.GetRandomArchetype();

            Assert.IsNotNull(result);
        }

        [Test]
        public void GetRandomArchetype_ReturnsNull_WhenDatabaseEmpty()
        {
            var db = ScriptableObject.CreateInstance<ArchetypeDatabase>();
            db.allArchetypes = new List<ClientArchetype>();

            var result = db.GetRandomArchetype();

            Assert.IsNull(result);
        }

        [Test]
        public void GetArchetypeByID_ReturnsCorrectArchetype()
        {
            var db = ScriptableObject.CreateInstance<ArchetypeDatabase>();
            var archetype = ScriptableObject.CreateInstance<ClientArchetype>();
            archetype.archetypeID = "test_archetype";
            db.allArchetypes = new List<ClientArchetype> { archetype };

            var result = db.GetArchetypeByID("test_archetype");

            Assert.AreEqual(archetype, result);
        }

        [Test]
        public void GetArchetypeByID_ReturnsNull_WhenNotFound()
        {
            var db = ScriptableObject.CreateInstance<ArchetypeDatabase>();
            var archetype = ScriptableObject.CreateInstance<ClientArchetype>();
            archetype.archetypeID = "test_archetype";
            db.allArchetypes = new List<ClientArchetype> { archetype };

            var result = db.GetArchetypeByID("nonexistent");

            Assert.IsNull(result);
        }
    }
}
