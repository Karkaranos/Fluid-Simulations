/* Author :             Cade Naylor
 * Last Modified :      June 30, 2025
 * Description :        This file contains testing information for game objects. It holds different zones
 *                          and computes the proper color for each, using some brute force methods
 *
 * TODO:                Refactoring
 *                          - Function headers
 *                          - Organize functions by purpose/general category
 *                          - See if brute force functions can be improved computationally using recursion
 *                      Behavior Updates
 *                          - Maybe get spawned particles to have more physics or fluid behavior?
 *                          - Pick up particles and drag them around
 * */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
using UnityEngine.InputSystem;

public class GameObjectSim : MonoBehaviour
{
    #region Variables
    [Foldout("Particle Controls")]
    [Header("Particle Display")]
    [SerializeField] private int _particleNumber;
    [Foldout("Particle Controls")] [SerializeField] private float _particleSize = 1f;
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
    [Header("Oxygen Controls")]
    [Foldout("2D Simulation Bounds"), ShowIf("_is2D")] [SerializeField] private ZoneInformation2D[] _zoneInformation2D;

    [Header("Bounding Boxes")]
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 _centerOfSpawn3D;
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 _spawnDimensions3D;
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private Vector3 _simulationDimensions3D;
    [Header("Oxygen Controls")]
    [Foldout("3D Simulation Bounds"), HideIf("_is2D")] [SerializeField] private ZoneInformation3D[] _zoneInformation3D;

    private List<GameObject> particles = new List<GameObject>();
    private bool containsBothTypes = false;
    [SerializeField, Required] private GameObject WallPrefab;

    [Foldout("Particle Controls")] [SerializeField] private ColorMode _colorMode;
    [Foldout("Particle Controls")] [ShowIf("_colorMode", ColorMode.DISTANCE)] [SerializeField] private bool _indicateInclusion;
    [Foldout("Particle Controls")] [ShowIf("_indicateInclusion")] [SerializeField] private Color _inHighO2;
    [Foldout("Particle Controls")] [ShowIf("_indicateInclusion")] [SerializeField] private Color _inLowO2;
    [Foldout("Particle Controls")] [ShowIf("_colorMode", ColorMode.TIME)] [SerializeField] private float _timeToDecay;
    [Foldout("Particle Controls")] [ShowIf("_colorMode", ColorMode.TIME)] [SerializeField] private float _lowO2DecayMultiplier;



    public enum ColorMode
    {
        TIME, DISTANCE
    }

    private Gradient _activeGradient;


    private InputActionMap _uMap;
    private InputAction _mousePos;
    private InputAction _mouseDelta;
    private InputAction _space;
    [SerializeField] private GameObject objVisualization;

    private Vector2 _mDelta;
    private Vector2 _mPos;
    private float[,] distanceBetweenZones;

    #endregion

    #region Functions
    private void Start()
    {
        bool typeFound = (_is2D? _zoneInformation2D[0].isHighO2 : _zoneInformation3D[0].isHighO2);
        for(int i=1; i<(_is2D? _zoneInformation2D.Length : _zoneInformation3D.Length); i++)
        {
            if((_is2D ? _zoneInformation2D[i].isHighO2 : _zoneInformation3D[i].isHighO2) != typeFound)
            {
                containsBothTypes = true;
                break;
            }
        }
        if(_is2D)
        {
            foreach(ZoneInformation2D zi in _zoneInformation2D)
            {
                if (zi.shape == Accepted2DZoneShapes.SQUARE)
                {
                    zi.GenerateSquareData();
                }
            }
        }
        else
        {
            foreach (ZoneInformation3D zi in _zoneInformation3D)
            {
                if (zi.shape == Accepted3DZoneShapes.CUBE)
                {
                    zi.GenerateSquareData();
                }
            }
        }

        GenerateDistanceArray();

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

    private void _space_started(InputAction.CallbackContext obj)
    {
        Vector3 halfBound = .5f * _simulationDimensions3D;
        Vector3 randomPos = new Vector3(Random.Range(-halfBound.x, halfBound.x), Random.Range(-halfBound.y, halfBound.y), Random.Range(-halfBound.z, halfBound.z));
        particles.Add(Instantiate((_is2D ? particlePrefab2D : particlePrefab3D), randomPos, Quaternion.identity));
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_is2D)
        {
            foreach (ZoneInformation2D zi in _zoneInformation2D)
            {
                if (zi.isHighO2)
                {
                    Gizmos.color = Color.red;
                }
                else
                {
                    Gizmos.color = Color.blue;
                }

                switch (zi.shape)
                {
                    case Accepted2DZoneShapes.SQUARE:
                        Gizmos.DrawWireCube(zi.ZoneCenter, zi.Dimensions);
                        break;
                    case Accepted2DZoneShapes.CIRCLE:
                        Gizmos.DrawWireSphere(zi.ZoneCenter, zi.Radius);
                        break;
                    default:
                        break;
                }
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, _simulationDimensions2D);
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(_centerOfSpawn2D, _spawnDimensions2D);
        }
        else
        {

            foreach (ZoneInformation3D zi in _zoneInformation3D)
            {
                if (zi.isHighO2)
                {
                    Gizmos.color = Color.red;
                }
                else
                {
                    Gizmos.color = Color.blue;
                }

                switch (zi.shape)
                {
                    case Accepted3DZoneShapes.CUBE:
                        Gizmos.DrawWireCube(zi.ZoneCenter, zi.Dimensions);
                        break;
                    case Accepted3DZoneShapes.SPHERE:
                        Gizmos.DrawWireSphere(zi.ZoneCenter, zi.Radius);
                        break;
                    default:
                        break;
                }
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, _simulationDimensions3D);
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(_centerOfSpawn3D, _spawnDimensions3D);
        }
    }
#endif

