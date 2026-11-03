using UnityEngine;

namespace TreeJam
{
    /// <summary>
    /// Basic winter snow: an editable snow accumulation curve and drifting flakes.
    /// Procedural visuals are runtime children; no Photoshop layers are overwritten.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TreeJamNordicSnow : MonoBehaviour
    {
        public TreeJamSeasonCycle seasons;
        public Camera worldCamera;
        [Header("Surface contour (adjust to match your curved grass)")]
        public float middleGroundY = -1.0f;
        [Range(0f, 1.8f)] public float raisedEdges = .55f;
        [Range(1f, 3f)] public float edgeShape = 1.6f;
        [Range(.05f, .6f)] public float maximumSnowThickness = .27f;
        [Range(1f, 60f)] public float accumulationSeconds = 28f;
        [Range(1f, 60f)] public float meltingSeconds = 20f;
        [Range(0f, 80f)] public float flakesPerSecond = 23f;
        public Color snowColor = new Color(.87f, .94f, 1f, .95f);

        [Header("Play Mode testing (turn off after testing)")]
        public bool forceWinterPreview;

        private float amount;
        private LineRenderer snowLine;
        private ParticleSystem falling;
        private Material snowMaterial;
        private const int Samples = 33;

        private void Start()
        {
            if (seasons == null) seasons = FindFirstObjectByType<TreeJamSeasonCycle>();
            if (worldCamera == null) worldCamera = Camera.main;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) { Debug.LogWarning("Tree Jam snow: missing unlit shader", this); enabled = false; return; }
            snowMaterial = new Material(shader) { name = "Tree Jam runtime snow (unlit)" };
            var lineObj = new GameObject("Snow blanket - contour settings above");
            lineObj.transform.SetParent(transform, false);
            snowLine = lineObj.AddComponent<LineRenderer>();
            snowLine.sharedMaterial = snowMaterial;
            snowLine.useWorldSpace = true;
            snowLine.alignment = LineAlignment.View;
            snowLine.numCapVertices = 5;
            snowLine.numCornerVertices = 5;
            snowLine.sortingOrder = 16;
            snowLine.positionCount = Samples;
            snowLine.enabled = false;

            var particleObj = new GameObject("Falling snowflakes - runtime particle system");
            particleObj.transform.SetParent(transform, false);
            falling = particleObj.AddComponent<ParticleSystem>();
            falling.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = falling.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 6f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(.028f, .075f);
            main.startColor = new Color(.94f, .97f, 1f, .85f);
            main.maxParticles = 1000;
            main.gravityModifier = .08f;
            var velocity = falling.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(-.95f, -.60f);
            velocity.x = new ParticleSystem.MinMaxCurve(-.08f, .08f);
            var emission = falling.emission;
            emission.rateOverTime = 0;
            var shape = falling.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            var pr = falling.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = snowMaterial;
            pr.sortingOrder = 18;
            falling.Play();
        }

        private void Update()
        {
            bool winter = forceWinterPreview || (seasons != null && seasons.CurrentSeason == TreeSeason.Winter);
            float speed = Time.deltaTime / Mathf.Max(1f, winter ? accumulationSeconds : meltingSeconds);
            amount = Mathf.MoveTowards(amount, winter ? 1f : 0f, speed);
            if (snowLine != null)
            {
                snowLine.enabled = amount > .003f;
                snowLine.widthMultiplier = Mathf.Max(.001f, maximumSnowThickness * amount);
                Color c = snowColor;
                c.a *= Mathf.SmoothStep(0f, 1f, amount);
                snowLine.startColor = snowLine.endColor = c;
            }
            if (worldCamera == null) return;
            float halfWidth = worldCamera.orthographicSize * worldCamera.aspect;
            float xCenter = worldCamera.transform.position.x;
            float sceneHalfWidth = halfWidth * 1.04f;
            for (int i = 0; i < Samples && snowLine != null; ++i)
            {
                float a = Mathf.Lerp(-1f, 1f, i / (float)(Samples - 1));
                float y = middleGroundY + raisedEdges * Mathf.Pow(Mathf.Abs(a), edgeShape);
                snowLine.SetPosition(i, new Vector3(xCenter + a * sceneHalfWidth, y, 0f));
            }
            if (falling != null)
            {
                var emission = falling.emission;
                emission.rateOverTime = winter ? flakesPerSecond : 0f;
                falling.transform.position = new Vector3(xCenter, worldCamera.transform.position.y + worldCamera.orthographicSize + .5f, 0f);
                var shape = falling.shape;
                shape.scale = new Vector3(sceneHalfWidth * 2f, .04f, 0f);
            }
        }

        private void OnDestroy() { if (snowMaterial != null) Destroy(snowMaterial); }
    }
}
