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
            col = 1.0f - (vals[0] / vals[1]);
            g.GetComponent<MeshRenderer>().material.color = _activeGradient.Evaluate(col);
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
        float[] results = new float[2];
        int closestHigh = 0, closestLow = 0;
        float highDist = int.MaxValue, lowDist = int.MaxValue, dist;
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
        }

        results[0] = highDist;
        results[1] = distanceBetweenZones[closestHigh, closestLow];

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
    /// Swaps the active gradient
    /// Visible in the Inspector
    /// </summary>
    [Button("Switch Gradient")]
    private void SwapColors()
    {
        _activeGradient = (_activeGradient == _easierVisualizationGradient ?
            _realisticGradient : _easierVisualizationGradient);
    }
}

[System.Serializable]
public class ZoneInformation
{
    public Vector3 ZoneCenter;
    public bool isHighO2;
    public AcceptedZoneShapes shape;
    [ShowIf("shape", AcceptedZoneShapes.CUBE), AllowNesting] public Vector3 Dimensions;
    [ShowIf("shape", AcceptedZoneShapes.SPHERE), AllowNesting] public float Radius;
}

public enum AcceptedZoneShapes
{
    CUBE, SPHERE
}