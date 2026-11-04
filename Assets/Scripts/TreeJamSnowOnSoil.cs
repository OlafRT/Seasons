using UnityEngine;

namespace TreeJam
{
    /// <summary>Winter blanket/snowfall, placed on the exact shared soil physics outline.</summary>
    [DisallowMultipleComponent]
    public sealed class TreeJamSnowOnSoil : MonoBehaviour
    {
        public TreeJamSoilSurface ground;
        public TreeJamSeasonCycle seasons;
        public Camera sceneCamera;
        [Range(.06f, .45f)] public float maximumThickness = .22f;
        [Min(1)] public float buildSeconds = 24f;
        [Min(1)] public float meltSeconds = 19f;
        [Range(0f, 90f)] public float snowflakesPerSecond = 20f;
        public Color snowColor = new Color(.90f,.95f,1f,.98f);
        public bool forceWinterPreview;
        [Header("Sorting (runtime renderers)")]
        [Tooltip("Snow blanket render order. Keep above grass but behind tree and mushrooms.")]
        public int blanketSortingOrder = -5;
        [Tooltip("Falling snowflake render order; separate from the ground blanket.")]
        public int snowfallSortingOrder = 18;
        [Range(32, 256)] public int surfaceSamples = 130;

        private LineRenderer blanket;
        private ParticleSystem snowfall;
        private Material mat;
        private float coverage;

        private void Start()
        {
            if (ground == null) ground = FindFirstObjectByType<TreeJamSoilSurface>();
            if (seasons == null) seasons = FindFirstObjectByType<TreeJamSeasonCycle>();
            if (sceneCamera == null) sceneCamera = Camera.main;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) { Debug.LogWarning("Missing unlit shader for snow.", this); enabled = false; return; }
            mat = new Material(shader) { name = "Snow on Soil (runtime)" };

            GameObject go = new GameObject("Snow sitting on painted soil (runtime)");
            go.transform.SetParent(transform, false);
            blanket = go.AddComponent<LineRenderer>();
            blanket.sharedMaterial = mat;
            blanket.useWorldSpace = true;
            blanket.alignment = LineAlignment.View;
            blanket.numCapVertices = 3;
            blanket.numCornerVertices = 4;
            blanket.sortingOrder = blanketSortingOrder;
            blanket.startColor = blanket.endColor = snowColor;
            blanket.positionCount = surfaceSamples;
            blanket.enabled = false;

            GameObject flakes = new GameObject("Snowflakes (runtime)");
            flakes.transform.SetParent(transform, false);
            snowfall = flakes.AddComponent<ParticleSystem>();
            snowfall.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = snowfall.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = 6f;
            main.startSize = new ParticleSystem.MinMaxCurve(.024f, .065f);
            main.startSpeed = 0f;
            main.startColor = new Color(.92f,.97f,1f,.90f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = .08f;
            main.maxParticles = 800;
            var velocity = snowfall.velocityOverLifetime;
            velocity.enabled = true;
            // Unity requires X, Y, and Z velocity curves to use matching modes.
            // X/Y use Two Constants; set Z to Two Constants too (zero depth motion).
            velocity.x = new ParticleSystem.MinMaxCurve(-.07f,.07f);
            velocity.y = new ParticleSystem.MinMaxCurve(-.9f,-.55f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var em = snowfall.emission;
            em.rateOverTime = 0f;
            var shape = snowfall.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            var particleRenderer = snowfall.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = mat;
            particleRenderer.sortingOrder = snowfallSortingOrder;
            snowfall.Play();
            if (ground == null || ground.soilOutline == null)
                Debug.LogWarning("Snow is waiting for a valid soil collider in TreeJamSoilSurface.", this);
        }

        private void Update()
        {
            bool winter = forceWinterPreview || (seasons != null && seasons.CurrentSeason == TreeSeason.Winter);
            coverage = Mathf.MoveTowards(coverage, winter ? 1f : 0f,
                Time.deltaTime / Mathf.Max(1f, winter ? buildSeconds : meltSeconds));
            if (blanket != null)
            {
                // Apply Inspector changes even while the game is playing.
                blanket.sortingOrder = blanketSortingOrder;
                blanket.enabled = coverage > .005f && ground != null && ground.soilOutline != null;
                blanket.widthMultiplier = Mathf.Max(.001f, maximumThickness * coverage);
                Color tint = snowColor;
                tint.a *= Mathf.SmoothStep(0f, 1f, coverage);
                blanket.startColor = blanket.endColor = tint;
                if (blanket.enabled)
                {
                    Bounds bounds = ground.WorldBounds;
                    int points = Mathf.Max(32, surfaceSamples);
                    blanket.positionCount = points;
                    for (int i = 0; i < points; ++i)
                    {
                        float x = Mathf.Lerp(bounds.min.x + .005f, bounds.max.x - .005f, i / (float)(points - 1));
                        float y = ground.HeightOr(x, bounds.center.y);
                        blanket.SetPosition(i, new Vector3(x, y + maximumThickness * coverage * .35f, 0f));
                    }
                }
            }
            if (snowfall != null)
            {
                var particleRenderer = snowfall.GetComponent<ParticleSystemRenderer>();
                if (particleRenderer != null) particleRenderer.sortingOrder = snowfallSortingOrder;
                var em = snowfall.emission;
                em.rateOverTime = winter ? snowflakesPerSecond : 0f;
                if (sceneCamera != null)
                {
                    float halfWidth = sceneCamera.orthographicSize * sceneCamera.aspect;
                    snowfall.transform.position = new Vector3(sceneCamera.transform.position.x,
                        sceneCamera.transform.position.y + sceneCamera.orthographicSize + .25f, 0f);
                    var shape = snowfall.shape;
                    shape.scale = new Vector3(halfWidth * 2f, .05f, 0f);
                }
            }
        }
        private void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
