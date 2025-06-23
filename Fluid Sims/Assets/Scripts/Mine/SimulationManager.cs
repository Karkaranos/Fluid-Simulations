/*
 * 
 * HEAVY help from https://www.youtube.com/watch?v=rSKMYc1CQHE
 * */
using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using NaughtyAttributes;

public class SimulationManager : MonoBehaviour
{
    [Foldout("Particle Controls")]
    [Header("Particle Display")]
    [SerializeField] private int _particleNumber;
    [Foldout("Particle Controls")] [SerializeField] private float _particleSize = 1f;
    private Gradient _activeGradient;
    [Foldout("Particle Controls")] [SerializeField] private Gradient _easierVisualizationGradient;
    [Foldout("Particle Controls")] [SerializeField] private Gradient _realisticGradient;
    [Tooltip("True for 2D simulations. False for 3D simulations")] [SerializeField] private bool _is2D;

    // 2D visuals
    [ShowIf("_is2D"), Foldout("2D Particle Controls")] [SerializeField] private Mesh _particleMesh2D;
    [ShowIf("_is2D"), Foldout("2D Particle Controls")] [SerializeField] Shader _particleShader2D;
    private Texture2D _gradientTexture2D;

    // 3D visuals
    [HideIf("_is2D"), Foldout("3D Particle Controls")] [SerializeField] private Mesh _particleMesh3D;
    [HideIf("_is2D"), Foldout("3D Particle Controls")] [SerializeField] Shader _particleShader3D;
    [HideIf("_is2D"), Foldout("3D Particle Controls")] [SerializeField] Color _color;
    private Material _material;
    private Texture _gradientTexture3D;


    [Foldout("Particle Controls")]
    [Header("Particle Simulation")]
    [SerializeField] private int _iterationsPerFrame;
    [Foldout("Particle Controls")] [SerializeField] private float _gravity = -9.8f;
    [Foldout("2D Particle Controls"), ShowIf("_is2D")] [SerializeField] private Vector2 _initialVelocity2D;
    [Foldout("3D Particle Controls"), HideIf("_is2D")] [SerializeField] private Vector3 _initialVelocity3D;
    [Foldout("Particle Controls")] [SerializeField] private float _maxVelocity;
    [Foldout("Particle Controls")] [SerializeField] private float _particleJitter;
    [Foldout("Particle Controls")] [SerializeField, Range(0f, 1f)] private float _collisionDampening = 0.8f;
    [Foldout("Particle Controls")] [SerializeField] private float _smoothingRadius = 3f;
    [Foldout("Particle Controls")] [SerializeField] private float _idealDensity = 1;
    [Foldout("Particle Controls")] [SerializeField] private float _pressure;
    [Foldout("Particle Controls")] [SerializeField] private float _nearParticlePressure;
    [Foldout("Particle Controls")] [SerializeField] private float _viscosity;


    [Header("Bounding Boxes")]
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 _centerOfSpawn;
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 _spawnDimensions;
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 _simulationDimensions;
    [Header("Bounding Boxes")]
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 _centerOfSpawn3D;
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 _spawnDimensions3D;
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 _simulationDimensions3D;
    private Vector2 CenterOfHighO2;
     private Vector2 HighO2;
    private Vector2 CenterOfLowO2;
    private Vector2 LowO2;
    Bounds _boundaries;

    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private ComputeShader _compute2D;
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private ComputeShader _compute3D;


    [Foldout("Heartbeat")] [SerializeField] private float _timeBetweenBeats = 1f;
    [Foldout("Heartbeat")] [SerializeField] private float _devianceFromResting = 7f;
    private Coroutine _heartCouroutine;
    private float defaultDensity;

    public ComputeBuffer positionBuffer { get; private set; }
    public ComputeBuffer velocityBuffer { get; private set; }
    public ComputeBuffer densityBuffer { get; private set; }
    ComputeBuffer predictedPositionBuffer;
    ComputeBuffer spatialIndices;
    ComputeBuffer spatialOffsets;
    GPUSort gpuSort;
    ComputeBuffer _argsBuffer;
    Bounds bounds;



    const int externalForcesKernel = 0;
    const int spatialHashKernel = 1;
    const int densityKernel = 2;
    const int pressureKernel = 3;
    const int viscosityKernel = 4;
    const int updatePositionKernel = 5;


