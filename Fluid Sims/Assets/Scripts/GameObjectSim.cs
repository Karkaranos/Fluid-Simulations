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
    public ZoneInformation[] OxygenZones;
    public List<GameObject> particles = new List<GameObject>();
    [SerializeField, Required] private GameObject particlePrefab;
    private bool containsBothTypes = false;
    [SerializeField, Required] private GameObject WallPrefab;

    [SerializeField] private Vector3 _simulationDimensions3D;
    [SerializeField] private Color InHighO2;
    [SerializeField] private Color InLoWO2;

    private Gradient _activeGradient;
    [Foldout("Particle Controls")] [SerializeField] private Gradient _easierVisualizationGradient;
    [Foldout("Particle Controls")] [SerializeField] private Gradient _realisticGradient;

    // Variables used to test gameObject proximity
    #region Input Variables
    private InputActionMap _uMap;
    private InputAction _mousePos;
    private InputAction _mouseDelta;
    private InputAction _space;
    [SerializeField] private GameObject objVisualization;

    private Vector2 _mDelta;
    private Vector2 _mPos;
    private float[,] distanceBetweenZones;
    #endregion
    #endregion

    #region Functions
    private void Start()
    {
        bool typeFound = OxygenZones[0].isHighO2;
        for(int i=1; i<OxygenZones.Length; i++)
        {
            if(OxygenZones[i].isHighO2!=typeFound)
            {
                containsBothTypes = true;
                break;
            }
        }

        foreach(ZoneInformation zi in OxygenZones)
        {
            if(zi.shape == AcceptedZoneShapes.CUBE)
            {
                zi.GenerateSquareData();
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
        particles.Add(Instantiate(particlePrefab, randomPos, Quaternion.identity));
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        foreach(ZoneInformation zi in OxygenZones)
        {
            if(zi.isHighO2)
            {
                Gizmos.color = Color.red;
            }
            else
            {
                Gizmos.color = Color.blue;
            }

            switch (zi.shape)
            {
                case AcceptedZoneShapes.CUBE:
                    Gizmos.DrawWireCube(zi.ZoneCenter, zi.Dimensions);
                    break;
                case AcceptedZoneShapes.SPHERE:
                    Gizmos.DrawWireSphere(zi.ZoneCenter, zi.Radius);
                    break;
                default:
                    break;
            }
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, _simulationDimensions3D);
    }
#endif

    private void Update()
    {
        Vector3 adjustedPos = MoveMouseIndicator();

        float dist, col;
        foreach(GameObject g in particles)
        {
            float[] vals = DistanceFromNearestNeighbor(g.transform);
            bool inShape = false;

            if(OxygenZones[(int)vals[2]].shape == AcceptedZoneShapes.SPHERE)
            {
                if(TestSphereInclusion(g.transform.position, OxygenZones[(int)vals[2]].ZoneCenter, OxygenZones[(int)vals[2]].Radius))
                {
                    inShape = true;
                }
            }

            if(OxygenZones[(int)vals[2]].shape == AcceptedZoneShapes.CUBE)
            {
                if(TestCubeInclusion(g.transform.position, OxygenZones[(int)vals[2]].vertices, 
                    OxygenZones[(int)vals[2]].edges))
                {
                    inShape = true;
                }
            }

            if(inShape)
            {
                g.GetComponent<MeshRenderer>().material.color = OxygenZones[(int)vals[2]].isHighO2 ? InHighO2 : InLoWO2;
            }
            else
            {
                col = 1.0f - (vals[0] / vals[1]);
                g.GetComponent<MeshRenderer>().material.color = _activeGradient.Evaluate(col);
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
        for(int i=0; i<OxygenZones.Length; i++)
        {
            dist = (OxygenZones[i].ZoneCenter - t.position).magnitude;
            if(OxygenZones[i].isHighO2 && dist < highDist)
            {
                closestHigh = i;
                highDist = dist;
            }
            else if (!OxygenZones[i].isHighO2 && dist < lowDist)
            {
                closestLow = i;
                lowDist = dist;
            }
            if(dist < lowestTotalDist)
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

    private void GenerateDistanceArray()
    {
        int n = OxygenZones.Length;
        distanceBetweenZones = new float[n, n];

        for (int i=0; i<n; i++)
        {
            for(int j=0; j<n; j++)
            {
                float val = int.MaxValue;
                if(i!=j && OxygenZones[i].isHighO2 != OxygenZones[j].isHighO2)
                {
                    Debug.LogWarning("Needs to factor box size/radius in");
                    val = (OxygenZones[i].ZoneCenter - OxygenZones[j].ZoneCenter).magnitude;
                }
                distanceBetweenZones[i,j] = val;
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

#region Helper Classes, Structs, and Enums
[System.Serializable]
public class ZoneInformation
{
    public Vector3 ZoneCenter;
    public bool isHighO2;
    public AcceptedZoneShapes shape;
    [ShowIf("shape", AcceptedZoneShapes.CUBE), AllowNesting] public Vector3 Dimensions;
    [ShowIf("shape", AcceptedZoneShapes.SPHERE), AllowNesting] public float Radius;
    [HideInInspector] public Vector3[] vertices;
    [HideInInspector] public Vector3[] edges;

    public void GenerateSquareData()
    {
        vertices = new Vector3[4];
        edges = new Vector3[3];

        Vector3 halfDimensions = Dimensions * .5f;

        // Generate vertices
        for (int i = 0; i < 4; i++)
        {
            Vector3 vPos = new Vector3(ZoneCenter.x - halfDimensions.x, ZoneCenter.y - halfDimensions.y, ZoneCenter.z + halfDimensions.z);
            switch (i)
            {
                case 1:
                    vPos.z = ZoneCenter.z - halfDimensions.z;
                    break;
                case 2:
                    vPos.x = ZoneCenter.x + halfDimensions.x;
                    break;
                case 3:
                    vPos.y = ZoneCenter.y + halfDimensions.y;
                    break;
                default:
                    break;
            }
            vertices[i] = vPos;
        }

        // Generate edges
        edges[0] = vertices[1] - vertices[0];
        edges[1] = vertices[2] - vertices[0];
        edges[2] = vertices[3] - vertices[0];

    }


}

public enum AcceptedZoneShapes
{
    CUBE, SPHERE
}
#endregion

