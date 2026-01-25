using NUnit.Framework;
using UnityEngine;
using Enums;
using Data.Documents;
using Characters;

namespace Tests
{
    public class DocumentDataTests
    {
        [Test]
        public void CanBeProcessedBy_WithSkillLevel_ReturnsTrue_WhenSkillSufficient()
        {
            var doc = ScriptableObject.CreateInstance<DocumentData>();
            doc.minSkillLevel = 2;
            doc.requiredEquipment = EquipmentType.None;

            var result = doc.CanBeProcessedBy(
                StaffController.Role.Clerk,
                3,
                true
            );

            Assert.IsTrue(result);
        }

        [Test]
        public void CanBeProcessedBy_WithLowSkill_ReturnsFalse()
        {
            var doc = ScriptableObject.CreateInstance<DocumentData>();
            doc.minSkillLevel = 3;
            doc.requiredEquipment = EquipmentType.None;

            var result = doc.CanBeProcessedBy(
                StaffController.Role.Clerk,
                2,
                true
            );

            Assert.IsFalse(result);
        }

        [Test]
        public void CanBeProcessedBy_WithEquipmentRequired_ReturnsFalse_WhenNoEquipment()
        {
            var doc = ScriptableObject.CreateInstance<DocumentData>();
            doc.requiredEquipment = EquipmentType.Copier;
            doc.canProcessWithoutEquipment = false;

            var result = doc.CanBeProcessedBy(
                StaffController.Role.Clerk,
                1,
                false
            );

            Assert.IsFalse(result);
        }

        [Test]
        public void CanBeProcessedBy_WithEquipmentAllowed_ReturnsTrue_WhenNoEquipment()
        {
            var doc = ScriptableObject.CreateInstance<DocumentData>();
            doc.requiredEquipment = EquipmentType.Copier;
            doc.canProcessWithoutEquipment = true;

            var result = doc.CanBeProcessedBy(
                StaffController.Role.Clerk,
                1,
                false
            );

            Assert.IsTrue(result);
        }

        [Test]
        public void GetProcessingTimeWithPenalty_ReturnsBaseTime_WhenHasEquipment()
        {
            var doc = ScriptableObject.CreateInstance<DocumentData>();
            doc.baseProcessingTime = 10f;
            doc.requiredEquipment = EquipmentType.Copier;
            doc.canProcessWithoutEquipment = false;
            doc.equipmentPenalty = 0.5f;

            var result = doc.GetProcessingTimeWithPenalty(true);

            Assert.AreEqual(10f, result);
        }

        [Test]
        public void GetProcessingTimeWithPenalty_ReturnsPenalizedTime_WhenNoEquipment()
        {
            var doc = ScriptableObject.CreateInstance<DocumentData>();
            doc.baseProcessingTime = 10f;
            doc.requiredEquipment = EquipmentType.Copier;
            doc.canProcessWithoutEquipment = true;
            doc.equipmentPenalty = 0.5f;

            var result = doc.GetProcessingTimeWithPenalty(false);

            Assert.AreEqual(15f, result);
        }
    }
}
