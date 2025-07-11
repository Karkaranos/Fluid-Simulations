using NaughtyAttributes;
using UnityEngine;

/*
public class ZoneInformation<T>
{
    public bool isHighO2;
    [HideInInspector] public Vector3[] vertices;
    [HideInInspector] public Vector3[] edges;
    public T shape;
    [ShowIf(nameof("SquareBased")), AllowNesting] public Vector3 Dimensions;
    [ShowIf("shape", Accepted3DZoneShapes.SPHERE), AllowNesting] public float Radius;
    bool SquareBased => shape == Accepted2DZoneShapes,S
}*/

[System.Serializable]
public class ZoneInformation3D/*<T> : ZoneInformation<T>*/
{
    public Vector3 ZoneCenter;
    public bool isHighO2;
    [HideInInspector] public Vector3[] vertices;
    [HideInInspector] public Vector3[] edges;
    public Accepted3DZoneShapes shape;
    [ShowIf("shape", Accepted3DZoneShapes.CUBE), AllowNesting] public Vector3 Dimensions;
    [ShowIf("shape", Accepted3DZoneShapes.SPHERE), AllowNesting] public float Radius;


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

[System.Serializable]
public class ZoneInformation2D /*: ZoneInformation*/
{
    public Vector3 ZoneCenter;
    public bool isHighO2;
    [HideInInspector] public Vector2[] vertices;
    [HideInInspector] public Vector2[] edges;
    public Accepted2DZoneShapes shape;
    [ShowIf("shape", Accepted2DZoneShapes.SQUARE), AllowNesting] public Vector2 Dimensions;
    [ShowIf("shape", Accepted2DZoneShapes.CIRCLE), AllowNesting] public float Radius;

    public void GenerateSquareData()
    {
        vertices = new Vector2[3];
        edges = new Vector2[2];

        Vector2 halfDimensions = Dimensions * .5f;

        // Generate vertices
        for (int i = 0; i < 3; i++)
        {
            Vector2 vPos = new Vector2(ZoneCenter.x - halfDimensions.x, ZoneCenter.y + halfDimensions.y);
            switch (i)
            {
                case 1:
                    vPos.x = ZoneCenter.x + halfDimensions.x;
                    break;
                case 2:
                    vPos.y = ZoneCenter.y - halfDimensions.y;
                    break;
                default:
                    break;
            }
            vertices[i] = vPos;
        }

        // Generate edges
        edges[0] = vertices[1] - vertices[0];
        edges[1] = vertices[2] - vertices[0];
    }


}

public enum Accepted3DZoneShapes
{
    CUBE, SPHERE
}

public enum Accepted2DZoneShapes
{
    SQUARE, CIRCLE
}