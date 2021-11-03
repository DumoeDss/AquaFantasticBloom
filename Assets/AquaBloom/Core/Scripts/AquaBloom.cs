using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AquaSys.AquaEffect
{
    [ExecuteInEditMode, VolumeComponentMenu("AquaEffect/AquaBloom")]
    public class AquaBloom : VolumeComponent, IPostProcessComponent
    {
        [Header("Bloom")]
        [Tooltip("Filters out pixels under this level of brightness. Value is in gamma-space.")]
        public MinFloatParameter Threshold = new MinFloatParameter(0.9f, 0f);

        [Tooltip("Strength of the bloom filter.")]
        public MinFloatParameter Intensity = new MinFloatParameter(0f, 0f);

        [Tooltip("Changes the extent of veiling effects.")]
        public ClampedFloatParameter scatter = new ClampedFloatParameter(0.7f, 0f, 1f);

        [Tooltip("Global tint of the bloom filter.")]
        public ColorParameter tint = new ColorParameter(Color.white, false, false, true);

        //[Tooltip("Clamps pixels to control the bloom amount.")]
        //public MinFloatParameter clamp = new MinFloatParameter(65472f, 0f);

        [Header("Quality")]
        public ClampedFloatParameter BlurOffset = new ClampedFloatParameter(2f, 0f, 3f);
        public ClampedIntParameter Iteration = new ClampedIntParameter(2, 1, 8);
        public ClampedFloatParameter RTDownScaling = new ClampedFloatParameter(3f, 1f, 10f);

        [Header("Lens Dirt")]
        [Tooltip("Dirtiness texture to add smudges or dust to the bloom effect.")]
        public TextureParameter dirtTexture = new TextureParameter(null);

        [Tooltip("Amount of dirtiness.")]
        public MinFloatParameter dirtIntensity = new MinFloatParameter(0f, 0f);

        [Header("Fantastic Bloom")]
        public BoolParameter FantasticBloom = new BoolParameter(false);
        public ClampedFloatParameter FantasticIntensity = new ClampedFloatParameter(0.5f, 0, 0.5f);
        public ClampedFloatParameter FullScreenBloomIntensity = new ClampedFloatParameter(0.2f, 0, 0.5f);
        public ColorParameter FantasticTint = new ColorParameter(Color.white, false, false, true);

        [Header("Others")]
        public BoolParameter BlurOnly = new BoolParameter(false);

        public bool IsActive() => active && (
            (FantasticBloom.value && FullScreenBloomIntensity.value > 0) ||
            BlurOnly.value ||
            Intensity.value > 0);

        public bool IsTileCompatible() => false;
    }
}
