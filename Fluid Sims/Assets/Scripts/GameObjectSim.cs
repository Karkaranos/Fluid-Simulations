/* Author :             Cade Naylor
 * Last Modified :      July 23, 2025
 * Description :        This file contains testing information for game objects. It holds different zones
 *                          and computes the proper color for each, using some brute force methods
 *
 * TODO:                Refactoring
 *                          - See if brute force functions can be improved computationally using recursion
 *                          - Fix Instantiate Walls
 *                      Behavior Updates
 *                          - Maybe get spawned particles to have more physics or fluid behavior?
 *                          - Pick up particles and drag them around
 *                          - Time-based color changing
 *                   
 * */
using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameObjectSim : MonoBehaviour
{
    #region Variables
    [Foldout("Particle Controls")]
    [Header("Particle Display")]
    [SerializeField] private int _particleNumber;
    [Foldout("Particle Controls")] [SerializeField] private float _particleSize = 1f;

    //May be replaced later
    [Foldout("Particle Controls")] [SerializeField] private Gradient _easierVisualizationGradient;
    [Foldout("Particle Controls")] [SerializeField] private Gradient _realisticGradient;
    [Tooltip("True for 2D simulations. False for 3D simulations")] [SerializeField] private bool _is2D;
    private bool _savedIs2D;

    // 2D Visuals
    [HideIf("_is2D"), Foldout("3D Particle Controls")] [SerializeField] private GameObject particlePrefab3D;
    [Foldout("2D Particle Controls"), ShowIf("_is2D")] [SerializeField] private Vector2 _initialVelocity2D;
    private Texture2D _gradientTexture2D;

    // 3D visuals
    [HideIf("_is2D"), Foldout("3D Particle Controls")] [SerializeField] private GameObject particlePrefab2D;
    [HideIf("_is2D"), Foldout("3D Particle Controls")] [SerializeField] Color _color;
    [Foldout("3D Particle Controls"), HideIf("_is2D")] [SerializeField] private Vector3 _initialVelocity3D;
    private Material _material;

    [Header("Bounding Boxes")]
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 _centerOfSpawn2D;
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 _spawnDimensions2D;
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private Vector2 _simulationDimensions2D;
    [Header("Zone Controls")]
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private ZoneInformation2D[] _zoneInformation2D;

    [Header("Bounding Boxes")]
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 _centerOfSpawn3D;
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 _spawnDimensions3D;
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 _simulationDimensions3D;
    [Header("Zone Controls")]
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private ZoneInformation3D[] _zoneInformation3D;

    [SerializeField, Required] private GameObject WallPrefab;

    [Foldout("Particle Controls")] [SerializeField] private ColorMode _colorMode;
    [Foldout("Particle Controls")] [ShowIf("_colorMode", ColorMode.DISTANCE)] [SerializeField] private bool _indicateInclusion;
    [Foldout("Particle Controls")] [ShowIf("_indicateInclusion")] [SerializeField] private Color _inDominantColor1;
    [Foldout("Particle Controls")] [ShowIf("_indicateInclusion")] [SerializeField] private Color _inDominantColor2;
    [Foldout("Particle Controls")] [ShowIf("_colorMode", ColorMode.TIME)] [SerializeField] private float _timeToDecay;
    [Foldout("Particle Controls")] [ShowIf("_colorMode", ColorMode.TIME)] [SerializeField] private float _inDominantColor2Multiplier;

    [SerializeField] private GameObject objVisualization;

    private Gradient _activeGradient;

    private InputActionMap _uMap;
    private InputAction _mousePos;
    private InputAction _mouseDelta;
    private InputAction _space;

    private List<GameObject> particles = new List<GameObject>();
    private bool containsBothTypes = false;
    private bool firstTypeContained;

    private Vector2 _mDelta;
    private Vector2 _mPos;
    private float[,] distanceBetweenZones;

    public enum ColorMode
    {
        TIME, DISTANCE
    }

    #endregion

    #region Functions

    // All functions have headers
    #region Initialization
    /// <summary>
    /// Called at the first frame update
    /// Sets whether the current simulation is 2D/3D
    /// Instantiates walls
    /// Sets active gradient
    /// Initializes Input 
    /// </summary>
    private void Start()
    {
        if(_is2D)
        {
            Initialize(_zoneInformation2D);
        }
        else
        {
            Initialize(_zoneInformation3D);
        }

        _uMap = GetComponent<PlayerInput>().currentActionMap;
        _uMap.Enable();
        _mousePos = _uMap.FindAction("MousePos");
        _mouseDelta = _uMap.FindAction("MouseDelta");
        _space = _uMap.FindAction("Space");

        _space.started += _space_started;

        _activeGradient = _easierVisualizationGradient;

        particles.Add(objVisualization);

        InstantiateWalls();

    }

    /// <summary>
    /// Initializes the zone information of the appropriate dimension for cube-based shapes
    /// Generates a Distance array that shows the distance between each zone and every other zone
    /// </summary>
    /// <typeparam name="T">Accepted2DZoneShapes if 2D, Accepted3DZoneShapes if 3D</typeparam>
    /// <param name="zoneInformation">An array of Zones of the matching type</param>
    private void Initialize<T>(ZoneInformation<T>[] zoneInformation)
    {
        containsBothTypes = CheckForBothTypes(zoneInformation);

        foreach (ZoneInformation<T> zi in zoneInformation)
        {
            if(!zi.isRound())
            {
                zi.GenerateNDimensionCubeData();
            }
        }

        GenerateDistanceArray(zoneInformation);
    }

    /// <summary>
    /// Checks if the specified zones contain both types of zones
    /// </summary>
    /// <typeparam name="T">Accepted2DZoneShapes if 2D, Accepted3DZoneShapes if 3D</typeparam>
    /// <param name="zoneInfo">An array of Zones of the matching type</param>
    /// <returns>Returns true if both types are present, false if only one is</returns>
    private bool CheckForBothTypes<T>(ZoneInformation<T>[] zoneInfo)
    {
        bool typeFound = zoneInfo[0].isMainColor1;
        firstTypeContained = typeFound;

        foreach(ZoneInformation<T> zi in zoneInfo)
        {
            if(zi.isMainColor1 != typeFound)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// There is such a better way to do this
    /// Instantiates walls to keep particles contained within the simulation bounds, but uh only works with 3D for now
    /// </summary>
    private void InstantiateWalls()
    {

        Vector3 spawnPos = Vector3.zero;
        Vector3 angle;

        spawnPos.y -= .5f * _simulationDimensions3D.y;
        var v = Instantiate(WallPrefab, spawnPos, Quaternion.identity);
        v.GetComponent<MeshRenderer>().material.color = new Color(1f, 1f, 1f, .5f);
        v.layer = 2;

        spawnPos.y += _simulationDimensions3D.y;
        v = Instantiate(WallPrefab, spawnPos, Quaternion.identity);
        v.layer = 2;

        spawnPos.y = 0;
        angle = new Vector3(0, 0, 90);
        spawnPos.x -= .5f * _simulationDimensions3D.x;
        v = Instantiate(WallPrefab, spawnPos, Quaternion.Euler(angle));
        v.layer = 2;

        spawnPos.x += _simulationDimensions3D.x;
        v = Instantiate(WallPrefab, spawnPos, Quaternion.Euler(angle));
        v.layer = 2;

        spawnPos.x = 0;
        angle = new Vector3(90, 0, 0);
        spawnPos.z -= .5f * _simulationDimensions3D.z;
        v = Instantiate(WallPrefab, spawnPos, Quaternion.Euler(angle));
        v.layer = 2;

        spawnPos.z += _simulationDimensions3D.z;
        v = Instantiate(WallPrefab, spawnPos, Quaternion.Euler(angle));
        v.layer = 2;
    }
    #endregion

    // Will be made obsolete when particles spawn on their own
    #region Input
    /// <summary>
    /// Spawns a particle anywhere in the dimensions when space is pressed
    /// </summary>
    /// <param name="obj">Callback context</param>
    private void _space_started(InputAction.CallbackContext obj)
    {
        Vector3 halfBound = .5f * _simulationDimensions3D;
        Vector3 randomPos = new Vector3(Random.Range(-halfBound.x, halfBound.x), Random.Range(-halfBound.y, halfBound.y), Random.Range(-halfBound.z, halfBound.z));
        particles.Add(Instantiate((_is2D ? particlePrefab2D : particlePrefab3D), randomPos, Quaternion.identity));
    }

    /// <summary>
    /// Moves the mouse indicator to follow the mouse
    /// Locks it within the simulation bounds
    /// </summary>
    /// <returns></returns>
    private Vector3 MoveMouseIndicator()
    {
        // move GameObject and adjust its color based on proximity to regions
        _mPos = Camera.main.ScreenToWorldPoint(_mousePos.ReadValue<Vector2>());
        _mDelta = _mouseDelta.ReadValue<Vector2>();
        Vector3 saveVel = _mDelta * 2;
        saveVel.z = 0;

        objVisualization.GetComponent<Rigidbody>().velocity = saveVel;
        bool neededUpdate = false;
        Vector3 adjustedPos = objVisualization.transform.position;
        if (Mathf.Abs(adjustedPos.x) > _simulationDimensions3D.x * .5f)
        {
            adjustedPos.x = (adjustedPos.x > _simulationDimensions3D.x * .5f ? 1 : -1) * _simulationDimensions3D.x * .5f;
            neededUpdate = true;
        }
        if (Mathf.Abs(adjustedPos.y) > _simulationDimensions3D.y * .5f)
        {
            adjustedPos.y = (adjustedPos.y > _simulationDimensions3D.y * .5f ? 1 : -1) * _simulationDimensions3D.y * .5f;
            neededUpdate = true;
        }
        if (Mathf.Abs(adjustedPos.z) > _simulationDimensions3D.z * .5f)
        {
            adjustedPos.z = (adjustedPos.z > _simulationDimensions3D.z * .5f ? 1 : -1) * _simulationDimensions3D.z * .5f;
            neededUpdate = true;
        }
        if (neededUpdate)
        {
            objVisualization.transform.position = adjustedPos;
        }

        return adjustedPos;
    }
    #endregion

    // FLESH OUT
    #region Frame Updates
    /// <summary>
    /// Runs the simulation
    /// Right now it just sets the mouse position and adjusts particle color
    /// </summary>
    private void Update()
    {
        MoveMouseIndicator();

        if (containsBothTypes)
        {
            foreach (GameObject g in particles)
            {
                SetColor(g);
            }
        }
        else
        {
            foreach(GameObject g in particles)
            {
                g.GetComponent<MeshRenderer>().material.color = (firstTypeContained ? _inDominantColor1 : _inDominantColor2);
            }
        }

    }
    #endregion 

    #region Helper Functions

    /// <summary>
    /// Sets the color of a given particle given:
    ///     - Whether the particle is contained within a zone
    ///     - How far along the particle is between the two closest zones of different colors
    /// </summary>
    /// <param name="g">The gameobject to set the color of</param>
    private void SetColor(GameObject g)
    {
        float[] vals;
        bool inShape = false;
        float col;

        if (_is2D)
        {
            vals = DistanceFromNearestNeighbor(_zoneInformation2D, g.transform);
            inShape = TestInclusion(_zoneInformation2D[(int)vals[2]], g);
        }
        else
        {
            vals = DistanceFromNearestNeighbor(_zoneInformation3D, g.transform);
            inShape = TestInclusion(_zoneInformation3D[(int)vals[2]], g);
        }

        if (inShape)
        {
            g.GetComponent<MeshRenderer>().material.color =
                (_is2D ? (_zoneInformation2D[(int)vals[2]].isMainColor1 ? _inDominantColor1 : _inDominantColor2) :
                (_zoneInformation3D[(int)vals[2]].isMainColor1 ? _inDominantColor1 : _inDominantColor2));
        }
        else
        {
            col = 1.0f - (vals[0] / vals[1]);
            g.GetComponent<MeshRenderer>().material.color = _activeGradient.Evaluate(col);
        }
    }

    /// <summary>
    /// Helper function for determing if a particle is included in a zone or not
    /// </summary>
    /// <typeparam name="T">Accepted2DZoneShapes if 2D, Accepted3DZoneShapes if 3D</typeparam>
    /// <param name="zi">The zone being tested against</param>
    /// <param name="g">the given particle</param>
    /// <returns></returns>
    private bool TestInclusion<T>(ZoneInformation<T> zi, GameObject g)
    {
        if (zi.isRound())
        {
            return TestSphereInclusion(g.transform.position, zi.ZoneCenter, zi.Radius);
        }
        return TestCubeInclusion(g.transform.position, zi.vertices, zi.edges);
    }
    #endregion

    #region Unity Stuff
#if UNITY_EDITOR
    /// <summary>
    /// Draws all bounding boxes and gizmos if the project is being run in Unity
    /// </summary>
    private void OnDrawGizmos()
    {
        if (_is2D)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, _simulationDimensions2D);
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(_centerOfSpawn2D, _spawnDimensions2D);

            DrawOxygenZoneGizmos(_zoneInformation2D);
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, _simulationDimensions3D);
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(_centerOfSpawn3D, _spawnDimensions3D);

            DrawOxygenZoneGizmos(_zoneInformation3D);
        }
    }

    /// <summary>
    /// Genericable function that draws Gizmos for all zones
    /// Displays the zone color of that zone, as well as the stated shape
    /// </summary>
    /// <typeparam name="T">Accepted2DZoneShapes if 2D, Accepted3DZoneShapes if 3D</typeparam>
    /// <param name="zoneInformation">An array of Zones of the matching type</param>
    private void DrawOxygenZoneGizmos<T>(ZoneInformation<T>[] zoneInformation)
    {
        foreach (ZoneInformation<T> zi in zoneInformation)
        {
            if (zi.isMainColor1)
            {
                Gizmos.color = _inDominantColor1;
            }
            else
            {
                Gizmos.color = _inDominantColor2;
            }

            if (zi.isRound())
            {
                Gizmos.DrawWireSphere(zi.ZoneCenter, zi.Radius);
            }
            else
            {
                Gizmos.DrawWireCube(zi.ZoneCenter, zi.Dimensions);
            }
        }
    }
