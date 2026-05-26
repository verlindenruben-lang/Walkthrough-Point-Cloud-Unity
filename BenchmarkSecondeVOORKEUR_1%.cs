using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class FPSRectangleTestss : MonoBehaviour
{
    // === Beweging ===
    public float width = 11f;
    public float height = 5f;
    public float speed = 3f;

    // === Stopvoorwaarden ===
    public float testDuration = 30f;
    public int maxFrames = 10000;

    // === Bestand ===
    public string filePath = @"C:\Users\r0849848\Downloads\fps_test.csv";

    // === Interne data ===
    private List<float> dtList = new List<float>();   // zoals je eerste script: deltaTimes
    private List<float> fpsList = new List<float>();  // handig voor min/max/ci95
    private List<float> timeList = new List<float>();
    private double lastTime = -1.0;

    private float startTime;
    private int frameCount = 0;
    private bool testFinished = false;

    private int currentCorner = 1;
    private Vector3[] corners;
    private double startRealTime;

    // vaste (niet instelbare) rustige draai
    private const float ROTATE_SMOOTH = 5f;

    void Start()
    {
        corners = new Vector3[4];
        corners[0] = transform.position;
        corners[1] = corners[0] + new Vector3(width, 0, 0);
        corners[2] = corners[1] + new Vector3(0, 0, height);
        corners[3] = corners[0] + new Vector3(0, 0, height);

        startRealTime = Time.realtimeSinceStartupAsDouble;

    }

void Update()
{
    if (testFinished) return;

    double now = Time.realtimeSinceStartupAsDouble;
    float t = (float)(now - startRealTime);


    if (lastTime < 0.0)
    {
        lastTime = now;
        return;
    }

    float dt = (float)(now - lastTime);
    lastTime = now;

    if (dt <= 0f) return;

    

    // log (nu correct gemeten)
    dtList.Add(dt);
    fpsList.Add(1f / dt);
    timeList.Add(t);

    frameCount++;

    if (t >= testDuration || frameCount >= maxFrames)
    {
        testFinished = true;
        Debug.Log("Test klaar - druk op knop om CSV te schrijven");
        return;
    }

    MoveRectangle(); // of jouw beweging
}


    void MoveRectangle()
    {
        Vector3 target = corners[currentCorner];

        // bewegen
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        // rustig draaien naar bewegingsrichting
        Vector3 dir = target - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion desired = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desired, ROTATE_SMOOTH * Time.deltaTime);
        }

        // hoek gehaald -> volgende hoek
        if (Vector3.Distance(transform.position, target) < 0.1f)
        {
            currentCorner = (currentCorner + 1) % 4;
        }
    }

    void OnGUI()
    {
        if (!testFinished) return;

        if (GUI.Button(new Rect(20, 20, 250, 45), "Schrijf CSV bestand"))
        {
            WriteCSV();
        }
    }

    void WriteCSV()
    {
        // zorg dat map bestaat
        string dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using (StreamWriter writer = new StreamWriter(filePath))
        {
            writer.WriteLine("SampleIndex;TimeSeconds;FPS");

            for (int i = 0; i < fpsList.Count; i++)
            {
                writer.WriteLine($"{i};{timeList[i]:F4};{fpsList[i]:F2}");
            }

            if (fpsList.Count > 0)
            {
                
                // averageDt = som(dt)/n
                // averageFps = 1/averageDt
                float avgFps = AverageFpsFromDeltaTime(dtList);

                float min = Min(fpsList);
                float max = Max(fpsList);
                float ci95 = Confidence95(fpsList); // 95% CI van het gemiddelde FPS (op basis van fps samples)

                writer.WriteLine("");
                writer.WriteLine($"Gemiddelde FPS;{avgFps:F2}");
                writer.WriteLine($"Minimum FPS;{min:F2}");
                writer.WriteLine($"Maximum FPS;{max:F2}");
                writer.WriteLine($"95% interval (+-);{ci95:F2}");
                writer.WriteLine($"Gemiddelde laagste 1%;{LowPercentileAverage(fpsList, 0.01f):F2}");
            }
        }

        Debug.Log("CSV geschreven naar: " + filePath);
    }

    // === Hulpfuncties ===

   
    // Gemiddelde van de laagste x% FPS waarden (bv. 0.01 = laagste 1%)
    float LowPercentileAverage(List<float> list, float fraction)
    {
        var gesorteerd = new List<float>(list);
        gesorteerd.Sort();

        int aantalPunten = Mathf.Max(1, (int)(gesorteerd.Count * fraction));
        float som = 0f;
        for (int i = 0; i < aantalPunten; i++)
            som += gesorteerd[i];

        return som / aantalPunten;
    }

    float AverageFpsFromDeltaTime(List<float> list)
    {
        float sumDt = 0f;
        foreach (float dt in list) sumDt += dt;

        float avgDt = sumDt / list.Count;
        return 1f / Mathf.Max(0.000001f, avgDt);
    }

    float Min(List<float> list)
    {
        float m = list[0];
        foreach (float v in list) if (v < m) m = v;
        return m;
    }

    float Max(List<float> list)
    {
        float m = list[0];
        foreach (float v in list) if (v > m) m = v;
        return m;
    }

    // 95% betrouwbaarheidsinterval halfbreedte (±) rond het gemiddelde:
    // 1.96 * (sd / sqrt(n))
    float Confidence95(List<float> list)
    {
        float mean = 0f;
        for (int i = 0; i < list.Count; i++) mean += list[i];
        mean /= list.Count;

        float sum = 0f;
        for (int i = 0; i < list.Count; i++)
        {
            float d = list[i] - mean;
            sum += d * d;
        }

        // sample sd (n-1) is net iets beter
        float sd = Mathf.Sqrt(sum / Mathf.Max(1, list.Count - 1));

        return 1.96f * (sd / Mathf.Sqrt(list.Count));
    }
}