    // Not stolen
    ParticleSpawnInformation2D spawnInformation2D;
    ParticleSpawnInformation3D spawnInformation3D;
    private bool fixedTime = false;
    private bool needsUpdate = true;


    private float interactionRadius = 2;
    private float interactionStrength = 90;
    private Vector2 obstacleSize = Vector2.zero;
    private Vector2 obstacleCentre = Vector2.zero;

    [Header("Functions")]
    private byte s;

    // Start is called before the first frame update
    void Start()
    {


        // Setting Fixed Update to run 60 times per second
        Time.fixedDeltaTime = 1 / 60f;

        _activeGradient = _easierVisualizationGradient;


        if (_is2D)
        {
            GenerateSpawnInformation2D();
            Initialize2DBuffers();
        }
        else
        {
            GenerateSpawnInformation3D();
            Initialize3DBuffers();
        }

        gpuSort = new();
        gpuSort.SetBuffers(spatialIndices, spatialOffsets);

        if(_is2D)
        {
            Initialize2D();
        }
        else
        {
            Initialize3D();
        }
    }


    private void FixedUpdate()
    {
        if(fixedTime)
        {
            if (_is2D)
            {
                RunSimulationFrame2D(Time.fixedDeltaTime);
            }
            else
            {
                RunSimulationFrame3D(Time.fixedDeltaTime);
            }
        }
    }

    void Update()
    {
        // Run simulation if not in fixed timestep mode
        // (skip running for first few frames as deltaTime can be disproportionaly large)
        if (!fixedTime && Time.frameCount > 10)
        {
            if (_is2D)
            {
                RunSimulationFrame2D(Time.deltaTime);
            }
            else
            {
                RunSimulationFrame3D(Time.deltaTime);
            }
        }
    }


    #region 2D Simulations

