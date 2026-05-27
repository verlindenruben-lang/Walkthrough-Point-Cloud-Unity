using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.VFX;

public class PointCloudRenderer : MonoBehaviour
{
    public string fileName = "Werktuig subsampled.txt"; // in Assets/
    public uint resolution = 1024;
    public float particleSize = 0.1f;
    public float scale = 1f;


    Texture2D texColor;
    Texture2D texPosScale;
    VisualEffect vfx;
    bool toUpdate = false;
    uint particleCount = 0;

    private void Start()
    {
        vfx = GetComponent<VisualEffect>();

        // 1) Lees puntenwolk
        var positions = new List<Vector3>();
        var colors = new List<Color>();
        var inv = CultureInfo.InvariantCulture;

        using (var sr = new StreamReader(Application.dataPath + "/" + fileName))
            while (!sr.EndOfStream)
            {
                var p = sr.ReadLine().Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 6) continue;

                positions.Add(new Vector3(
                    float.Parse(p[0], inv),
                    float.Parse(p[1], inv),
                    float.Parse(p[2], inv)));

                colors.Add(new Color(
                    float.Parse(p[3], inv) / 255f,
                    float.Parse(p[4], inv) / 255f,
                    float.Parse(p[5], inv) / 255f,
                    1f));
            }

        Debug.Log("Rendering " + positions.Count + " particles");

        // 2) Upload naar VFX
        SetParticles(positions.ToArray(), colors.ToArray());
    }

    private void Update()
    {
        if (!toUpdate) return;
        toUpdate = false;

        vfx.Reinit();
        vfx.SetUInt("Particle count", particleCount);
        vfx.SetTexture("TexColor", texColor);
        vfx.SetTexture("TexPosScale", texPosScale);
        vfx.SetUInt("Resolution", resolution);
    }

    public void SetParticles(Vector3[] positions, Color[] colors)
    {
        // Belangrijk: texture kan max resolution*resolution punten bevatten
        int max = (int)resolution * (int)resolution;
        int count = Mathf.Min(positions.Length, max);

        texColor = new Texture2D(
            (count > (int)resolution) ? (int)resolution : count,
            Mathf.Clamp(count / (int)resolution, 1, (int)resolution),
            TextureFormat.RGBAFloat,
            false
        );

        texPosScale = new Texture2D(
            texColor.width,
            texColor.height,
            TextureFormat.RGBAFloat,
            false
        );

        int w = texColor.width;
        int h = texColor.height;

        int i = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (i >= count) break;

                texColor.SetPixel(x, y, colors[i]);
                texPosScale.SetPixel(x, y, new Color(
                    positions[i].x*scale, positions[i].y*scale, positions[i].z*scale, particleSize));

                i++;
            }
        }

        texColor.Apply();
        texPosScale.Apply();

        particleCount = (uint)count;
        toUpdate = true;
    }
}
