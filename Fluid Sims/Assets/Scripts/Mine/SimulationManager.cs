/* Author :             Cade Naylor
 * Last Modified :      June 23, 2025
 * Description :        This file contains all logic for 2D and 3D fluid simulations. It
 *                          - Generates Spawn positions
 *                          - Generates Materials from Gradients
 *                          - Updates the renderer at the specified number of times per frame
 *                          - Controls and visualizes boundaries
 * 
 * Resources Used :     https://www.youtube.com/watch?v=_8v4DRhHu2g&t=873s
 *                      https://www.youtube.com/watch?v=rSKMYc1CQHE
 *                      https://www.youtube.com/watch?v=zbBwKMRyavE&t=178s
 *
 * TODO:                Optimization
 *                          - Restructure code by code function, not dimensions, after refactor complete
 *                          - Region headers
 *                      Shader Updates
 *                          - Create buffers for High/Low o2 zones instead of just having it hardcoded on shaders
 *                          - Update references when initializing materials
 *                      Fluid Properties
 *                          - Blood is sort of non-newtonian. It is viscoelastic
 *                          - Shear-thinning
 *                      Heartbeat Simulation
 *                          - Needs easier and more realistic controls
 *                      Obstacles
 *                          - Obstacle detection at runtime will allow for more customizability and greater use cases
 *                          - Convert game object positions or colliders to vectors and pass them to the computeBuffer?
 * */
using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using NaughtyAttributes;
using System;
using UnityEngine.InputSystem;

public class SimulationManager : MonoBehaviour
{
    #region Variables
    [Foldout("Particle Controls")]
    [Header("Particle Display")]
    [SerializeField] private int _particleNumber;
    [Foldout("Particle Controls")] [SerializeField] private float _particleSize = 1f;
    private Gradient _activeGradient;
    [Foldout("Particle Controls")] [SerializeField] private Gradient _easierVisualizationGradient;
    [Foldout("Particle Controls")] [SerializeField] private Gradient _realisticGradient;
    [Tooltip("True for 2D simulations. False for 3D simulations")] [SerializeField] private bool _is2D;
    private bool _savedIs2D;

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
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 _centerOfSpawn2D;
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 _spawnDimensions2D;
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 _simulationDimensions2D;
    [Header("Oxygen Controls")]
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 CenterOfHighO2_2D;
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 HighO2_2D;
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 CenterOfLowO2_2D;
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 LowO2_2D;
    [Header("Bounding Boxes")]
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 _centerOfSpawn3D;
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 _spawnDimensions3D;
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 _simulationDimensions3D;
    [Header("Oxygen Controls")]
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 CenterOfHighO2_3D;
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 HighO2_3D;
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 CenterOfLowO2_3D;
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 LowO2_3D;

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
    public ComputeBuffer highO2 { get; private set; }
    public ComputeBuffer highO2Distance { get; private set; }
    public ComputeBuffer lowO2 { get; private set; }
    public ComputeBuffer lowO2Distance { get; private set; }
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

    int frameCount;

    //[Header("Functions")]
    private byte s;

    #endregion

    #region Testing Variables
    private InputActionMap _uMap;
    private InputAction _mousePos;
    private InputAction _mouseDelta;
    [SerializeField] private GameObject objVisualization;

    private Vector2 _mDelta;
    private Vector2 _mPos;

    #endregion

    // Update with updated GenerateSpawnInformation(), needs function headers
    #region Initialization 
    // Start is called before the first frame update
    void Start()
    {
        _savedIs2D = _is2D;
        Time.fixedDeltaTime = 1 / 60f;

        _activeGradient = _easierVisualizationGradient;

        Init();

        _uMap = GetComponent<PlayerInput>().currentActionMap;
        _uMap.Enable();
        _mousePos = _uMap.FindAction("MousePos");
        _mouseDelta = _uMap.FindAction("MouseDelta");
    }

    private void Init()
    {
        if (_is2D)
        {
            GenerateSpawnInformation2D();
            InitializeBuffers<float2>(ref _compute2D);
        }
        else
        {
            GenerateSpawnInformation3D();
            InitializeBuffers<float3>(ref _compute3D);
        }

        gpuSort = new();
        gpuSort.SetBuffers(spatialIndices, spatialOffsets);

        if (_is2D)
        {
            Initialize(_particleShader2D, _particleMesh2D);
        }
        else
        {
            Initialize(_particleShader3D, _particleMesh3D);
        }
    }

