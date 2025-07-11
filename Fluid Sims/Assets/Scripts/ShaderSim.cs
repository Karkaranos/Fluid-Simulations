/* Author :             Cade Naylor
 * Last Modified :      June 28, 2025
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
 * TODO:                Shader Updates
 *                          - Create buffers for High/Low o2 zones instead of just having it hardcoded on shaders
 *                          - Update references when initializing materials
 *                      Fluid Properties
 *                          - Blood is sort of non-newtonian. It is viscoelastic
 *                          - Shear-thinning
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
using System.Collections.Generic;

public class SimulationManager : MonoBehaviour
{

    #region OBSOLETE VARIABLES
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 CenterOfHighO2_2D;
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 HighO2_2D;
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 CenterOfLowO2_2D;
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 LowO2_2D;
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 CenterOfHighO2_3D;
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 HighO2_3D;
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 CenterOfLowO2_3D;
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 LowO2_3D;
    #endregion


    #region Variables
    // General visual Controls
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
    [Foldout("2D Particle Controls"), ShowIf("_is2D")] [SerializeField] private Vector2 _initialVelocity2D;
    private Texture2D _gradientTexture2D;

    // 3D visuals
    [HideIf("_is2D"), Foldout("3D Particle Controls")] [SerializeField] private Mesh _particleMesh3D;
    [HideIf("_is2D"), Foldout("3D Particle Controls")] [SerializeField] Shader _particleShader3D;
    [HideIf("_is2D"), Foldout("3D Particle Controls")] [SerializeField] Color _color;
    [Foldout("3D Particle Controls"), HideIf("_is2D")] [SerializeField] private Vector3 _initialVelocity3D;
    private Material _material;

    // General Particle Simulation controls
    [Header("Particle Simulation")]
    [Foldout("Particle Controls")] [SerializeField] private int _iterationsPerFrame;
    [Foldout("Particle Controls")] [SerializeField] private float _gravity = -9.8f;
    [Foldout("Particle Controls")] [SerializeField] private float _maxVelocity;
    [Foldout("Particle Controls")] [SerializeField] private float _particleJitter;
    [Foldout("Particle Controls")] [SerializeField, Range(0f, 1f)] private float _collisionDampening = 0.8f;
    [Foldout("Particle Controls")] [SerializeField] private float _smoothingRadius = 3f;
    [Foldout("Particle Controls")] [SerializeField] private float _idealDensity = 1;
    [Foldout("Particle Controls")] [SerializeField] private float _pressure;
    [Foldout("Particle Controls")] [SerializeField] private float _nearParticlePressure;
    [Foldout("Particle Controls")] [SerializeField] private float _viscosity;

    // 2D Bounding Boxes and O2 Zones
    [Header("Bounding Boxes")]
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 _centerOfSpawn2D;
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 _spawnDimensions2D;
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 _simulationDimensions2D;
    [Header("Oxygen Controls")]
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private ZoneInformation2D _zoneInformation2D;

    // 3D Bounding Boxes and O2 Zones
    [Header("Bounding Boxes")]
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 _centerOfSpawn3D;
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 _spawnDimensions3D;
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 _simulationDimensions3D;
    [Header("Oxygen Controls")]
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private ZoneInformation3D _zoneInformation3D;

    Bounds _boundaries;

    // ComputeShaders
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private ComputeShader _compute2D;
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private ComputeShader _compute3D;

    // Heartbeat Controls
    [Foldout("Heartbeat")] [SerializeField] private float _timeBetweenBeats = 1f;
    [Foldout("Heartbeat")] [SerializeField] private float _devianceFromResting = 7f;
    private Coroutine _heartCouroutine;
    private float defaultDensity;

    #region Buffers and Kernels
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
    #endregion

    ParticleSpawnInformation2D spawnInformation2D;
    ParticleSpawnInformation3D spawnInformation3D;
    private bool fixedTime = false;
    private bool needsUpdate = true;

    int frameCount;

    // This just makes it look nicer lol
    [Header("Functions")]
    private byte s;

    #endregion

    // Variables used to test gameObject proximity
    #region Testing Variables
    private InputActionMap _uMap;
    private InputAction _mousePos;
    private InputAction _mouseDelta;
    [SerializeField] private GameObject objVisualization;

    private Vector2 _mDelta;
    private Vector2 _mPos;

    #endregion

    #region Initialization 
    /// <summary>
    /// Called at the first frame update
    /// Sets whether the current simulation is 2D/3D
    /// Sets the frame rate
    /// Sets active gradient
    /// Initializes Input 
    /// </summary>
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

    /// <summary>
    /// Extracted function from Start
    /// Generates SpawnInformation and sets buffers
    /// </summary>
    private void Init()
    {
        if (_is2D)
        {
            GenerateSpawnInformation2D();
            InitializeBuffers<float2>(_compute2D);
        }
        else
        {
            GenerateSpawnInformation3D();
            InitializeBuffers<float3>(_compute3D);
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

    /// <summary>
    /// Initializes variables on the appropriate Shaders
    /// TODO: Set bounds for 2D
    /// 3D: Math derived from the post from Trey Reynolds at the below link
    ///     https://math.stackexchange.com/questions/1472049/check-if-a-point-is-inside-a-rectangular-shaped-area-3d
    /// </summary>
    /// <param name="shader">The shader to initialize, either 2D or 3D</param>
    /// <param name="mesh">The mesh to initialize, either 2D or 3D</param>
    private void Initialize(Shader shader, Mesh mesh)
    {
        // General information
        _material = new Material(shader);
        _material.SetBuffer("Positions" + (_is2D? "2D" : ""), positionBuffer);
        _material.SetBuffer("Velocities", velocityBuffer);
        _material.SetBuffer("DensityData", densityBuffer);

        if (_is2D)
        {
            // TODO: Handle setting of 2D High/Low o2 zones here
        }
        else
        {
            Vector3[] points = new Vector3[4];
            Vector3[] edges = new Vector3[3];
            Vector3[] knownDots = new Vector3[3];

            // Generates p1, p2, p4, p5
            for (int i=0; i<4; i++)
            {
                points[i] = ListToVector3(GetPointData(i + 1, CenterOfHighO2_3D, HighO2_3D));
            }

            edges[0] = points[1] - points[0];   // p2-p1, i
            edges[1] = points[2] - points[0];   // p4-p1, j
            edges[2] = points[3] - points[0];   // p5-p1, k

            for (int i=0; i<3; i++)
            {
                knownDots[i] = DotProduct(edges[0], edges[0]); // Generates i.i, j.j, k.k
            }

            // Assigns the information on the material
            for(int i=0; i<4; i++)
            {
                _material.SetFloatArray("Highv" + (i + 1), Vector3ToList(points[i]));
            }

            int index = 0;
            for(char c = 'i'; c<'l'; c++, index++)
            {
                Debug.Log(c);
                _material.SetFloatArray(c, Vector3ToList(edges[index]));
                _material.SetFloatArray(c + "2", Vector3ToList(knownDots[index]));
            }
            _material.SetFloatArray("CenterOfHighO2", Vector3ToList(CenterOfLowO2_3D));

        }

        // Set arguments
        _argsBuffer = ComputeHelper.CreateArgsBuffer(mesh, positionBuffer.count);
        bounds = new Bounds(Vector3.zero, Vector3.one * 10000);
    }

    /// <summary>
    /// Creates the structuredBuffers and initializes them with the desired dimension
    /// </summary>
    /// <typeparam name="T">Float2 if 2D, Float3 if 3D.</typeparam>
    /// <param name="compShader">The ComputeShader to initialize information for</param>
    private void InitializeBuffers<T>(ComputeShader compShader)
    {
        positionBuffer = ComputeHelper.CreateStructuredBuffer<T>(_particleNumber);
        predictedPositionBuffer = ComputeHelper.CreateStructuredBuffer<T>(_particleNumber);
        velocityBuffer = ComputeHelper.CreateStructuredBuffer<T>(_particleNumber);
        densityBuffer = ComputeHelper.CreateStructuredBuffer<float2>(_particleNumber);
        spatialIndices = ComputeHelper.CreateStructuredBuffer<uint3>(_particleNumber);
        spatialOffsets = ComputeHelper.CreateStructuredBuffer<uint>(_particleNumber);

        // Set initial buffer data
        // Unfortunately it didn't like the Tertiary operator due to type ambiguity so it had to be done this way
        if (_is2D)
        {
            InitializeBufferData(spawnInformation2D);
        }
        else
        {
            InitializeBufferData(spawnInformation3D);
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

    /// <summary>
    /// Initialize data in the ComputeBuffers given the spawn information
    /// </summary>
    /// <typeparam name="T">Float2 if 2D, Float3 if 3D. The type that the child ParticleSpawnInformation class uses</typeparam>
    /// <param name="spawnInformation">The spawn information to use for initial positions and velocities</param>
    void InitializeBufferData<T>(ParticleSpawnInformation<T> spawnInformation)
    {
        T[] allPoints = new T[spawnInformation.SpawnPositions.Length];
        Array.Copy(spawnInformation.SpawnPositions, allPoints, spawnInformation.SpawnPositions.Length);

        positionBuffer.SetData(allPoints);
        predictedPositionBuffer.SetData(allPoints);
        velocityBuffer.SetData(spawnInformation.SpawnVelocities);
    }

    #endregion

    // Rework 3D to calculate for non-cube spawn shapes & give it a header
    #region Spawning

    /// <summary>
    /// Generates all spawn information for 2D particle simulations
    /// Assigns initial spawn positions and velocities for each particle out of the specified number
    /// </summary>
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

        int particlesSpawnPerColumn = Mathf.CeilToInt(_particleNumber / ((float)particlesSpawnPerRow *
            (float)particlesSpawnPerWidth));


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
                    if (i >= _particleNumber)
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

    // Update needs color proximity- use the Shader as inspiration
    #region Frame Updates
    /// <summary>
    /// Runs the simulation if a fixed framerate is not being used
    /// Sets a gameObject to the mouse position for basic proximity detection
    /// </summary>
    void Update()
    {
        // Run simulation if not in fixed timestep mode
        // (skip running for first few frames as deltaTime can be disproportionaly large)
        if (!fixedTime && frameCount > 10)
        {
            RunFrame(Time.deltaTime, (_is2D ? _compute2D : _compute3D), (_is2D ? _particleNumber : positionBuffer.count));
        }

        // move GameObject and adjust its color based on proximity to regions
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

        float d = (adjustedPos - CenterOfHighO2_3D).magnitude;
        float col = 1.0f-(d / 10f);
        objVisualization.GetComponent<MeshRenderer>().material.color = _activeGradient.Evaluate(col);

        frameCount++;
    }

    /// <summary>
    /// Runs the simulation at a fixed framerate
    /// </summary>
    private void FixedUpdate()
    {
        if (fixedTime)
        {
            RunFrame(Time.fixedDeltaTime, (_is2D ? _compute2D : _compute3D), (_is2D ? _particleNumber : positionBuffer.count));
        }
    }

    /// <summary>
    /// Called after Update
    /// Updates visual settings and draws the particles
    /// </summary>
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

    #region Iterations and Frames
    /// <summary>
    /// Runs one frame in the simulation
    /// </summary>
    /// <param name="time">The amount of time elapsed since the last frame</param>
    /// <param name="compute">The ComputeShader to use, can be 2D or 3D</param>
    /// <param name="particleCount">The number of particles</param>
    private void RunFrame(float time, ComputeShader compute, int particleCount)
    {
        float timeStep = time / _iterationsPerFrame * Time.timeScale;

        UpdateSettings(timeStep, compute);

        for(int i=0; i<_iterationsPerFrame; i++)
        {
            RunIteration(compute, particleCount);
        }
    }

    /// <summary>
    /// Runs one iteration of a frame in the simulation
    /// Updates the ComputeHelper and sets the appropriate Kernel values on the shader
    /// </summary>
    /// <param name="shader">The ComputeShader to use, 2D or 3D</param>
    /// <param name="particleCount">The number of particles</param>
    private void RunIteration(ComputeShader shader, int particleCount)
    {
        ComputeHelper.Dispatch(shader, particleCount, kernelIndex: externalForcesKernel);
        ComputeHelper.Dispatch(shader, particleCount, kernelIndex: spatialHashKernel);
        gpuSort.SortAndCalculateOffsets();
        ComputeHelper.Dispatch(shader, particleCount, kernelIndex: densityKernel);
        ComputeHelper.Dispatch(shader, particleCount, kernelIndex: pressureKernel);
        ComputeHelper.Dispatch(shader, particleCount, kernelIndex: viscosityKernel);
        ComputeHelper.Dispatch(shader, particleCount, kernelIndex: updatePositionKernel);
    }

    #endregion

    #region Settings
    /// <summary>
    /// Updates the Simulation settings as needed per unit of time
    /// </summary>
    /// <param name="deltaTime">The amount of time elapsed since last call</param>
    /// <param name="compute">The Compute shader to use, either 2D or 3D</param>
    private void UpdateSettings(float deltaTime, ComputeShader compute)
    {
        // Updates for both 2D and 3D
        compute.SetFloat("deltaTime", deltaTime);
        compute.SetFloat("gravity", _gravity);
        compute.SetFloat("collisionDamping", _collisionDampening);
        compute.SetFloat("smoothingRadius", _smoothingRadius);
        compute.SetFloat("targetDensity", _idealDensity);
        compute.SetFloat("pressureMultiplier", _pressure);
        compute.SetFloat("nearPressureMultiplier", _nearParticlePressure);
        compute.SetFloat("viscosityStrength", _viscosity);
        compute.SetVector("boundsSize", (_is2D? _simulationDimensions2D : _simulationDimensions3D));

        // Ensure scaling factors are correct for 2D
        if(_is2D)
        {
            compute.SetFloat("Poly6ScalingFactor", 4 / (Mathf.PI * Mathf.Pow(_smoothingRadius, 8)));
            compute.SetFloat("SpikyPow3ScalingFactor", 10 / (Mathf.PI * Mathf.Pow(_smoothingRadius, 5)));
            compute.SetFloat("SpikyPow2ScalingFactor", 6 / (Mathf.PI * Mathf.Pow(_smoothingRadius, 4)));
            compute.SetFloat("SpikyPow3DerivativeScalingFactor", 30 / (Mathf.Pow(_smoothingRadius, 5) * Mathf.PI));
            compute.SetFloat("SpikyPow2DerivativeScalingFactor", 12 / (Mathf.Pow(_smoothingRadius, 4) * Mathf.PI));
        }

    }

    /// <summary>
    /// Update the simulation settings if the Inspector is changed
    /// Can adjust color, particle size, and maximum velocity
    /// </summary>
    private void UpdateSettings()
    {
        if(needsUpdate)
        {
            needsUpdate = false;
            TextureFromGradient(64, _activeGradient);
            _material.SetTexture("ColourMap", _gradientTexture2D);
            _material.SetFloat("scale", _particleSize);
            _material.SetFloat("velocityMax", _maxVelocity);
        }

        // Handles updating values on the 3D shader 
        if(!_is2D)
        {
            _material.SetColor("colour", _color);
        }
    }

    #endregion

    #region Visuals
    /// <summary>
    /// Converts a Gradient into a Texture2D at a specified width
    /// https://www.youtube.com/watch?v=rSKMYc1CQHE
    /// </summary>
    /// <param name="texture">The resulting texture</param>
    /// <param name="width">How wide the resulting texture should be</param>
    /// <param name="gradient">The gradient to convert into a Texture2D</param>
    /// <param name="filterMode">How samples are procured</param>
    public void TextureFromGradient(int width, Gradient gradient, FilterMode filterMode = FilterMode.Bilinear)
    {
        // Create a new texture, or clear the previous one
        if (_gradientTexture2D == null)
        {
            _gradientTexture2D = new Texture2D(width, 1);
        }
        else if (_gradientTexture2D.width != width)
        {
            _gradientTexture2D.Reinitialize(width, 1);
        }

        // If no gradient exists, create a new one that runs from black to white at full alpha
        if (gradient == null)
        {
            gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.black, 0), new GradientColorKey(Color.white, 1) },
                new GradientAlphaKey[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) }
            );
        }

        _gradientTexture2D.wrapMode = TextureWrapMode.Clamp;
        _gradientTexture2D.filterMode = filterMode;

        // Sample the gradient at the desired steps and set the texture pixels to match
        Color[] cols = new Color[width];
        for (int i = 0; i < cols.Length; i++)
        {
            float t = i / (cols.Length - 1f);
            cols[i] = gradient.Evaluate(t);
        }
        _gradientTexture2D.SetPixels(cols);
        _gradientTexture2D.Apply();
    }

    #endregion

    #region Heartbeat
    /// <summary>
    /// Starts the beat simulation
    /// Visible in the Inspector
    /// </summary>
    [Button("Start Beat")]
    private void StartBasicBeatSimulation()
    {
        if (_heartCouroutine == null)
        {
            _heartCouroutine = StartCoroutine(Beat());
        }
    }

    /// <summary>
    /// Ends the beat simulation
    /// Visible in the Inspector
    /// </summary>
    [Button("End Beat")]
    private void EndBasicBeatSimulation()
    {
        StopCoroutine(_heartCouroutine);
        _idealDensity = defaultDensity;
        _heartCouroutine = null;

    }

    /// <summary>
    /// Switches the ideal density of the fluid to simulate beating...ish
    /// </summary>
    /// <returns>The time waited between 'beats'</returns>
    private IEnumerator Beat()
    {
        defaultDensity = _idealDensity;
        while (true)
        {
            _idealDensity = defaultDensity + _devianceFromResting;
            yield return new WaitForSeconds(_timeBetweenBeats);

            _idealDensity = defaultDensity - _devianceFromResting;
            yield return new WaitForSeconds(_timeBetweenBeats);
        }

    }

    #endregion

    // RestartSimulation currently crashes Unity
    #region Simulation Button Controls
    /// <summary>
    /// Swaps the active gradient
    /// Visible in the Inspector
    /// </summary>
    [Button("Switch Gradient")]
    private void SwapColors()
    {
        _activeGradient = (_activeGradient == _easierVisualizationGradient ?
            _realisticGradient : _easierVisualizationGradient);
        needsUpdate = true;
    }

    /// <summary>
    /// Completely resets and starts the simulation
    /// Visible in the Inspector
    /// 
    /// TODO: This crashes Unity. It probably shouldn't do that
    /// </summary>
    //[Button("Resimulate")]
    private void RestartSimulation()
    {
        ClearLastData();
        Init();
    }

    /// <summary>
    /// Releases all active buffers
    /// Resets all SpawnInformation
    /// </summary>
    private void ClearLastData()
    {
        ComputeHelper.Release(positionBuffer, predictedPositionBuffer, velocityBuffer, densityBuffer, spatialIndices, spatialOffsets);
        ComputeHelper.Release(_argsBuffer, highO2, lowO2, highO2Distance, lowO2Distance);

        spawnInformation2D.SpawnPositions = null;
        spawnInformation2D.SpawnVelocities = null;
        spawnInformation3D.SpawnPositions = null;
        spawnInformation3D.SpawnVelocities = null;
        if (_savedIs2D != _is2D)
        {
            if (_savedIs2D)
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

    #region Unity Stuff
#if UNITY_EDITOR
    /// <summary>
    /// Handles drawing the bounding box, spawn box, and High/Low o2 regions
    /// Runs ONLY IF this is in the editor, not a build
    /// </summary>
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

    /// <summary>
    /// Runs whenever the Unity inspector is updated
    /// </summary>
    void OnValidate()
    {
        needsUpdate = true;
    }
#endif

    /// <summary>
    /// Deallocates and releases ComputeHelpers to prevent memory leaks
    /// </summary>
    void OnDestroy()
    {
        ComputeHelper.Release(positionBuffer, predictedPositionBuffer, velocityBuffer, densityBuffer, spatialIndices, spatialOffsets);
        ComputeHelper.Release(_argsBuffer, highO2, lowO2, highO2Distance, lowO2Distance);
    }

    #endregion

    #region Math
    /// <summary>
    /// Generates the dot product of two Vector3s
    /// </summary>
    /// <param name="vec1">The first vector, as a Vector3</param>
    /// <param name="vec2">The second vector, as a Vector3</param>
    /// <returns>The dot product, as a Vector3</returns>
    private Vector3 DotProduct(Vector3 vec1, Vector3 vec2)
    {
        return new Vector3(vec1.x * vec2.x, vec1.y * vec2.y, vec1.z * vec2.z);
    }

    /// <summary>
    /// Generates vertex coordinates for calculating if a point is in the desired range
    /// https://math.stackexchange.com/questions/1472049/check-if-a-point-is-inside-a-rectangular-shaped-area-3d
    /// </summary>
    /// <param name="vertex">The vertex to generate coordinates for, as an int</param>
    /// <param name="center">The center of the specified region, as a Vector3</param>
    /// <param name="dimensions">The dimensions of the specified region, as a Vector3</param>
    /// <returns>A list of floats with a length of 3</returns>
    private List<float> GetPointData(int vertex, Vector3 center, Vector3 dimensions)
    {
        List<float> coordinates = new List<float>();
        Vector3 halfDimensions = dimensions * .5f;

        // generate X value
        if (vertex != 3)
        {
            coordinates.Add(center.x - halfDimensions.x);
        }
        else
        {
            coordinates.Add(center.x + halfDimensions.x);
        }

        // generate y value
        if (vertex != 4)
        {
            coordinates.Add(center.y - halfDimensions.y);
        }
        else
        {
            coordinates.Add(center.y + halfDimensions.y);
        }

        // generate z value
        if (vertex != 2)
        {
            coordinates.Add(center.z + halfDimensions.z);
        }
        else
        {
            coordinates.Add(center.z - halfDimensions.z);
        }
        return coordinates;
    }

    /// <summary>
    /// Helper function for converting a List of floats(using at most 3) to a Vector3
    /// </summary>
    /// <param name="floats">List of floats to promote into Vector3</param>
    /// <returns>The first three indeces in the list, as a Vector3</returns>
    private Vector3 ListToVector3(List<float> floats)
    {
        Vector3 result = Vector3.zero;
        if (floats.Count >= 1)
        {
            result.x = floats[0];
        }
        if (floats.Count >= 2)
        {
            result.y = floats[1];
        }
        if (floats.Count >= 3)
        {
            result.z = floats[2];
        }
        return result;
    }

    /// <summary>
    /// Helper function for converting a Vector3 into a List of floats with 3 values
    /// </summary>
    /// <param name="vector">The vector to deconstruct into floats, as a Vector3</param>
    /// <returns>A list of floats with 3 values</returns>
    private List<float> Vector3ToList(Vector3 vector)
    {
        List<float> coordinates = new List<float>();
        coordinates.Add(vector.x);
        coordinates.Add(vector.y);
        coordinates.Add(vector.z);
        return coordinates;

    }
    #endregion
}

#region ParticleSpawnInformation Classes
/// <summary>
/// Parent class for ParticleSpawnInformation classes
/// </summary>
/// <typeparam name="T">The type of data to store, either float2 or float3</typeparam>
public class ParticleSpawnInformation<T>
{
    public T[] SpawnPositions;
    public T[] SpawnVelocities;
}

/// <summary>
/// Child class of ParticleSpawnInformation using float2
/// Stores particle positions and velocities for 2D simulations
/// </summary>
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

/// <summary>
/// Child class of ParticleSpawnInformation using float3
/// Stores particle positions and velocities for 3D simulations
/// </summary>
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
