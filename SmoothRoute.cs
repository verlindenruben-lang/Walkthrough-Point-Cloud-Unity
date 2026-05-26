using System.Collections.Generic;
using UnityEngine;

public enum BewegingType
{
    Lerp,
    CatmullRom,
    CentripetalCatmullRom,
    Bezier
}

public class SmoothRoute : MonoBehaviour
{
    public Passeerpunt[] passeerpunten;

    [Header("Beweging")]
    public BewegingType bewegingType = BewegingType.Lerp;
    public float ooghoogte = 1.7f;
    public float bewegingsSnelheid = 2f;
    public float rotatieSnelheid = 6f;

    [Header("Kijkrichting")]
    public float kijkAfstand = 0.5f;

    [Header("Bezier")]
    [Tooltip("Schaal van de automatische control points (hogere waarde = rondere bochten)")]
    [Range(0.01f, 1f)]
    public float bezierAutoControlLength = 0.3f;

    [Tooltip("Als true worden control points automatisch herberekend bij elke wijziging. Zet uit om handmatig te bewerken.")]
    public bool bezierAutoUpdate = true;

    // Publiek + serialized zodat de editor ze kan aanpassen en Unity ze onthoudt
    // Per segment: [c1Uit, c2In] => 2 Vector3 per segment
    [HideInInspector] public Vector3[] bezierControls;

    [Header("Camera")]
    [Tooltip("Sleep hier de Main Camera in. SmoothRoute stuurt deze camera aan tijdens de tour.")]
    public Transform cameraTransform;

    // Runtime
    private List<int> routeIndexen = new List<int>();
    private int huidigRouteSegment = 0;
    private float t = 0f;
    private Vector3 startPos;
    private Vector3 doelPos;
    private bool tourActief = false;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Start()
    {
        HerstartTour();
    }

    public void HerstartTour()
    {
        if (passeerpunten == null || passeerpunten.Length < 2) return;

        BouwRoute();
        if (routeIndexen.Count < 2) return;

        BerekenBezierControls();

        huidigRouteSegment = 0;
        t = 0f;

        Vector3 beginPos = GetPuntPos(routeIndexen[0]);
        if (cameraTransform != null)
            cameraTransform.position = beginPos;
        else
            transform.position = beginPos;

        startPos = GetPuntPos(routeIndexen[0]);
        doelPos  = GetPuntPos(routeIndexen[1]);
        tourActief = true;
    }

    [Header("Lijn weergave")]
    public Material lijnMateriaal;
    [Tooltip("Breedte van de wandelingslijn in meters")]
    public float lijnDikte = 0.30f;

