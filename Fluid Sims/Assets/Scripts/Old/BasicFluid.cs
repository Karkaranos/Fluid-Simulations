using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class BasicFluid : MonoBehaviour
{
    public float gravity = 2f;
    [Range(.2f, 5f)]
    public float particleSize = 1;
    Particle[] particles;
    public Vector2 boundingBox = new Vector2(10,7.5f);
    private float collisionDampening = .9f;
    public int numParticles = 1;
    public float partSpacing = .1f;
    private int savedParticleNum = 1;
    [SerializeField] private float smoothingRadius = .5f;


    // Start is called before the first frame update
    void Start()
    {
        particles = new Particle[0];
        savedParticleNum = numParticles;

        GenerateNewArray();

    }




    // Update is called once per frame
    void Update()
    {
        if(numParticles!=savedParticleNum)
        {
            GenerateNewArray();
        }
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].vel += Vector2.down * gravity * Time.deltaTime;
            particles[i].pos += particles[i].vel * Time.deltaTime;

            ResolveCollisions(ref particles[i].pos, ref particles[i].vel);

            DrawCircle(particles[i], particleSize);
        }
    }


    void ResolveCollisions(ref Vector2 pos, ref Vector2 vel)
    {
        Vector2 halfBounds = boundingBox / 2 - Vector2.one * particleSize;
        if(Mathf.Abs(pos.x)>halfBounds.x)
        {
            pos.x = halfBounds.x * (pos.x > 0 ? 1 : -1);
            vel.x *= -1 * collisionDampening;
        }
        if(Mathf.Abs(pos.y) > halfBounds.y)
        {
            pos.y = halfBounds.y * (pos.y > 0 ? 1 : -1);
            vel.y *= -1*collisionDampening;
        }


    }

    float SmoothingKernel(float radius, float dst)
    {
        float vol = Mathf.PI * Mathf.Pow(radius, 8) / 4;
        float val = Mathf.Max(0, radius * radius - dst * dst);
        return val * val * val / vol;
    }

    float CalculateDensity(Vector2 point)
    {
        float density = 0;
        const float mass = 1;

        foreach(Particle p in particles)
        {
            float dst = (p.pos - point).magnitude;
            float influence = SmoothingKernel(smoothingRadius, dst);
            density += mass * influence;
        }

        return density;
    }


    #region Helpers

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(Vector3.zero, boundingBox);
    }

    private void DrawCircle(Particle p, float radius)
    {
        int segments = 363;
        float width = 2* radius;
        LineRenderer line = p.lr;

        line.useWorldSpace = false;
        line.startWidth = width;
        line.endWidth = width;

        line.positionCount = segments;

        Vector3[] points = new Vector3[segments];

        for(int i=0; i<segments; i++)
        {
            float radian = Mathf.Deg2Rad*(i * 360f / 360f);
            points[i] = new Vector3(Mathf.Sin(radian) * radius + p.pos.x, Mathf.Cos(radian) * radius + p.pos.y,0 );
        }

        line.SetPositions(points);
    }

    void GenerateNewArray()
    {

        for(int i=0; i<particles.Length; i++)
        {
            particles[i].delete();
        }

        particles = new Particle[numParticles];

        int partPerRow = (int)(Mathf.Sqrt(numParticles));
        int particlesPerCol = (numParticles - 1) / partPerRow + 1;
        float spacing = particleSize * 2 + partSpacing;

        for (int i = 0; i < numParticles; i++)
        {
            particles[i] = new Particle(i);
            particles[i].pos = new Vector2((i % partPerRow - partPerRow / 2f + .5f) * spacing, (i / partPerRow - particlesPerCol / 2f + .5f) * spacing);
        }


        savedParticleNum = numParticles;
    }

    

    #endregion
}

public class Particle : Behaviour
{
    public Color c;
    public float r;
    public Vector2 pos;
    public Vector2 vel;

    private GameObject g;
    public LineRenderer lr;

    public Particle(int i)
    {
        g = new GameObject();
        g.name = "Particle " + i;
        lr = g.AddComponent<LineRenderer>();
        c = new Color(Random.Range(0, 1f), Random.Range(0, 1f), Random.Range(0, 1f));
        lr.material.SetColor("_Color", c);
    }

    public void delete()
    {
        pos = Vector2.zero;
        vel = Vector2.zero;

        Destroy(lr);
        Destroy(g);
    }

    

}
