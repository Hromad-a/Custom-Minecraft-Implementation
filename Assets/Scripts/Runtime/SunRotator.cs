using UnityEngine;

namespace CustomMinecraft
{
    /// <summary>
    /// Day/night cycle: rotates the directional light around its X axis at a
    /// constant speed (default 1.2 degrees per second = full rotation in 5
    /// minutes), fading its intensity out as it sets and tinting it warm near
    /// the horizon. The light's configured intensity and color are treated as
    /// the midday values.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class SunRotator : MonoBehaviour
    {
        [Tooltip("Degrees per second; 1.2 = full rotation in 5 minutes.")]
        [SerializeField, Min(0f)] private float degreesPerSecond = 1.2f;
        [Tooltip("Light color at sunrise/sunset, blending into the midday color as the sun climbs.")]
        [SerializeField] private Color horizonColor = new(1f, 0.55f, 0.25f);
        [Tooltip("Sun elevation (0..1, where 1 is straight overhead) below which the light fades toward the horizon look.")]
        [SerializeField, Range(0.05f, 1f)] private float horizonFadeBand = 0.25f;

        private Light sun;
        private float middayIntensity;
        private Color middayColor;

        private void Awake()
        {
            sun = GetComponent<Light>();
            middayIntensity = sun.intensity;
            middayColor = sun.color;
        }

        private void Update()
        {
            transform.Rotate(degreesPerSecond * Time.deltaTime, 0f, 0f);

            // 1 when the sun shines straight down, 0 at the horizon, negative at night.
            float elevation = -transform.forward.y;
            float daylight = Mathf.Clamp01(elevation / horizonFadeBand);
            sun.intensity = middayIntensity * daylight;
            sun.color = Color.Lerp(horizonColor, middayColor, daylight);
        }
    }
}
