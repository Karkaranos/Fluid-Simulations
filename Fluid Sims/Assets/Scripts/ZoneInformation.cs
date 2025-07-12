using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;


public abstract class ZoneInformation<T>
{
    public Vector3 ZoneCenter;
    public bool isHighO2;
    [HideInInspector] public Vector3[] vertices;
    [HideInInspector] public Vector3[] edges;
    public T shape;
    [HideIf("isRound"), AllowNesting] public Vector3 Dimensions;
    [ShowIf("isRound"), AllowNesting] public float Radius;

    public bool isRound()
    {
        return Equals(shape, Accepted2DZoneShapes.CIRCLE) || Equals(shape, Accepted3DZoneShapes.SPHERE);
    }

    public abstract void GenerateNDimensionCubeData();
}

[System.Serializable]
public class ZoneInformation3D : ZoneInformation<Accepted3DZoneShapes>
{
    public override void GenerateNDimensionCubeData()
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
public class ZoneInformation2D : ZoneInformation<Accepted2DZoneShapes>
{
    public override void GenerateNDimensionCubeData()
    {
        vertices = new Vector3[3];
        edges = new Vector3[2];

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