    private void Initialize2D()
    {
        _material = new Material(_particleShader2D);
        _material.SetBuffer("Positions2D", positionBuffer);
        _material.SetBuffer("Velocities", velocityBuffer);
        _material.SetBuffer("DensityData", densityBuffer);


        _argsBuffer = ComputeHelper.CreateArgsBuffer(_particleMesh2D, positionBuffer.count);
        bounds = new Bounds(Vector3.zero, Vector3.one * 10000);

    }
    private void Initialize2DBuffers()
    {
        positionBuffer = ComputeHelper.CreateStructuredBuffer<float2>(_particleNumber);
        predictedPositionBuffer = ComputeHelper.CreateStructuredBuffer<float2>(_particleNumber);
        velocityBuffer = ComputeHelper.CreateStructuredBuffer<float2>(_particleNumber);
        densityBuffer = ComputeHelper.CreateStructuredBuffer<float2>(_particleNumber);
        spatialIndices = ComputeHelper.CreateStructuredBuffer<uint3>(_particleNumber);
        spatialOffsets = ComputeHelper.CreateStructuredBuffer<uint>(_particleNumber);

        // Set buffer data
        SetInitialBufferData2D(spawnInformation2D);

        // Init compute
        ComputeHelper.SetBuffer(_compute2D, positionBuffer, "Positions", externalForcesKernel, updatePositionKernel);
        ComputeHelper.SetBuffer(_compute2D, predictedPositionBuffer, "PredictedPositions", externalForcesKernel, spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(_compute2D, spatialIndices, "SpatialIndices", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(_compute2D, spatialOffsets, "SpatialOffsets", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(_compute2D, densityBuffer, "Densities", densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(_compute2D, velocityBuffer, "Velocities", externalForcesKernel, pressureKernel, viscosityKernel, updatePositionKernel);

        _compute2D.SetInt("numParticles", _particleNumber);
    }

    //stolen
    private void RunSimulationIteration2D()
    {
        ComputeHelper.Dispatch(_compute2D, _particleNumber, kernelIndex: externalForcesKernel);
        ComputeHelper.Dispatch(_compute2D, _particleNumber, kernelIndex: spatialHashKernel);
        gpuSort.SortAndCalculateOffsets();
        ComputeHelper.Dispatch(_compute2D, _particleNumber, kernelIndex: densityKernel);
        ComputeHelper.Dispatch(_compute2D, _particleNumber, kernelIndex: pressureKernel);
        ComputeHelper.Dispatch(_compute2D, _particleNumber, kernelIndex: viscosityKernel);
        ComputeHelper.Dispatch(_compute2D, _particleNumber, kernelIndex: updatePositionKernel);

    }

    //stolen
    void UpdateSettings2D(float deltaTime)
    {
        _compute2D.SetFloat("deltaTime", deltaTime);
        _compute2D.SetFloat("gravity", _gravity);
        _compute2D.SetFloat("collisionDamping", _collisionDampening);
        _compute2D.SetFloat("smoothingRadius", _smoothingRadius);
        _compute2D.SetFloat("targetDensity", _idealDensity);
        _compute2D.SetFloat("pressureMultiplier", _pressure);
        _compute2D.SetFloat("nearPressureMultiplier", _nearParticlePressure);
        _compute2D.SetFloat("viscosityStrength", _viscosity);
        _compute2D.SetVector("boundsSize", _simulationDimensions);
        _compute2D.SetVector("obstacleSize", obstacleSize);
        _compute2D.SetVector("obstacleCentre", obstacleCentre);

        _compute2D.SetFloat("Poly6ScalingFactor", 4 / (Mathf.PI * Mathf.Pow(_smoothingRadius, 8)));
        _compute2D.SetFloat("SpikyPow3ScalingFactor", 10 / (Mathf.PI * Mathf.Pow(_smoothingRadius, 5)));
        _compute2D.SetFloat("SpikyPow2ScalingFactor", 6 / (Mathf.PI * Mathf.Pow(_smoothingRadius, 4)));
        _compute2D.SetFloat("SpikyPow3DerivativeScalingFactor", 30 / (Mathf.Pow(_smoothingRadius, 5) * Mathf.PI));
        _compute2D.SetFloat("SpikyPow2DerivativeScalingFactor", 12 / (Mathf.Pow(_smoothingRadius, 4) * Mathf.PI));

        
        // Mouse interaction settings:
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        bool isPullInteraction = Input.GetMouseButton(0);
        bool isPushInteraction = Input.GetMouseButton(1);
        float currInteractStrength = 0;
        if (isPushInteraction || isPullInteraction)
        {
            currInteractStrength = isPushInteraction ? -interactionStrength : interactionStrength;
        }

        _compute2D.SetVector("interactionInputPoint", mousePos);
        _compute2D.SetFloat("interactionInputStrength", currInteractStrength);
        _compute2D.SetFloat("interactionInputRadius", interactionRadius);
    }

    //stolen
    void SetInitialBufferData2D(ParticleSpawnInformation2D spawnInformation)
    {
        float2[] allPoints = new float2[spawnInformation.SpawnPositions.Length];
        System.Array.Copy(spawnInformation.SpawnPositions, allPoints, spawnInformation.SpawnPositions.Length);

        positionBuffer.SetData(allPoints);
        predictedPositionBuffer.SetData(allPoints);
        velocityBuffer.SetData(spawnInformation.SpawnVelocities);
    }

    private void RunSimulationFrame2D(float time)
    {
        float timeStep = time / _iterationsPerFrame * Time.timeScale;

        UpdateSettings2D(timeStep);


        for (int i = 0; i < _iterationsPerFrame; i++)
        {
            RunSimulationIteration2D();
        }

    }

    void UpdateSettings2D()
    {
        if (needsUpdate)
        {
            needsUpdate = false;
            TextureFromGradient(ref _gradientTexture2D, 64, _activeGradient);
            _material.SetTexture("ColorMap", _gradientTexture2D);

            _material.SetFloat("scale", _particleSize);
            _material.SetFloat("velocityMax", _maxVelocity);
        }
    }

    #endregion

    #region 3D Simulations

    private void Initialize3D()
    {
        _material = new Material(_particleShader3D);
        _material.SetBuffer("Positions", positionBuffer);
        _material.SetBuffer("Velocities", velocityBuffer);
        _material.SetBuffer("DensityData", densityBuffer);

        //_particleMesh3D = SebStuff.SphereGenerator.GenerateSphereMesh(3);
        _argsBuffer = ComputeHelper.CreateArgsBuffer(_particleMesh3D, positionBuffer.count);
        bounds = new Bounds(Vector3.zero, Vector3.one * 10000);


    }
    private void Initialize3DBuffers()
    {
        positionBuffer = ComputeHelper.CreateStructuredBuffer<float3>(_particleNumber);
        predictedPositionBuffer = ComputeHelper.CreateStructuredBuffer<float3>(_particleNumber);
        velocityBuffer = ComputeHelper.CreateStructuredBuffer<float3>(_particleNumber);
        densityBuffer = ComputeHelper.CreateStructuredBuffer<float2>(_particleNumber);
        spatialIndices = ComputeHelper.CreateStructuredBuffer<uint3>(_particleNumber);
        spatialOffsets = ComputeHelper.CreateStructuredBuffer<uint>(_particleNumber);

        // Set buffer data
        SetInitialBufferData3D(spawnInformation3D);

        // Init compute
        ComputeHelper.SetBuffer(_compute3D, positionBuffer, "Positions", externalForcesKernel, updatePositionKernel);
        ComputeHelper.SetBuffer(_compute3D, predictedPositionBuffer, "PredictedPositions", externalForcesKernel, spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(_compute3D, spatialIndices, "SpatialIndices", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(_compute3D, spatialOffsets, "SpatialOffsets", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(_compute3D, densityBuffer, "Densities", densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(_compute3D, velocityBuffer, "Velocities", externalForcesKernel, pressureKernel, viscosityKernel, updatePositionKernel);

        _compute3D.SetInt("numParticles", positionBuffer.count);
    }

    //stolen
    //  TODO: Switch _particleNum to positionBuffer?
    private void RunSimulationIteration3D()
    {
        ComputeHelper.Dispatch(_compute3D, positionBuffer.count, kernelIndex: externalForcesKernel);
        ComputeHelper.Dispatch(_compute3D, positionBuffer.count, kernelIndex: spatialHashKernel);
        gpuSort.SortAndCalculateOffsets();
        ComputeHelper.Dispatch(_compute3D, positionBuffer.count, kernelIndex: densityKernel);
        ComputeHelper.Dispatch(_compute3D, positionBuffer.count, kernelIndex: pressureKernel);
        ComputeHelper.Dispatch(_compute3D, positionBuffer.count, kernelIndex: viscosityKernel);
        ComputeHelper.Dispatch(_compute3D, positionBuffer.count, kernelIndex: updatePositionKernel);

    }

    private void RunSimulationFrame3D(float time)
    {
        float timeStep = time / _iterationsPerFrame * Time.timeScale;

        UpdateSettings3D(timeStep);


        for (int i = 0; i < _iterationsPerFrame; i++)
        {
            RunSimulationIteration3D();
        }

    }

    //stolen
    void UpdateSettings3D(float deltaTime)
    {
        /* _compute3D.SetFloat("deltaTime", deltaTime);
         _compute3D.SetFloat("gravity", _gravity);
         _compute3D.SetFloat("collisionDamping", _collisionDampening);
         _compute3D.SetFloat("smoothingRadius", _smoothingRadius);
         _compute3D.SetFloat("targetDensity", _idealDensity);
         _compute3D.SetFloat("pressureMultiplier", _pressure);
         _compute3D.SetFloat("nearPressureMultiplier", _nearParticlePressure);
         _compute3D.SetFloat("viscosityStrength", _viscosity);
         _compute3D.SetVector("boundsSize", _simulationDimensions);
         _compute3D.SetVector("obstacleSize", obstacleSize);
         _compute3D.SetVector("obstacleCentre", obstacleCentre);

         _compute3D.SetFloat("Poly6ScalingFactor", 4 / (Mathf.PI * Mathf.Pow(_smoothingRadius, 8)));
         _compute3D.SetFloat("SpikyPow3ScalingFactor", 10 / (Mathf.PI * Mathf.Pow(_smoothingRadius, 5)));
         _compute3D.SetFloat("SpikyPow2ScalingFactor", 6 / (Mathf.PI * Mathf.Pow(_smoothingRadius, 4)));
         _compute3D.SetFloat("SpikyPow3DerivativeScalingFactor", 30 / (Mathf.Pow(_smoothingRadius, 5) * Mathf.PI));
         _compute3D.SetFloat("SpikyPow2DerivativeScalingFactor", 12 / (Mathf.Pow(_smoothingRadius, 4) * Mathf.PI));


         // Mouse interaction settings:
         Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
         bool isPullInteraction = Input.GetMouseButton(0);
         bool isPushInteraction = Input.GetMouseButton(1);
         float currInteractStrength = 0;
         if (isPushInteraction || isPullInteraction)
         {
             currInteractStrength = isPushInteraction ? -interactionStrength : interactionStrength;
         }

         _compute3D.SetVector("interactionInputPoint", mousePos);
         _compute3D.SetFloat("interactionInputStrength", currInteractStrength);
         _compute3D.SetFloat("interactionInputRadius", interactionRadius);*/
        Vector3 simBoundsSize = transform.localScale;
        Vector3 simBoundsCentre = transform.position;

        _compute3D.SetFloat("deltaTime", deltaTime);
        _compute3D.SetFloat("gravity", _gravity);
        _compute3D.SetFloat("collisionDamping", _collisionDampening);
        _compute3D.SetFloat("smoothingRadius", _smoothingRadius);
        _compute3D.SetFloat("targetDensity", _idealDensity);
        _compute3D.SetFloat("pressureMultiplier", _pressure);
        _compute3D.SetFloat("nearPressureMultiplier", _nearParticlePressure);
        _compute3D.SetFloat("viscosityStrength", _viscosity);
        _compute3D.SetVector("boundsSize", simBoundsSize);
        _compute3D.SetVector("centre", simBoundsCentre);

        _compute3D.SetMatrix("localToWorld", transform.localToWorldMatrix);
        _compute3D.SetMatrix("worldToLocal", transform.worldToLocalMatrix);
    }

    //stolen
    void SetInitialBufferData3D(ParticleSpawnInformation3D spawnInformation)
    {
        float3[] allPoints = new float3[spawnInformation3D.SpawnPositions.Length];
        System.Array.Copy(spawnInformation.SpawnPositions, allPoints, spawnInformation.SpawnPositions.Length);

        positionBuffer.SetData(allPoints);
        predictedPositionBuffer.SetData(allPoints);
        velocityBuffer.SetData(spawnInformation.SpawnVelocities);
    }



   

    void UpdateSettings3D()
    {
        /*if (needsUpdate)
        {
            needsUpdate = false;
            //TextureFromGradient(ref _gradientTexture3D, 64, _activeGradient);
            _material.SetTexture("ColorMap", _gradientTexture3D);

            _material.SetFloat("scale", _particleSize);
            _material.SetFloat("velocityMax", _maxVelocity);
        }*/

        if (needsUpdate)
        {
            needsUpdate = false;
            ParticleDisplay2D.TextureFromGradient(ref _gradientTexture2D, 50, _activeGradient);
            _material.SetTexture("ColorMap", _gradientTexture2D);
        }
        _material.SetFloat("scale", _particleSize);
        _material.SetColor("color", _color);
        _material.SetFloat("velocityMax", _maxVelocity);

        
        Vector3 s = transform.localScale;
        transform.localScale = Vector3.one;
        var localToWorld = transform.localToWorldMatrix;
        transform.localScale = s;

        _material.SetMatrix("localToWorld", localToWorld);
    }

    #endregion

    #region HeartBeat
    [Button("Start Beat")]
    private void StartBasicBeatSimulation()
    {
        if (_heartCouroutine == null)
        {
            _heartCouroutine = StartCoroutine(Beat());
        }
    }

    [Button("End Beat")]
    private void EndBasicBeatSimulation()
    {
        StopCoroutine(_heartCouroutine);
        _idealDensity = defaultDensity;
        _heartCouroutine = null;

    }

    [Button("Switch Gradient")]
    private void SwapColors()
    {
        _activeGradient = (_activeGradient == _easierVisualizationGradient ? _realisticGradient : _easierVisualizationGradient);
        needsUpdate = true;
    }
    private IEnumerator Beat()
    {
        defaultDensity = _idealDensity;
        while(true)
        {
            _idealDensity = defaultDensity + _devianceFromResting;
            yield return new WaitForSeconds(_timeBetweenBeats);
            _idealDensity = defaultDensity - _devianceFromResting;
            yield return new WaitForSeconds(_timeBetweenBeats);
        }

    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_is2D)
        {
            // Display the spawn region
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(_centerOfSpawn, Vector2.one * _spawnDimensions);

            // Display the bounding region
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, Vector2.one * _simulationDimensions);

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(CenterOfHighO2, Vector2.one * HighO2);

            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(CenterOfLowO2, Vector2.one * LowO2);
        }
        else
        {
            // Display the spawn region
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(_centerOfSpawn3D, _spawnDimensions3D);

            // Display the bounding region
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, _simulationDimensions3D);

            /*Gizmos.color = Color.red;
            Gizmos.DrawWireCube(CenterOfHighO2, Vector3.one * HighO2);

            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(CenterOfLowO23D, Vector3.one * LowO2);*/
        }
    }
#endif

    #region Spawning

    private void GenerateSpawnInformation2D()
    {
        spawnInformation2D = new ParticleSpawnInformation2D(_particleNumber);
        Unity.Mathematics.Random randomFactor = new Unity.Mathematics.Random(1);


        float2 s = _spawnDimensions;

        // Calculate the required number of particles per row and column
        int particlesSpawnPerRow = Mathf.CeilToInt(Mathf.Sqrt(s.x / s.y * _particleNumber +
            Mathf.Pow((s.x - s.y), 2) / (4 * Mathf.Pow(s.y, 2)) - 
            (s.x - s.y) / (2 * s.y)));
        int particlesSpawnPerColumn = Mathf.CeilToInt(_particleNumber / (float)particlesSpawnPerRow);

        int index = 0;


        // Generate spawn positions and velocity for each particle
        for(int x=0; x<particlesSpawnPerRow; x++)
        {
            for(int y=0; y<particlesSpawnPerColumn; y++)
            {
                if(index >= _particleNumber)
                {
                    break;
                }

                float xPos = particlesSpawnPerRow <= 1 ? .5f : x / (particlesSpawnPerRow - 1.0f);
                float yPos = particlesSpawnPerColumn <= 1 ? .5f : y / (particlesSpawnPerColumn - 1.0f);

                // Generates force to create particle jitter 
                float spawnAngle = (float)randomFactor.NextDouble() * Mathf.PI * 2;
                Vector2 direction = new Vector2(Mathf.Cos(spawnAngle), Mathf.Sin(spawnAngle));
                Vector2 jitter = direction * _particleJitter * ((float)randomFactor.NextDouble() - .5f);

                // Sets the spawn position 
                    // Adds the x/y position, minus .5 to allow for spawning left/down from center
                    // The particle's jitter and offset
                    // The center of the spawnable area
                spawnInformation2D.SpawnPositions[index] = new Vector2((xPos - .5f) * s.x, (yPos - .5f) * 
                    s.y) + jitter + _centerOfSpawn;

                spawnInformation2D.SpawnVelocities[index] = _initialVelocity2D;
                index++;
            }
        }
       
    }

    private void GenerateSpawnInformation3D()
    {
        spawnInformation3D = new ParticleSpawnInformation3D(_particleNumber);
        Unity.Mathematics.Random randomFactor = new Unity.Mathematics.Random(1);


        float3 s = _spawnDimensions3D;

        // Calculate the required number of particles per row and column

        int particlesSpawnPerRow = /*Mathf.CeilToInt(Mathf.Sqrt(s.x / s.y * _particleNumber +
            Mathf.Pow((s.x - s.y), 2) / (4 * Mathf.Pow(s.y, 2)) -
            (s.x - s.y) / (2 * s.y)));*/ (int)Mathf.Pow(_particleNumber, (1f / 3f));
        int particlesSpawnPerWidth = particlesSpawnPerRow;

        int particlesSpawnPerColumn = Mathf.CeilToInt(_particleNumber / ((float)particlesSpawnPerRow*(float)particlesSpawnPerWidth));


        /*int index = 0;

        // Generate spawn positions and velocity for each particle
        for (int x = 0; x < particlesSpawnPerRow; x++)
        {
            for (int y = 0; y < particlesSpawnPerColumn; y++)
            {
                if (index >= _particleNumber)
                {
                    break;
                }

                float xPos = particlesSpawnPerRow <= 1 ? .5f : x / (particlesSpawnPerRow - 1.0f);
                float yPos = particlesSpawnPerColumn <= 1 ? .5f : y / (particlesSpawnPerColumn - 1.0f);

                // Generates force to create particle jitter 
                float spawnAngle = (float)randomFactor.NextDouble() * Mathf.PI * 2;
                Vector3 direction = new Vector2(Mathf.Cos(spawnAngle), Mathf.Sin(spawnAngle));
                Vector3 jitter = direction * _particleJitter * ((float)randomFactor.NextDouble() - .5f);

                // Sets the spawn position 
                // Adds the x/y position, minus .5 to allow for spawning left/down from center
                // The particle's jitter and offset
                // The center of the spawnable area
                spawnInformation3D.SpawnPositions[index] = new Vector3((xPos - .5f) * s.x, (yPos - .5f) *
                    s.y, 0) + jitter + _centerOfSpawn;

                spawnInformation3D.SpawnVelocities[index] = _initialVelocity3D;
                index++;
            }
        }*/
        int i = 0;

        for (int x = 0; x < particlesSpawnPerRow; x++)
        {
            for (int y = 0; y < particlesSpawnPerColumn; y++)
            {
                for (int z = 0; z < particlesSpawnPerWidth; z++)
                {
                    if(i >= _particleNumber)
                    {
                        return;
                    }
                    float tx = x / (particlesSpawnPerRow - 1f);
                    float ty = y / (particlesSpawnPerColumn - 1f);
                    float tz = z / (particlesSpawnPerWidth - 1f);

                    float px = (tx - 0.5f) * _spawnDimensions3D.x + _centerOfSpawn3D.x;
                    float py = (ty - 0.5f) * _spawnDimensions3D.y + _centerOfSpawn3D.y;
                    float pz = (tz - 0.5f) * _spawnDimensions3D.z + _centerOfSpawn3D.z;
                    float3 jitter = UnityEngine.Random.insideUnitSphere * _particleJitter;
                    spawnInformation3D.SpawnPositions[i] = new float3(px, py, pz) + jitter;
                    spawnInformation3D.SpawnVelocities[i] = _initialVelocity3D;
                    i++;
                }
            }
        }

    }

    #endregion

    #region Visuals

    void LateUpdate()
    {
        if (_is2D && _particleShader2D != null)
        {
            UpdateSettings2D();
            Graphics.DrawMeshInstancedIndirect(_particleMesh2D, 0, _material, bounds, _argsBuffer);
        }
        else if (!_is2D)
        {
            UpdateSettings3D();
            Graphics.DrawMeshInstancedIndirect(_particleMesh3D, 0, _material, bounds, _argsBuffer);
        }
    }

    void OnValidate()
    {
        needsUpdate = true;
    }


    public static void TextureFromGradient(ref Texture2D texture, int width, Gradient gradient, FilterMode filterMode = FilterMode.Bilinear)
    {
        if (texture == null)
        {
            texture = new Texture2D(width, 1);
        }
        else if (texture.width != width)
        {
            texture.Reinitialize(width, 1);
        }
        if (gradient == null)
        {
            gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.black, 0), new GradientColorKey(Color.black, 1) },
                new GradientAlphaKey[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) }
            );
        }
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = filterMode;

        Color[] cols = new Color[width];
        for (int i = 0; i < cols.Length; i++)
        {
            float t = i / (cols.Length - 1f);
            cols[i] = gradient.Evaluate(t);
        }
        texture.SetPixels(cols);
        texture.Apply();
    }

    #endregion

    void OnDestroy()
    {
        ComputeHelper.Release(positionBuffer, predictedPositionBuffer, velocityBuffer, densityBuffer, spatialIndices, spatialOffsets);
        ComputeHelper.Release(_argsBuffer);
    }


}

public struct ParticleSpawnInformation2D
{
    public float2[] SpawnPositions;
    public float2[] SpawnVelocities;

    /// <summary>
    /// Constructor for this struct
    /// Defines float2 arrays with the appropriate particle count
    /// </summary>
    /// <param name="particleCount">The maximum number of particles</param>
    public ParticleSpawnInformation2D(int particleCount)
    {
        SpawnPositions = new float2[particleCount];
        SpawnVelocities = new float2[particleCount];
    }
}

public struct ParticleSpawnInformation3D
{
    public float3[] SpawnPositions;
    public float3[] SpawnVelocities;

    /// <summary>
    /// Constructor for this struct
    /// Defines float2 arrays with the appropriate particle count
    /// </summary>
    /// <param name="particleCount">The maximum number of particles</param>
    public ParticleSpawnInformation3D(int particleCount)
    {
        SpawnPositions = new float3[particleCount];
        SpawnVelocities = new float3[particleCount];
    }
}
