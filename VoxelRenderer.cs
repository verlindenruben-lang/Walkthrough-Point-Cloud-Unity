using UnityEngine;
using System.Collections.Generic; // voor List<>
using System.IO;  
using System.Globalization;

[RequireComponent(typeof(ParticleSystem))]
public class VoxelRenderer : MonoBehaviour
{
    private ParticleSystem system;
    private ParticleSystem.Particle[] voxels;
    private bool voxelsUpdated = false;

    public float voxelScale = 0.1f;
    public float scale = 1f;
    public int pointCount = 100000;

    public string fileName = "E028 enkel klas.txt";

    private void Start()
    {
        system = GetComponent<ParticleSystem>();

        List<Vector3> positions = new List<Vector3>();
        List<Color> colors = new List<Color>();

        using (var sr = new StreamReader(Application.dataPath + "/"+ fileName))
        {
            var inv = CultureInfo.InvariantCulture;
            while (!sr.EndOfStream)
            {
                var parts = sr.ReadLine().Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries); // split op alle whitespace

                if (parts.Length < 6) continue; // skip rare lijnen

                positions.Add(new Vector3(
                    float.Parse(parts[0], inv),
                    float.Parse(parts[1], inv),
                    float.Parse(parts[2], inv)
                ));

                colors.Add(new Color(
                    float.Parse(parts[3], inv) / 255f,
                    float.Parse(parts[4], inv) / 255f,
                    float.Parse(parts[5], inv) / 255f
                ));
            }
        }

        SetVoxels(positions.ToArray(), colors.ToArray());
    }



    private void Update()
    {
        if (voxelsUpdated)
        {
            system.SetParticles(voxels, voxels.Length);
            voxelsUpdated = false;
        }
    }

    public void SetVoxels(Vector3[] positions, Color[] colors)
    {
        voxels = new ParticleSystem.Particle[positions.Length];

        for (int i = 0; i < positions.Length; i++)
        {
            voxels[i].position = positions[i] * scale;
            voxels[i].startColor = colors[i];
            voxels[i].startSize = voxelScale;
        }

        Debug.Log("Voxels set! Voxel count: " + voxels.Length);
        voxelsUpdated = true;
    }
}
