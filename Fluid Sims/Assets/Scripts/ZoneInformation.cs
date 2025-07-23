/* Author :             Cade Naylor
 * Last Modified :      July 23, 2025
 * Description :        This file contains all classes and enums to create ZoneInformation serializable classes.
 *                      They store:
 *                          - Shape
 *                          - Shape Center
 *                          - Dimensions (be it cube, square, or using radius)
 *                          - Which dominant color(1 or 2) the zone is
 *                   
 * */
using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;

#region Classes

#region Parent Class
/**********************************************************************************************************************
 * Author :             Cade Naylor
 * File Name :          ZoneInformation.cs
 * Last Modified :      July 23, 2025
 * Description :        This file defines the abstract ZoneInformation classes used for all simulations, as well as its
 *                      child classes. It stores the zone's shape and which main color it holds.                 
 *********************************************************************************************************************/
public abstract class ZoneInformation<T>
{
    public Vector3 ZoneCenter;
    public bool isMainColor1;
    [HideInInspector] public Vector3[] vertices;
    [HideInInspector] public Vector3[] edges;
    public T shape;
    [HideIf("isRound"), AllowNesting] public Vector3 Dimensions;
    [ShowIf("isRound"), AllowNesting] public float Radius;

    /// <summary>
    /// I really don't like using AI to help with writing code
    /// However, I will give it credit where it is due. The return statement here was written by ChatGPT. 
    /// I searched StackOverflow, Reddit, NaughtyAttributes, and Unity documentation for a few hours trying to 
    /// find a solution, which is why I asked it to help. And it did.
    /// Anyway enough about that.
    /// 
    /// This function returns whether the shape of an unspecified type is round (ie a circle or a sphere)
    /// </summary>
    /// <returns>Whether the zone's shape is circle-based, as a bool</returns>
    public bool isRound()
    {
        return Equals(shape, Accepted2DZoneShapes.CIRCLE) || Equals(shape, Accepted3DZoneShapes.SPHERE);
    }

    /// <summary>
    /// Implementation handled in child classes
    /// Generates the array of points and edges for non-round shapes
    /// </summary>
    public abstract void GenerateNDimensionCubeData();
}
#endregion

#region Child Classes
/**********************************************************************************************************************
 * Author :             Cade Naylor
 * File Name :          ZoneInformation3D.cs
 * Last Modified :      July 23, 2025
 * Description :        This file inherits from the abstract ZoneInformation class, using a type of Accepted3DZoneShapes
 *                      It handles generating N Dimension Cube Data for 3D Cubes
 *********************************************************************************************************************/
[System.Serializable]
public class ZoneInformation3D : ZoneInformation<Accepted3DZoneShapes>
{
    /// <summary>
    /// Initializes the vertices and edges Vector3 arrays for cubes
    /// Implements the Abstract function from ZoneInformation
    /// </summary>
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


/**********************************************************************************************************************
 * Author :             Cade Naylor
 * File Name :          ZoneInformation2D.cs
 * Last Modified :      July 23, 2025
 * Description :        This file inherits from the abstract ZoneInformation class, using a type of Accepted2DZoneShapes
 *                      It handles generating N Dimension Cube Data for 2D Squares
 *********************************************************************************************************************/
[System.Serializable]
public class ZoneInformation2D : ZoneInformation<Accepted2DZoneShapes>
{
    /// <summary>
    /// Initializes the vertices and edges Vector3 arrays for squares
    /// Implements the Abstract function from ZoneInformation
    /// </summary>
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
#endregion 

#endregion

#region Enums

/// <summary>
/// Contains valid shapes for 3D Zones
/// </summary>
public enum Accepted3DZoneShapes
{
    CUBE, SPHERE
}

/// <summary>
/// Contains valid shapes for 2D Zones
/// </summary>
public enum Accepted2DZoneShapes
{
    SQUARE, CIRCLE
}
#endregion