    private void Update()
    {
        Vector3 adjustedPos = MoveMouseIndicator();

        float col;
        if (containsBothTypes)
        {
            foreach (GameObject g in particles)
            {
                float[] vals = DistanceFromNearestNeighbor(g.transform);
                bool inShape = false;

                if (_is2D)
                {
                    if (_zoneInformation2D[(int)vals[2]].shape == Accepted2DZoneShapes.CIRCLE)
                    {
                        if (TestCircleInclusion(g.transform.position, _zoneInformation2D[(int)vals[2]].ZoneCenter, _zoneInformation2D[(int)vals[2]].Radius))
                        {
                            inShape = true;
                        }
                    }

                    if (_zoneInformation2D[(int)vals[2]].shape == Accepted2DZoneShapes.SQUARE)
                    {
                        if (TestSquareInclusion(g.transform.position, _zoneInformation2D[(int)vals[2]].vertices,
                            _zoneInformation2D[(int)vals[2]].edges))
                        {
                            inShape = true;
                        }
                    }
                }
                else
                {
                    if (_zoneInformation3D[(int)vals[2]].shape == Accepted3DZoneShapes.SPHERE)
                    {
                        if (TestSphereInclusion(g.transform.position, _zoneInformation3D[(int)vals[2]].ZoneCenter, _zoneInformation3D[(int)vals[2]].Radius))
                        {
                            inShape = true;
                        }
                    }

                    if (_zoneInformation3D[(int)vals[2]].shape == Accepted3DZoneShapes.CUBE)
                    {
                        if (TestCubeInclusion(g.transform.position, _zoneInformation3D[(int)vals[2]].vertices,
                            _zoneInformation3D[(int)vals[2]].edges))
                        {
                            inShape = true;
                        }
                    }
                }

                if (inShape)
                {
                    g.GetComponent<MeshRenderer>().material.color = 
                        (_is2D ? (_zoneInformation2D[(int)vals[2]].isHighO2 ? _inHighO2 : _inLowO2) : 
                        (_zoneInformation3D[(int)vals[2]].isHighO2 ? _inHighO2 : _inLowO2));
                }
                else
                {
                    col = 1.0f - (vals[0] / vals[1]);
                    g.GetComponent<MeshRenderer>().material.color = _activeGradient.Evaluate(col);
                }
            }
        }

    }

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

