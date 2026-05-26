using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

/// Runtime PLY loader — laadt een binary little-endian PLY bestand
/// met float x/y/z en uchar r/g/b in een Mesh met MeshTopology.Points.
///
/// Gebruik:
///   1. Voeg dit script toe aan een leeg GameObject
///   2. Voeg een MeshFilter en MeshRenderer toe aan hetzelfde GameObject
///   3. Wijs het PCX billboard materiaal toe aan de MeshRenderer
///   4. Roep LaadPLY(pad) aan vanuit code of via de TourBuilder
///
/// Beperkingen:
///   - Enkel binary little-endian PLY
///   - Verwacht float x, y, z en uchar r, g, b (volgorde maakt niet uit)
///   - Unity Mesh limiet: max 4 miljoen punten per mesh (wordt automatisch gesplitst)

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class RuntimePLYLoader : MonoBehaviour
{
    [Header("Status")]
    public string   huidigBestand = "";
    public int      aantalPunten  = 0;
    public bool     isAanHetLaden = false;

    private MeshFilter   meshFilter;
    private MeshRenderer meshRenderer;

    // Max punten per Unity mesh (Unity limiet is 4294967295 met 32-bit index)
    private const int MAX_PER_MESH = 3000000;

    void Awake()
    {
        meshFilter   = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
    }

    // ── Publieke API ──────────────────────────────────────────────────────────

    public void LaadPLY(string pad)
    {
        if (isAanHetLaden) return;
        StartCoroutine(LaadPLYCoroutine(pad));
    }

    // ── Coroutine ─────────────────────────────────────────────────────────────

    IEnumerator LaadPLYCoroutine(string pad)
    {
        isAanHetLaden = true;
        huidigBestand = Path.GetFileName(pad);

        Debug.Log("PLY laden: " + pad);

        // Lees bestand in achtergrond thread
        Vector3[] posities  = null;
        Color32[] kleuren   = null;
        string    foutmelding = null;

        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                LeesPLY(pad, out posities, out kleuren);
            }
            catch (Exception e)
            {
                foutmelding = e.Message;
            }
        });
        thread.Start();

        // Wacht tot thread klaar is
        while (thread.IsAlive)
            yield return null;

        if (foutmelding != null)
        {
            Debug.LogError("PLY laad fout: " + foutmelding);
            isAanHetLaden = false;
            yield break;
        }

        aantalPunten = posities.Length;
        Debug.Log("Punten gelezen: " + aantalPunten);

        // Bereken gemiddelde X en Z voor centrering, en vloer via 1e percentiel Y
        float somX = 0f, somZ = 0f;
        float[] yWaarden = new float[posities.Length];
        for (int i = 0; i < posities.Length; i++)
        {
            somX += posities[i].x;
            somZ += posities[i].z;
            yWaarden[i] = posities[i].y;
        }
        float gemX = somX / posities.Length;
        float gemZ = somZ / posities.Length;

        System.Array.Sort(yWaarden);
        int percentielIndex = Mathf.Max(0, (int)(posities.Length * 0.01f));
        float vloerY = yWaarden[percentielIndex];

        Debug.Log($"Centrering: X={gemX:F2}, Z={gemZ:F2} | Vloer Y={vloerY:F2}");

        // Verschuif zodat centrum op X=0, Z=0 en vloer op Y=0
        for (int i = 0; i < posities.Length; i++)
        {
            posities[i] = new Vector3(
                posities[i].x - gemX,
                posities[i].y - vloerY,
                posities[i].z - gemZ
            );
        }

        // Bouw mesh(en) op hoofdthread
        BouwMesh(posities, kleuren);

        // Zet het GameObject zelf op de oorsprong
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;

        // Camera naar centrum op ooghoogte
        if (Camera.main != null)
        {
            Vector3 startPos = new Vector3(0, 1.7f, 0);
            Camera.main.transform.position = startPos;
            Debug.Log("Camera verplaatst naar: " + startPos);
        }

        isAanHetLaden = false;
        Debug.Log("PLY geladen: " + huidigBestand);
    }

    // ── PLY lezen ─────────────────────────────────────────────────────────────

    void LeesPLY(string pad, out Vector3[] posities, out Color32[] kleuren)
    {
        using (var stream = new FileStream(pad, FileMode.Open, FileAccess.Read))
        using (var reader = new BinaryReader(stream))
        {
            // ── Header lezen ──────────────────────────────────────────────────
            var props      = new System.Collections.Generic.List<(string type, string naam)>();
            int nPunten    = 0;
            bool ascii     = false;

            while (true)
            {
                string lijn = LeesHeaderLijn(reader).Trim();

                if (lijn.StartsWith("format ascii"))
                    ascii = true;
                else if (lijn.StartsWith("element vertex"))
                    nPunten = int.Parse(lijn.Split(' ')[2]);
                else if (lijn.StartsWith("property"))
                {
                    var d = lijn.Split(' ');
                    props.Add((d[1], d[2]));
                }
                else if (lijn == "end_header")
                    break;
            }

            if (ascii)
                throw new Exception("ASCII PLY niet ondersteund. Gebruik binary little-endian.");

            // Bereken stride en offsets
            int stride    = 0;
            int offX = -1, offY = -1, offZ = -1;
            int offR = -1, offG = -1, offB = -1;

            for (int i = 0; i < props.Count; i++)
            {
                var (type, naam) = props[i];
                if (naam == "x") offX = stride;
                if (naam == "y") offY = stride;
                if (naam == "z") offZ = stride;
                if (naam == "red")   offR = stride;
                if (naam == "green") offG = stride;
                if (naam == "blue")  offB = stride;
                stride += TypeGrootte(type);
            }

            if (offX < 0 || offY < 0 || offZ < 0)
                throw new Exception("PLY heeft geen x/y/z properties.");

            // ── Punten lezen ──────────────────────────────────────────────────
            posities = new Vector3[nPunten];
            kleuren  = new Color32[nPunten];

            byte[] buf = new byte[stride];

            for (int i = 0; i < nPunten; i++)
            {
                reader.Read(buf, 0, stride);

                float x = BitConverter.ToSingle(buf, offX);
                float y = BitConverter.ToSingle(buf, offY);
                float z = BitConverter.ToSingle(buf, offZ);

                // PLY naar Unity coördinatenstelsel: Y omhoog, Z vooruit
                posities[i] = new Vector3(x, z, y);

                if (offR >= 0 && offG >= 0 && offB >= 0)
                    kleuren[i] = new Color32(buf[offR], buf[offG], buf[offB], 255);
                else
                    kleuren[i] = new Color32(200, 200, 200, 255);
            }
        }
    }

    // ── Mesh bouwen ───────────────────────────────────────────────────────────

    void BouwMesh(Vector3[] posities, Color32[] kleuren)
    {
        // Verwijder oude child meshes
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        int totaal  = posities.Length;
        int chunks  = Mathf.CeilToInt((float)totaal / MAX_PER_MESH);
        var materiaal = meshRenderer.sharedMaterial;

        for (int c = 0; c < chunks; c++)
        {
            int start = c * MAX_PER_MESH;
            int count = Mathf.Min(MAX_PER_MESH, totaal - start);

            var subPos = new Vector3[count];
            var subKlr = new Color32[count];
            Array.Copy(posities, start, subPos, 0, count);
            Array.Copy(kleuren,  start, subKlr, 0, count);

            var mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices    = subPos;
            mesh.colors32    = subKlr;

            var indices = new int[count];
            for (int j = 0; j < count; j++) indices[j] = j;
            mesh.SetIndices(indices, MeshTopology.Points, 0);
            mesh.RecalculateBounds();

            if (c == 0)
            {
                // Eerste chunk op het hoofdobject
                meshFilter.mesh = mesh;
            }
            else
            {
                // Extra chunks als child GameObjects
                var child = new GameObject("Chunk_" + c);
                child.transform.SetParent(transform, false);
                child.AddComponent<MeshFilter>().mesh = mesh;
                var rend = child.AddComponent<MeshRenderer>();
                rend.sharedMaterial = materiaal;
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    string LeesHeaderLijn(BinaryReader reader)
    {
        var sb = new StringBuilder();
        while (true)
        {
            byte b = reader.ReadByte();
            if (b == '\n') break;
            if (b != '\r') sb.Append((char)b);
        }
        return sb.ToString();
    }

    int TypeGrootte(string type)
    {
        switch (type)
        {
            case "float":
            case "float32":
            case "int":
            case "int32":   return 4;
            case "double":
            case "float64": return 8;
            case "uchar":
            case "uint8":   return 1;
            case "short":
            case "int16":   return 2;
            case "uint":
            case "uint32":  return 4;
            default:        return 4;
        }
    }
}