    private void Initialize(Shader shader, Mesh mesh)
    {
        _material = new Material(shader);
        _material.SetBuffer("Positions" + (_is2D? "2D" : ""), positionBuffer);
        _material.SetBuffer("Velocities", velocityBuffer);
        _material.SetBuffer("DensityData", densityBuffer);
        //TODO: Add high/low o2 positions to material

        _argsBuffer = ComputeHelper.CreateArgsBuffer(mesh, positionBuffer.count);
        bounds = new Bounds(Vector3.zero, Vector3.one * 10000);
    }

    private void InitializeBuffers<T>(ref ComputeShader compShader)
    {
        positionBuffer = ComputeHelper.CreateStructuredBuffer<T>(_particleNumber);
        predictedPositionBuffer = ComputeHelper.CreateStructuredBuffer<T>(_particleNumber);
        velocityBuffer = ComputeHelper.CreateStructuredBuffer<T>(_particleNumber);
        densityBuffer = ComputeHelper.CreateStructuredBuffer<float2>(_particleNumber);
        spatialIndices = ComputeHelper.CreateStructuredBuffer<uint3>(_particleNumber);
        spatialOffsets = ComputeHelper.CreateStructuredBuffer<uint>(_particleNumber);

        if (_is2D)
        {
            InitializeBufferData<float2>(spawnInformation2D);
        }
        else
        {
            InitializeBufferData<float3>(spawnInformation3D);
        }


        // Initialize Buffers
        ComputeHelper.SetBuffer(compShader, positionBuffer, "Positions", externalForcesKernel, updatePositionKernel);
        ComputeHelper.SetBuffer(compShader, predictedPositionBuffer, "PredictedPositions", externalForcesKernel, spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(compShader, spatialIndices, "SpatialIndices", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(compShader, spatialOffsets, "SpatialOffsets", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(compShader, densityBuffer, "Densities", densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(compShader, velocityBuffer, "Velocities", externalForcesKernel, pressureKernel, viscosityKernel, updatePositionKernel);
        // TODO: Initialize high/low o2 areas as buffers here

        compShader.SetInt("numParticles", (_is2D ? _particleNumber : positionBuffer.count)); 
    }

    void InitializeBufferData<T>(ParticleSpawnInformation<T> spawnInformation)
    {
        T[] allPoints = new T[spawnInformation.SpawnPositions.Length];
        Array.Copy(spawnInformation.SpawnPositions, allPoints, spawnInformation.SpawnPositions.Length);

        positionBuffer.SetData(allPoints);
        predictedPositionBuffer.SetData(allPoints);
        velocityBuffer.SetData(spawnInformation.SpawnVelocities);
    }

    #endregion

    // Update after refactoring Settings, needs function headers
    #region Frame Updates
    private void FixedUpdate()
    {
        if(fixedTime)
        {
            RunFrame(Time.fixedDeltaTime, (_is2D ? _compute2D : _compute3D), (_is2D? _particleNumber : positionBuffer.count));
        }
    }

    void Update()
    {
        // Run simulation if not in fixed timestep mode
        // (skip running for first few frames as deltaTime can be disproportionaly large)
        if (!fixedTime && frameCount > 10)
        {
            RunFrame(Time.deltaTime, (_is2D ? _compute2D : _compute3D), (_is2D ? _particleNumber : positionBuffer.count));
        }

        _mPos = Camera.main.ScreenToWorldPoint(_mousePos.ReadValue<Vector2>());
        _mDelta = _mouseDelta.ReadValue<Vector2>();
        Vector3 saveVel = _mDelta * 2;
        saveVel.z = 0;
        
        objVisualization.GetComponent<Rigidbody>().velocity = saveVel;
        bool neededUpdate = false;
        Vector3 adjustedPos = objVisualization.transform.position;
        if(Mathf.Abs(adjustedPos.x) > _simulationDimensions2D.x*.5f)
        {
            adjustedPos.x = (adjustedPos.x > _simulationDimensions2D.x*.5f ? 1 : -1) * _simulationDimensions2D.x*.5f;
            neededUpdate = true;
        }
        if (Mathf.Abs(adjustedPos.y) > _simulationDimensions2D.y*.5f)
        {
            adjustedPos.y = (adjustedPos.y > _simulationDimensions2D.y*.5f ? 1 : -1) * _simulationDimensions2D.y*.5f;
            neededUpdate = true;
        }
        if(neededUpdate)
        {
            objVisualization.transform.position = adjustedPos;
        }

        frameCount++;

    }

    void LateUpdate()
    {
        if (_is2D && _particleShader2D != null)
        {
            UpdateSettings();
            Graphics.DrawMeshInstancedIndirect(_particleMesh2D, 0, _material, bounds, _argsBuffer);
        }
        else if (!_is2D)
        {
            UpdateSettings();
            Graphics.DrawMeshInstancedIndirect(_particleMesh3D, 0, _material, bounds, _argsBuffer);
        }
    }

    #endregion

    // needs function headers
    #region Iterations and Frames
    private void RunIteration(ComputeShader shader, int iterationCount)
    {
        ComputeHelper.Dispatch(shader, iterationCount, kernelIndex: externalForcesKernel);
        ComputeHelper.Dispatch(shader, iterationCount, kernelIndex: spatialHashKernel);
        gpuSort.SortAndCalculateOffsets();
        ComputeHelper.Dispatch(shader, iterationCount, kernelIndex: densityKernel);
        ComputeHelper.Dispatch(shader, iterationCount, kernelIndex: pressureKernel);
        ComputeHelper.Dispatch(shader, iterationCount, kernelIndex: viscosityKernel);
        ComputeHelper.Dispatch(shader, iterationCount, kernelIndex: updatePositionKernel);
    }

    private void RunFrame(float time, ComputeShader compute, int iterationCount)
    {
        float timeStep = time / _iterationsPerFrame * Time.timeScale;

        UpdateSettings(timeStep, compute);

        for(int i=0; i<_iterationsPerFrame; i++)
        {
            RunIteration(compute, iterationCount);
        }
    }

    #endregion

    // needs function headers
    #region Settings
    private void UpdateSettings(float deltaTime, ComputeShader compute)
    {
        compute.SetFloat("deltaTime", deltaTime);
        compute.SetFloat("gravity", _gravity);
        compute.SetFloat("collisionDamping", _collisionDampening);
        compute.SetFloat("smoothingRadius", _smoothingRadius);
        compute.SetFloat("targetDensity", _idealDensity);
        compute.SetFloat("pressureMultiplier", _pressure);
        compute.SetFloat("nearPressureMultiplier", _nearParticlePressure);
        compute.SetFloat("viscosityStrength", _viscosity);
        compute.SetVector("boundsSize", (_is2D? _simulationDimensions2D : _simulationDimensions3D));

        if(_is2D)
        {
            compute.SetFloat("Poly6ScalingFactor", 4 / (Mathf.PI * Mathf.Pow(_smoothingRadius, 8)));
            compute.SetFloat("SpikyPow3ScalingFactor", 10 / (Mathf.PI * Mathf.Pow(_smoothingRadius, 5)));
            compute.SetFloat("SpikyPow2ScalingFactor", 6 / (Mathf.PI * Mathf.Pow(_smoothingRadius, 4)));
            compute.SetFloat("SpikyPow3DerivativeScalingFactor", 30 / (Mathf.Pow(_smoothingRadius, 5) * Mathf.PI));
            compute.SetFloat("SpikyPow2DerivativeScalingFactor", 12 / (Mathf.Pow(_smoothingRadius, 4) * Mathf.PI));
        }
        else
        {
            compute.SetVector("centre", Vector3.zero);

            compute.SetMatrix("localToWorld", transform.localToWorldMatrix);
            compute.SetMatrix("worldToLocal", transform.worldToLocalMatrix);
        }

    }

    private void UpdateSettings()
    {
        if(needsUpdate)
        {
            needsUpdate = false;
            TextureFromGradient(ref _gradientTexture2D, 64, _activeGradient);
            _material.SetTexture("ColourMap", _gradientTexture2D);
            _material.SetFloat("scale", _particleSize);
            _material.SetFloat("velocityMax", _maxVelocity);
        }

        if(!_is2D)
        {
            _material.SetColor("colour", _color);
            Vector3 s = transform.localScale;
            transform.localScale = Vector3.one;
            var localToWorld = transform.localToWorldMatrix;
            transform.localScale = s;

            _material.SetMatrix("localToWorld", localToWorld);
        }
    }

    #endregion

    // Needs function headers
    #region Heartbeat
    [Button("Start Beat")]
    private void StartBasicBeatSimulation()
    {
        if (_heartCouroutine == null)
        {
            _heartCouroutine = StartCoroutine(Beat());
        }
    }

    //[Button("End Beat")]
    private void EndBasicBeatSimulation()
    {
        StopCoroutine(_heartCouroutine);
        _idealDensity = defaultDensity;
        _heartCouroutine = null;

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

    // needs work as it is janky, needs function headers
    #region Simulation Button Controls
    [Button("Switch Gradient")]
    private void SwapColors()
    {
        _activeGradient = (_activeGradient == _easierVisualizationGradient ? _realisticGradient : _easierVisualizationGradient);
        needsUpdate = true;
    }

    // Needs work. This likes working after MANUALLY switching between 2/3d a couple times
    [Button("Resimulate")]
    private void RestartSimulation()
    {
        ClearLastData();
        Init();
    }

    private void ClearLastData()
    {
        ComputeHelper.Release(positionBuffer, predictedPositionBuffer, velocityBuffer, densityBuffer, spatialIndices, spatialOffsets);
        ComputeHelper.Release(_argsBuffer, highO2, lowO2, highO2Distance, lowO2Distance);
        if (_savedIs2D != _is2D)
        {
            if(_savedIs2D)
            {
                spawnInformation2D.SpawnPositions = null;
                spawnInformation2D.SpawnVelocities = null;
            }
            else
            {
                spawnInformation3D.SpawnPositions = null;
                spawnInformation3D.SpawnVelocities = null;
            }
            _savedIs2D = _is2D;
        }
    }

    #endregion

    // Needs function headers, rework 3D to calculate for non-cube spawn shapes
    #region Spawning

    private void GenerateSpawnInformation2D()
    {
        spawnInformation2D = new ParticleSpawnInformation2D(_particleNumber);
        Unity.Mathematics.Random randomFactor = new Unity.Mathematics.Random(1);


        float2 s = _spawnDimensions2D;

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
                    s.y) + jitter + _centerOfSpawn2D;

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

    // Needs function headers
    #region Visuals
    void OnValidate()
    {
        needsUpdate = true;
    }

    //stolen
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

    // Needs function headers
    #region Unity Stuff
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_is2D)
        {
            // Display the spawn region
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(_centerOfSpawn2D, Vector2.one * _spawnDimensions2D);

            // Display the bounding region
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, Vector2.one * _simulationDimensions2D);

            Gizmos.color = Color.red;
            //Gizmos.DrawWireCube(CenterOfHighO2_2D, Vector2.one * HighO2_2D);

            Gizmos.color = Color.blue;
            //Gizmos.DrawWireCube(CenterOfLowO2_2D, Vector2.one * LowO2_2D);
        }
        else
        {
            // Display the spawn region
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(_centerOfSpawn3D, _spawnDimensions3D);


            // Display the bounding region
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, _simulationDimensions3D);

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(CenterOfHighO2_3D, HighO2_3D);

            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(CenterOfLowO2_3D, LowO2_3D);
        }
    }
#endif

    void OnDestroy()
    {
        ComputeHelper.Release(positionBuffer, predictedPositionBuffer, velocityBuffer, densityBuffer, spatialIndices, spatialOffsets);
        ComputeHelper.Release(_argsBuffer, highO2, lowO2, highO2Distance, lowO2Distance);
    }

    #endregion
}

// Needs class headers
#region ParticleSpawnInformation Classes
public class ParticleSpawnInformation<T>
{
    public T[] SpawnPositions;
    public T[] SpawnVelocities;
}

public class ParticleSpawnInformation2D : ParticleSpawnInformation <float2>
{
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

public class  ParticleSpawnInformation3D : ParticleSpawnInformation<float3>
{
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
#endregion