    private float[] DistanceFromNearestNeighbor(Transform t)
    {
        float[] results = new float[3];
        int closestHigh = 0, closestLow = 0;
        float highDist = int.MaxValue, lowDist = int.MaxValue, dist, lowestTotalDist = int.MaxValue;
        int nearestZone = 0;
        if (_is2D)
        {
            for (int i = 0; i < _zoneInformation2D.Length; i++)
            {
                dist = (_zoneInformation2D[i].ZoneCenter - t.position).magnitude;
                if (_zoneInformation2D[i].isHighO2 && dist < highDist)
                {
                    closestHigh = i;
                    highDist = dist;
                }
                else if (!_zoneInformation2D[i].isHighO2 && dist < lowDist)
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
        }
        else
        {
            for (int i = 0; i < _zoneInformation3D.Length; i++)
            {
                dist = (_zoneInformation3D[i].ZoneCenter - t.position).magnitude;
                if (_zoneInformation3D[i].isHighO2 && dist < highDist)
                {
                    closestHigh = i;
                    highDist = dist;
                }
                else if (!_zoneInformation3D[i].isHighO2 && dist < lowDist)
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
        }

        results[0] = highDist;
        results[1] = distanceBetweenZones[closestHigh, closestLow];
        results[2] = nearestZone;

        return results;
    }

    private void GenerateDistanceArray()
    {
        if (_is2D)
        {
            int n = _zoneInformation2D.Length;
            distanceBetweenZones = new float[n, n];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float val = int.MaxValue;
                    if (i != j && _zoneInformation2D[i].isHighO2 != _zoneInformation2D[j].isHighO2)
                    {
                        val = (_zoneInformation2D[i].ZoneCenter - _zoneInformation2D[j].ZoneCenter).magnitude;
                    }
                    distanceBetweenZones[i, j] = val;
                }
            }
        }
        else
        {
            int n = _zoneInformation3D.Length;
            distanceBetweenZones = new float[n, n];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float val = int.MaxValue;
                    if (i != j && _zoneInformation3D[i].isHighO2 != _zoneInformation3D[j].isHighO2)
                    {
                        val = (_zoneInformation3D[i].ZoneCenter - _zoneInformation3D[j].ZoneCenter).magnitude;
                    }
                    distanceBetweenZones[i, j] = val;
                }
            }
        }
    }

    private bool TestSphereInclusion(Vector3 position, Vector3 center, float radius)
    {
        return (Mathf.Pow(position.x - center.x, 2) + Mathf.Pow(position.y - center.y, 2) + Mathf.Pow(position.z - center.z, 2)) <= Mathf.Pow(radius, 2);
    }

    private bool TestCubeInclusion(Vector3 p0, Vector3[] vertices, Vector3[] edges)
    {
        Vector3 e4 = p0 - vertices[0];

        if(0f < Vector3.Dot(e4, edges[0]) && Vector3.Dot(e4, edges[0])  < Vector3.Dot(edges[0], edges[0]))
        {
            if (0f < Vector3.Dot(e4, edges[1]) && Vector3.Dot(e4, edges[1]) < Vector3.Dot(edges[1], edges[1]))
            {
                if (0f < Vector3.Dot(e4, edges[2]) && Vector3.Dot(e4, edges[2]) < Vector3.Dot(edges[2], edges[2]))
                {
                    return true;
                }
            }
        }

        return false;
    }


    private bool TestSquareInclusion(Vector2 p0, Vector2[] vertices, Vector2[] edges)
    {
        Vector2 e2 = p0 - vertices[0];

        if(0f < Vector2.Dot(e2, edges[0]) && Vector2.Dot(e2, edges[0]) < Vector2.Dot(edges[0], edges[0]))
        {
            if (0f < Vector2.Dot(e2, edges[1]) && Vector2.Dot(e2, edges[1]) < Vector2.Dot(edges[1], edges[1]))
            {
                return true;
            }
        }
        return false;
    }

    private bool TestCircleInclusion(Vector2 position, Vector2 center, float radius)
    {
        return Mathf.Pow(position.x - center.x, 2) + Mathf.Pow(position.y - center.y, 2) <= radius;
    }



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
    /// Swaps the active gradient
    /// Visible in the Inspector
    /// </summary>
    [Button("Switch Gradient")]
    private void SwapColors()
    {
        _activeGradient = (_activeGradient == _easierVisualizationGradient ?
            _realisticGradient : _easierVisualizationGradient);
    }

    /// <summary>
    /// Comparing magnitude? length? 
    /// </summary>
    /// <param name="dir"></param>
    /// <param name="vec1"></param>
    /// <param name="vec2"></param>
    /// <param name="vec3"></param>
    /// <returns></returns>
    public static bool CompareVector3(char dir, Vector3 vec1, Vector3 vec2)
    {
        if(dir == '<')
        {
            if(vec1.magnitude < vec2.magnitude)
            {
                return true;
            }
        }
        else if (dir == '>')
        {
            if (vec1.magnitude > vec2.magnitude)
            {
                return true;
            }
        }

        return false; ;
    }

    #endregion
}

