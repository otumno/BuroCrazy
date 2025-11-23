using System;
using UnityEngine;

namespace Scriptables
{
    [Serializable]
    public class LightingPreset
    {
        public Color lightColor = Color.white;
        [Range(0f, 2f)] public float lightIntensity = 1f;
    }
}