#endif
    #endregion

    #region Math
    /// <summary>
    /// Calculates the distance from each zone to the zone's nearest neighbor
    /// </summary>
    /// <typeparam name="T">Accepted2DZoneShapes if 2D, Accepted3DZoneShapes if 3D</typeparam>
    /// <param name="zoneInfo">An array of Zones of the matching type</param>
    /// <param name="t">The transform of the current zone</param>
    /// <returns>Returns an array of floats, size 3, with the following information:
    /// float[0] : the distance to the closest Dominant Color 1 zone
    /// float[1] : the total distance between the closest two zones of differing types 
    /// float[2] : the index of the overall closest zone 
    /// </returns>
    private float[] DistanceFromNearestNeighbor<T>(ZoneInformation<T>[] zoneInfo, Transform t)
    {
        float[] results = new float[3];
        int closestHigh = 0, closestLow = 0;
        float highDist = int.MaxValue, lowDist = int.MaxValue, dist, lowestTotalDist = int.MaxValue;
        int nearestZone = 0;
        for (int i = 0; i < zoneInfo.Length; i++)
        {
            dist = (zoneInfo[i].ZoneCenter - t.position).magnitude;
            if (zoneInfo[i].isMainColor1 && dist < highDist)
            {
                closestHigh = i;
                highDist = dist;
            }
            else if (!zoneInfo[i].isMainColor1 && dist < lowDist)
            {
                closestLow = i;
                lowDist = dist;
            }
            if (dist < lowestTotalDist)
            {
                lowestTotalDist = dist;
                nearestZone = i;
            }
        }

        results[0] = highDist;
        results[1] = distanceBetweenZones[closestHigh, closestLow];
        results[2] = nearestZone;

        return results;
    }

    /// <summary>
    /// Generates an array that contains the distance between each zone and every other zone
    /// </summary>
    /// <typeparam name="T">Accepted2DZoneShapes if 2D, Accepted3DZoneShapes if 3D</typeparam>
    /// <param name="zoneInformation">An array of Zones of the matching type</param>
    private void GenerateDistanceArray<T>(ZoneInformation<T>[] zoneInformation)
    {
        int n = zoneInformation.Length;
        distanceBetweenZones = new float[n, n];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                float val = int.MaxValue;
                if (i != j && zoneInformation[i].isMainColor1 != zoneInformation[j].isMainColor1)
                {
                    val = (zoneInformation[i].ZoneCenter - zoneInformation[j].ZoneCenter).magnitude;
                }
                distanceBetweenZones[i, j] = val;
            }
        }
    }

    /// <summary>
    /// Tests whether or not a given point is inside of the given round zone
    /// </summary>
    /// <param name="position">The position being tested</param>
    /// <param name="center">The center of the round area</param>
    /// <param name="radius">The radius of the round area</param>
    /// <returns>Whether the point is inside of the zone</returns>
    private bool TestSphereInclusion(Vector3 position, Vector3 center, float radius)
    {
        return (Mathf.Pow(position.x - center.x, 2) + Mathf.Pow(position.y - center.y, 2) + Mathf.Pow(position.z - center.z, 2)) <= Mathf.Pow(radius, 2);
    }

    /// <summary>
    /// Tests whether or not a given point is inside of the given N dimension cube
    /// </summary>
    /// <param name="p0">The position being tested</param>
    /// <param name="vertices">The vertices defining the N dimension cube</param>
    /// <param name="edges">The edges defining the N dimension cube</param>
    /// <returns>Whether the point is inside of the zone</returns>
    private bool TestCubeInclusion(Vector3 p0, Vector3[] vertices, Vector3[] edges)
    {
        Vector3 e4 = p0 - vertices[0];

        if(0f < Vector3.Dot(e4, edges[0]) && Vector3.Dot(e4, edges[0])  < Vector3.Dot(edges[0], edges[0]))
        {
            if (0f < Vector3.Dot(e4, edges[1]) && Vector3.Dot(e4, edges[1]) < Vector3.Dot(edges[1], edges[1]))
            {
                // If the shape is 3D, check the final dimension
                if(edges.Length > 2)
                {
                    if (0f < Vector3.Dot(e4, edges[2]) && Vector3.Dot(e4, edges[2]) < Vector3.Dot(edges[2], edges[2]))
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }
        }
        return false;
    }

    #endregion

    #region Buttons
    /// <summary>
    /// Swaps the active gradient
    /// Visible in the Inspector
    /// </summary>
    [Button("Switch Gradient")]
    private void SwapColors()
    {
        _activeGradient = (_activeGradient == _easierVisualizationGradient ?
            _realisticGradient : _easierVisualizationGradient);
    }
    #endregion

    #endregion
}