    public void TekenCurve()
    {
        var lr = GetComponent<LineRenderer>();
        if (lr == null) lr = gameObject.AddComponent<LineRenderer>();

        var route = BouwGizmoRoute();
        if (route.Count < 2) { lr.positionCount = 0; return; }

        if (lijnMateriaal != null)
            lr.material = lijnMateriaal;
        else
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader != null) lr.material = new Material(shader);
        }
        lr.startColor    = Color.cyan;
        lr.endColor      = Color.cyan;
        lr.startWidth    = lijnDikte;
        lr.endWidth      = lijnDikte;
        lr.useWorldSpace = true;

        if (bezierAutoUpdate || bezierControls == null || bezierControls.Length == 0)
            BerekenBezierControls();

        var posities = new System.Collections.Generic.List<Vector3>();
        int stappen  = 20;
        int segmenten = route.Count - 1;

        for (int seg = 0; seg < segmenten; seg++)
        {
            if (!IsGeldigPunt(route[seg]) || !IsGeldigPunt(route[seg+1])) continue;
            for (int stap = 0; stap <= stappen; stap++)
            {
                float t = stap / (float)stappen;
                Vector3 pos;
                switch (bewegingType)
                {
                    case BewegingType.Bezier:
                        pos = (bezierControls != null && bezierControls.Length >= (seg+1)*2)
                            ? CubicBezier(GetPuntPos(route[seg]), bezierControls[seg*2],
                                          bezierControls[seg*2+1], GetPuntPos(route[seg+1]), t)
                            : Vector3.Lerp(GetPuntPos(route[seg]), GetPuntPos(route[seg+1]), t);
                        break;
                    case BewegingType.CatmullRom:
                        pos = CatmullRom(
                            GetPuntPos(GetGizmoRouteIndex(route, seg-1)),
                            GetPuntPos(route[seg]), GetPuntPos(route[seg+1]),
                            GetPuntPos(GetGizmoRouteIndex(route, seg+2)), t);
                        break;
                    case BewegingType.CentripetalCatmullRom:
                        pos = CentripetalCatmullRom(
                            GetPuntPos(GetGizmoRouteIndex(route, seg-1)),
                            GetPuntPos(route[seg]), GetPuntPos(route[seg+1]),
                            GetPuntPos(GetGizmoRouteIndex(route, seg+2)), t);
                        break;
                    default:
                        pos = Vector3.Lerp(GetPuntPos(route[seg]), GetPuntPos(route[seg+1]), t);
                        break;
                }
                if (posities.Count == 0 || Vector3.Distance(posities[posities.Count-1], pos) > 0.001f)
                    posities.Add(pos);
            }
        }

        lr.positionCount = posities.Count;
        for (int i = 0; i < posities.Count; i++)
            lr.SetPosition(i, posities[i]);
    }

    public bool TourIsActief() { return tourActief; }

    public void StopTour()
    {
        tourActief = false;
    }

    void Update()
    {
        if (!tourActief) return;
        if (routeIndexen == null || routeIndexen.Count < 2) return;
        if (huidigRouteSegment >= routeIndexen.Count - 1)
        {
            tourActief = false;
            return;
        }

        int startIndex = routeIndexen[huidigRouteSegment];
        int eindIndex  = routeIndexen[huidigRouteSegment + 1];
        if (!IsGeldigPunt(startIndex) || !IsGeldigPunt(eindIndex)) return;

        float snelheid = passeerpunten[startIndex].doorloopsnelheid > 0f
            ? passeerpunten[startIndex].doorloopsnelheid
            : bewegingsSnelheid;

        float afstand = Vector3.Distance(startPos, doelPos);
        float duur    = afstand / Mathf.Max(snelheid, 0.01f);

        t += Time.deltaTime / duur;
        t  = Mathf.Clamp01(t);

        Vector3 nieuwePos = BerekenSegmentPos(huidigRouteSegment, t);

        // Beweeg camera als die gekoppeld is, anders het eigen GameObject
        Transform doel = cameraTransform != null ? cameraTransform : transform;
        doel.position = nieuwePos;

        Vector3 kijkRichting = BerekenKijkRichting(huidigRouteSegment, t, nieuwePos);
        kijkRichting.y = 0f;

        if (kijkRichting.sqrMagnitude > 0.0001f)
        {
            Quaternion doelRotatie = Quaternion.LookRotation(kijkRichting.normalized, Vector3.up);
            doel.rotation = Quaternion.Slerp(doel.rotation, doelRotatie, Time.deltaTime * rotatieSnelheid);
        }

        if (t >= 1f)
        {
            doel.position = doelPos;
            t = 0f;
            huidigRouteSegment++;
            if (huidigRouteSegment >= routeIndexen.Count - 1) return;
            startPos = GetPuntPos(routeIndexen[huidigRouteSegment]);
            doelPos  = GetPuntPos(routeIndexen[huidigRouteSegment + 1]);
        }
    }

    // ── Route bouwen ─────────────────────────────────────────────────────────

    void BouwRoute()
    {
        routeIndexen.Clear();
        if (passeerpunten == null || passeerpunten.Length == 0) return;

        for (int i = 0; i < passeerpunten.Length; i++)
        {
            if (!IsGeldigPunt(i)) continue;
            routeIndexen.Add(i);
            if (passeerpunten[i].looptDood && i > 0 && IsGeldigPunt(i - 1))
                routeIndexen.Add(i - 1);
        }
    }

    // ── Bezier control points ─────────────────────────────────────────────────

    public void BerekenBezierControls()
    {
        if (passeerpunten == null || passeerpunten.Length < 2)
        {
            bezierControls = new Vector3[0];
            return;
        }

        List<int> route = BouwGizmoRoute();
        int aantalPunten    = route.Count;
        int aantalSegmenten = aantalPunten - 1;
        if (aantalSegmenten <= 0)
        {
            bezierControls = new Vector3[0];
            return;
        }

        bezierControls = new Vector3[aantalSegmenten * 2];

        // Bereken per ankerpunt één gespiegelde tangent zodat c_uit en c_in
        // van hetzelfde ankerpunt altijd even ver liggen (Mirrored modus)
        Vector3[] tangenten = new Vector3[aantalPunten];

        for (int i = 0; i < aantalPunten; i++)
        {
            if (!IsGeldigPunt(route[i])) continue;

            Vector3 huidig = GetPuntPos(route[i]);

            int prevI = i > 0 ? i - 1 : i;
            int nextI = i < aantalPunten - 1 ? i + 1 : i;

            Vector3 prev = i > 0 && IsGeldigPunt(route[prevI])
                ? GetPuntPos(route[prevI])
                : huidig + (huidig - GetPuntPos(route[nextI]));

            Vector3 next = i < aantalPunten - 1 && IsGeldigPunt(route[nextI])
                ? GetPuntPos(route[nextI])
                : huidig + (huidig - GetPuntPos(route[prevI]));

            // Tangent van prev naar next: garandeert gespiegelde control points
            tangenten[i] = (next - prev).normalized;
        }

        // Zet tangenten om naar control points per segment
        for (int seg = 0; seg < aantalSegmenten; seg++)
        {
            if (!IsGeldigPunt(route[seg]) || !IsGeldigPunt(route[seg + 1])) continue;

            Vector3 p0    = GetPuntPos(route[seg]);
            Vector3 p3    = GetPuntPos(route[seg + 1]);
            float   schaal = Vector3.Distance(p0, p3) * bezierAutoControlLength;

            bezierControls[seg * 2]     = p0 + tangenten[seg]     * schaal;
            bezierControls[seg * 2 + 1] = p3 - tangenten[seg + 1] * schaal;
        }
    }

    // ── Segment positie ───────────────────────────────────────────────────────

    Vector3 BerekenSegmentPos(int routeSegmentIndex, float tWaarde)
    {
        switch (bewegingType)
        {
            case BewegingType.CatmullRom:
                return BerekenCatmullRomRoutePos(routeSegmentIndex, tWaarde);
            case BewegingType.CentripetalCatmullRom:
                return BerekenCentripetalCatmullRomRoutePos(routeSegmentIndex, tWaarde);
            case BewegingType.Bezier:
                return BerekenBezierSegmentPos(routeSegmentIndex, tWaarde);
            default:
                return Vector3.Lerp(
                    GetPuntPos(routeIndexen[routeSegmentIndex]),
                    GetPuntPos(routeIndexen[routeSegmentIndex + 1]),
                    Mathf.SmoothStep(0f, 1f, tWaarde));
        }
    }

    // ── Cubic Bezier ──────────────────────────────────────────────────────────

    Vector3 BerekenBezierSegmentPos(int segmentIndex, float tWaarde)
    {
        if (bezierControls == null || bezierControls.Length < (segmentIndex + 1) * 2)
            BerekenBezierControls();

        Vector3 p0 = GetPuntPos(routeIndexen[segmentIndex]);
        Vector3 p1 = bezierControls[segmentIndex * 2];
        Vector3 p2 = bezierControls[segmentIndex * 2 + 1];
        Vector3 p3 = GetPuntPos(routeIndexen[segmentIndex + 1]);

        return CubicBezier(p0, p1, p2, p3, tWaarde);
    }

    public static Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u  = 1f - t;
        float u2 = u * u;
        float u3 = u2 * u;
        float t2 = t * t;
        float t3 = t2 * t;
        return u3 * p0 + 3f * u2 * t * p1 + 3f * u * t2 * p2 + t3 * p3;
    }

    // ── Kijkrichting ──────────────────────────────────────────────────────────

    Vector3 BerekenKijkRichting(int routeSegmentIndex, float huidigeT, Vector3 huidigePos)
    {
        float segmentLengte = Vector3.Distance(startPos, doelPos);
        if (segmentLengte < 0.0001f) return transform.forward;

        float extraT    = kijkAfstand / segmentLengte;
        float vooruitT  = huidigeT + extraT;
        int kijkSegment = routeSegmentIndex;

        while (vooruitT > 1f && kijkSegment < routeIndexen.Count - 2)
        {
            vooruitT -= 1f;
            kijkSegment++;
        }

        return BerekenSegmentPos(kijkSegment, Mathf.Clamp01(vooruitT)) - huidigePos;
    }

    // ── CatmullRom ────────────────────────────────────────────────────────────

    Vector3 BerekenCatmullRomRoutePos(int routeSegmentIndex, float tWaarde)
    {
        int i0 = GetRoutePuntIndex(routeSegmentIndex - 1);
        int i1 = GetRoutePuntIndex(routeSegmentIndex);
        int i2 = GetRoutePuntIndex(routeSegmentIndex + 1);
        int i3 = GetRoutePuntIndex(routeSegmentIndex + 2);
        return CatmullRom(GetPuntPos(i0), GetPuntPos(i1), GetPuntPos(i2), GetPuntPos(i3), tWaarde);
    }

    Vector3 BerekenCentripetalCatmullRomRoutePos(int routeSegmentIndex, float tWaarde)
    {
        int i0 = GetRoutePuntIndex(routeSegmentIndex - 1);
        int i1 = GetRoutePuntIndex(routeSegmentIndex);
        int i2 = GetRoutePuntIndex(routeSegmentIndex + 1);
        int i3 = GetRoutePuntIndex(routeSegmentIndex + 2);
        return CentripetalCatmullRom(GetPuntPos(i0), GetPuntPos(i1), GetPuntPos(i2), GetPuntPos(i3), tWaarde);
    }

    int GetRoutePuntIndex(int routeIndex)
    {
        if (routeIndex < 0)                   return routeIndexen[0];
        if (routeIndex >= routeIndexen.Count) return routeIndexen[routeIndexen.Count - 1];
        return routeIndexen[routeIndex];
    }

    Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float tWaarde)
    {
        float t2 = tWaarde * tWaarde;
        float t3 = t2 * tWaarde;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * tWaarde +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    Vector3 CentripetalCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
    {
        float alpha = 0.5f;
        float t0 = 0f;
        float t1 = t0 + Mathf.Pow(Vector3.Distance(p0, p1), alpha);
        float t2 = t1 + Mathf.Pow(Vector3.Distance(p1, p2), alpha);
        float t3 = t2 + Mathf.Pow(Vector3.Distance(p2, p3), alpha);
        float t  = Mathf.Lerp(t1, t2, u);
        Vector3 A1 = InterpoleerPunten(p0, p1, t0, t1, t);
        Vector3 A2 = InterpoleerPunten(p1, p2, t1, t2, t);
        Vector3 A3 = InterpoleerPunten(p2, p3, t2, t3, t);
        Vector3 B1 = InterpoleerPunten(A1, A2, t0, t2, t);
        Vector3 B2 = InterpoleerPunten(A2, A3, t1, t3, t);
        return InterpoleerPunten(B1, B2, t1, t2, t);
    }

    Vector3 InterpoleerPunten(Vector3 a, Vector3 b, float ta, float tb, float t)
    {
        if (Mathf.Approximately(tb, ta)) return a;
        return ((tb - t) / (tb - ta)) * a + ((t - ta) / (tb - ta)) * b;
    }

    // ── Editor: punt toevoegen ────────────────────────────────────────────────

    public void VoegPuntToe(Vector3 wereldPositie, bool looptDood = false)
    {
        GameObject nieuwObject = new GameObject("Punt " + ((passeerpunten?.Length ?? 0) + 1));
        nieuwObject.transform.position = wereldPositie;
        nieuwObject.transform.SetParent(transform);

        Passeerpunt nieuwPunt = new Passeerpunt
        {
            location  = nieuwObject.transform,
            looptDood = looptDood
        };

        System.Array.Resize(ref passeerpunten, (passeerpunten?.Length ?? 0) + 1);
        passeerpunten[passeerpunten.Length - 1] = nieuwPunt;

        if (bezierAutoUpdate)
            BerekenBezierControls();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public Vector3 GetPuntPos(int index)
    {
        Vector3 pos = passeerpunten[index].location.position;
        pos.y += ooghoogte;
        return pos;
    }

    public bool IsGeldigPunt(int index)
    {
        return passeerpunten != null &&
               index >= 0 &&
               index < passeerpunten.Length &&
               passeerpunten[index] != null &&
               passeerpunten[index].location != null;
    }

    public List<int> BouwGizmoRoute()
    {
        List<int> lijst = new List<int>();
        if (passeerpunten == null) return lijst;

        for (int i = 0; i < passeerpunten.Length; i++)
        {
            if (!IsGeldigPunt(i)) continue;
            lijst.Add(i);
            if (passeerpunten[i].looptDood && i > 0 && IsGeldigPunt(i - 1))
                lijst.Add(i - 1);
        }
        return lijst;
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        if (passeerpunten == null || passeerpunten.Length == 0) return;

        List<int> gizmoRoute = BouwGizmoRoute();

        for (int i = 0; i < passeerpunten.Length; i++)
        {
            if (!IsGeldigPunt(i)) continue;
            Gizmos.color = passeerpunten[i].looptDood ? Color.magenta : Color.red;
            Gizmos.DrawSphere(GetPuntPos(i), 0.15f);
        }

        if (gizmoRoute.Count < 2) return;

        switch (bewegingType)
        {
            case BewegingType.Lerp:
                TekenLerpRoute(gizmoRoute); break;
            case BewegingType.CatmullRom:
                TekenCatmullRomRoute(gizmoRoute, false); break;
            case BewegingType.CentripetalCatmullRom:
                TekenCatmullRomRoute(gizmoRoute, true); break;
            case BewegingType.Bezier:
                TekenBezierRoute(gizmoRoute); break;
        }
    }

    void TekenLerpRoute(List<int> route)
    {
        Gizmos.color = Color.yellow;
        for (int i = 0; i < route.Count - 1; i++)
        {
            if (!IsGeldigPunt(route[i]) || !IsGeldigPunt(route[i + 1])) continue;
            Gizmos.DrawLine(GetPuntPos(route[i]), GetPuntPos(route[i + 1]));
        }
    }

    public void TekenBezierRoute(List<int> route)
    {
        // Herbereken alleen als autoUpdate aan staat
        if (bezierAutoUpdate || bezierControls == null || bezierControls.Length != (route.Count - 1) * 2)
        {
            // Tijdelijke lokale berekening voor gizmo zonder routeIndexen te overschrijven
            BerekenBezierControlsVoorRoute(route, out Vector3[] tijdControls);
            TekenBezierMetControls(route, tijdControls);
        }
        else
        {
            TekenBezierMetControls(route, bezierControls);
        }
    }

    void BerekenBezierControlsVoorRoute(List<int> route, out Vector3[] controls)
    {
        int aantalSegmenten = route.Count - 1;
        controls = new Vector3[aantalSegmenten * 2];

        for (int seg = 0; seg < aantalSegmenten; seg++)
        {
            if (!IsGeldigPunt(route[seg]) || !IsGeldigPunt(route[seg + 1])) continue;

            Vector3 p0 = GetPuntPos(route[seg]);
            Vector3 p3 = GetPuntPos(route[seg + 1]);

            Vector3 prev = seg > 0 && IsGeldigPunt(route[seg - 1])
                ? GetPuntPos(route[seg - 1]) : p0 + (p0 - p3);
            Vector3 next = seg < aantalSegmenten - 1 && IsGeldigPunt(route[seg + 2])
                ? GetPuntPos(route[seg + 2]) : p3 + (p3 - p0);

            Vector3 tangent0 = (p3 - prev).normalized;
            Vector3 tangent1 = (next - p0).normalized;
            float   schaal   = Vector3.Distance(p0, p3) * bezierAutoControlLength;

            controls[seg * 2]     = p0 + tangent0 * schaal;
            controls[seg * 2 + 1] = p3 - tangent1 * schaal;
        }
    }

    void TekenBezierMetControls(List<int> route, Vector3[] controls)
    {
        int stappen = 20;
        int aantalSegmenten = route.Count - 1;

        for (int seg = 0; seg < aantalSegmenten; seg++)
        {
            if (!IsGeldigPunt(route[seg]) || !IsGeldigPunt(route[seg + 1])) continue;
            if (controls.Length < (seg + 1) * 2) continue;

            Vector3 p0 = GetPuntPos(route[seg]);
            Vector3 p1 = controls[seg * 2];
            Vector3 p2 = controls[seg * 2 + 1];
            Vector3 p3 = GetPuntPos(route[seg + 1]);

            Gizmos.color = Color.blue;
            Vector3 vorige = p0;
            for (int stap = 1; stap <= stappen; stap++)
            {
                Vector3 huidige = CubicBezier(p0, p1, p2, p3, stap / (float)stappen);
                Gizmos.DrawLine(vorige, huidige);
                vorige = huidige;
            }

            // Control point lijnen
            Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.5f);
            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p3, p2);
            Gizmos.DrawSphere(p1, 0.07f);
            Gizmos.DrawSphere(p2, 0.07f);
        }
    }

    void TekenCatmullRomRoute(List<int> route, bool centripetal)
    {
        int stappenPerSegment = 20;
        for (int segment = 0; segment < route.Count - 1; segment++)
        {
            Vector3 vorige = BerekenCatmullRomGizmoPos(route, segment, 0f, centripetal);
            for (int stap = 1; stap <= stappenPerSegment; stap++)
            {
                float   lokaleT = stap / (float)stappenPerSegment;
                Vector3 huidige = BerekenCatmullRomGizmoPos(route, segment, lokaleT, centripetal);
                Gizmos.color = centripetal ? Color.green : Color.cyan;
                Gizmos.DrawLine(vorige, huidige);
                vorige = huidige;
            }
        }
    }

    Vector3 BerekenCatmullRomGizmoPos(List<int> route, int segmentIndex, float tWaarde, bool centripetal)
    {
        int i0 = GetGizmoRouteIndex(route, segmentIndex - 1);
        int i1 = GetGizmoRouteIndex(route, segmentIndex);
        int i2 = GetGizmoRouteIndex(route, segmentIndex + 1);
        int i3 = GetGizmoRouteIndex(route, segmentIndex + 2);

        Vector3 p0 = GetPuntPos(i0); Vector3 p1 = GetPuntPos(i1);
        Vector3 p2 = GetPuntPos(i2); Vector3 p3 = GetPuntPos(i3);

        return centripetal
            ? CentripetalCatmullRom(p0, p1, p2, p3, tWaarde)
            : CatmullRom(p0, p1, p2, p3, tWaarde);
    }

    int GetGizmoRouteIndex(List<int> route, int index)
    {
        if (index < 0)            return route[0];
        if (index >= route.Count) return route[route.Count - 1];
        return route[index];
    }
}

[System.Serializable]
public class Passeerpunt
{
    public bool      looptDood        = false;
    public float     doorloopsnelheid = 0f;
    public Transform location;
}
