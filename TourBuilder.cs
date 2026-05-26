using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// Canvas structuur:
///   Panel0Puntenwolk
///     Button           KnopModusDemo
///     Button           KnopModusPLY
///     SubPanelPLY
///       TMP_Dropdown   DropdownPLYBestanden
///       Button         KnopKiesPLYMap
///       Button         KnopLaadPuntenwolk
///       Button         KnopNaarVerdiepingen
///       TMP_Text       TekstLaadStatusPLY
///     SubPanelDemo
///       TMP_Dropdown   DropdownPuntenwolk
///       Button         KnopBevestigPuntenwolk
///   Panel1Verdiepingen
///     TMP_InputField   InvoerVerdiepNaam
///     TMP_InputField   InvoerHoogte
///     Button           KnopVerdiepToevoegen
///     Button           KnopVerdiepVerwijderen
///     TMP_Dropdown     DropdownVerdieping
///     TMP_Text         TekstHoogte
///     Button           KnopNaar2
///     Button           KnopTerugNaarPuntenwolk
///   Panel2Punten
///     TMP_Dropdown     DropdownInterpolatie
///     Button           KnopToevoegen
///     Button           KnopVerwijderen
///     Button           KnopReset
///     TMP_Text         TekstStatus
///     Button           KnopTerug1
///   Panel4Instellingen
///     Button           KnopSnelheidPlus / KnopSnelheidMin
///     Button           KnopRotatiePlus  / KnopRotatieMin
///     Button           KnopOoghoogtePlus / KnopOoghoogteMin
///     Button           KnopPuntGrootterMaken / KnopPuntKleinerMaken
///     TMP_Text         TekstSnelheid / TekstRotatie / TekstOoghoogte / TekstPuntGrootte
///     Button           KnopStopTourInstellingen
///   PanelStopTour  (kleine overlay, standaard verborgen)
///     Button           KnopStopTourVolledig

[RequireComponent(typeof(SmoothRoute))]
public class TourBuilder : MonoBehaviour
{
    [Header("Camera")]
    public Camera tourCamera;
    public float  topDownHoogte   = 50f;
    public float  defaultNearClip = 0.3f;

    [Header("Panels")]
    public GameObject panel0Puntenwolk;
    public GameObject panel1Verdiepingen;
    public GameObject panel2Punten;
    public GameObject panel4Instellingen;
    public GameObject panelStopTour;

    [Header("UI — Panel 0: Puntenwolk laden (PLY)")]
    public RuntimePLYLoader plyLoader;
    public Button           knopKiesPLYMap;
    public TMP_Dropdown     dropdownPLYBestanden;
    public Button           knopLaadPuntenwolk;
    public TMP_Text         tekstLaadStatusPLY;
    public Button           knopNaarVerdiepingen;

    [Header("UI — Panel 0: Demo modus (PCX)")]
    public GameObject   subPanelPLY;
    public GameObject   subPanelDemo;
    public Button       knopModusPLY;
    public Button       knopModusDemo;
    public TMP_Dropdown dropdownPuntenwolk;
    public Button       knopBevestigPuntenwolk;
    public GameObject[] puntenwolken;


    public TMP_InputField invoerVerdiepNaam;
    public TMP_InputField invoerHoogte;
    public Button         knopVerdiepToevoegen;
    public Button         knopVerdiepVerwijderen;
    public TMP_Dropdown   dropdownVerdieping;
    public TMP_Text       tekstHoogte;
    public Button         knopNaar2;
    public Button         knopTerugNaarPuntenwolk;

    [Header("UI — Panel 2: Punten")]
    public TMP_Dropdown dropdownInterpolatie;
    public TMP_Dropdown dropdownVerdiepingPanel2;
    public TMP_Text     tekstHoogtePanel2;
    public Button       knopToevoegen;
    public Button       knopVerwijderen;
    public Button       knopReset;
    public TMP_Text     tekstStatus;
    public GameObject   previewBol;
    public Button       knopTerug1;
    public Button       knopStartMetInstellingen;
    public Button       knopStartVolledigScherm;

    [Header("UI — Panel 4: Instellingen")]
    public Button   knopSnelheidPlus;
    public Button   knopSnelheidMin;
    public Button   knopRotatiePlus;
    public Button   knopRotatieMin;
    public Button   knopOoghoogtePlus;
    public Button   knopOoghoogteMin;
    public Button   knopPuntGrootterMaken;
    public Button   knopPuntKleinerMaken;
    public TMP_Text tekstSnelheid;
    public TMP_Text tekstRotatie;
    public TMP_Text tekstOoghoogte;
    public TMP_Text tekstPuntGrootte;
    public Button   knopStopTourInstellingen;

    [Header("UI — Stop Tour overlay")]
    public Button knopStopTourVolledig;

    [Header("UI — Panel 1: JSON laden")]
    public Button         knopKiesLaadMap;
    public TMP_Dropdown   dropdownJsonBestanden;
    public Button         knopLaden;
    public TMP_Text       tekstLaadStatus;

    [Header("UI — Panel 2: JSON opslaan")]
    public GameObject[]   verbergBijPuntenPlaatsen;
    public TMP_InputField invoerTourNaam;
    public Button         knopKiesOpslagMap;
    public Button         knopOpslaan;
    public TMP_Text       tekstOpslagStatus;

    [Header("Puntenwolk")]
    public MeshRenderer puntenwolkRenderer;

    [Header("Opname")]
    public Button         knopKiesOpnameMap;
    public TMP_Text       tekstOpnameMap;
    public TMP_InputField invoerOpnameNaam;
    public string         opnameMap       = "";
    public string         opnameNaam      = "Tour_Opname";
    public int            opnameFramerate = 30;

    [Header("Instellingen")]
    public Color markeringKleur   = new Color(1f, 0.4f, 0.1f);
    public float markeringGrootte = 0.4f;
    public float lijnDikte        = 0.30f;

    // ── Verdieping data ───────────────────────────────────────────────────────

    [System.Serializable]
    public class Verdieping
    {
        public string naam;
        public float  hoogte;
        public Verdieping(string naam, float hoogte) { this.naam = naam; this.hoogte = hoogte; }
    }

    private List<Verdieping> verdiepingen         = new List<Verdieping>();
    private int              huidigeVerdiepingIdx  = -1;

    private float HuidigeHoogte =>
        huidigeVerdiepingIdx >= 0 && huidigeVerdiepingIdx < verdiepingen.Count
        ? verdiepingen[huidigeVerdiepingIdx].hoogte
        : 0f;

    // ── Private state ─────────────────────────────────────────────────────────

    private SmoothRoute      smoothRoute;
    private bool             plyModusActief      = true;
    private bool             puntToevoegenActief = false;
    private bool             tourLoopt           = false;
    private bool             volledigScherm      = false;

    // Frame-gebaseerde opname (werkt in Editor én standalone build)
    private bool   frameOpnameActief = false;
    private int    frameIndex        = 0;
    private string frameOpnameMap    = "";

    private List<GameObject> markeringen         = new List<GameObject>();
    private List<GameObject> controlPointBollen  = new List<GameObject>();
    private bool             sleepActief         = false;
    private int              gesleeptControlIdx  = -1;
    private GameObject       gesleeptBol         = null;
    private Plane            grondVlak           = new Plane(Vector3.up, Vector3.zero);

    private Vector3    cameraStartPos;
    private Quaternion cameraStartRot;
    private float      cameraStartNear;
    private float      cameraStartFar;
    private bool       cameraOpgeslagen = false;

    private float huidigeGrootte    = 5f;

    // JSON opslaan/laden
    private string       opslagMap          = "";
    private string       laadMap            = "";
    private List<string> gevondenBestanden  = new List<string>();
    private List<string> gevondenPLY        = new List<string>();
    private string       plyMap             = "";

    // ── JSON data klassen ─────────────────────────────────────────────────────

    [System.Serializable]
    public class Vector3Data
    {
        public float x, y, z;
        public Vector3Data() {}
        public Vector3Data(Vector3 v) { x = v.x; y = v.y; z = v.z; }
        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    [System.Serializable]
    public class VerdiepingData
    {
        public string naam;
        public float  hoogte;
    }

    [System.Serializable]
    public class PasSeerpuntData
    {
        public float x, y, z;
        public float doorloopsnelheid;
        public bool  looptDood;
    }

    [System.Serializable]
    public class TourData
    {
        public string naam              = "Nieuwe tour";
        public string datum             = "";
        public float  bewegingsSnelheid = 2f;
        public float  rotatieSnelheid   = 6f;
        public float  ooghoogte         = 1.7f;
        public float  puntGrootte       = 5f;
        public int    interpolatieModus = 0;
        public bool   bezierAutoUpdate  = true;
        public List<VerdiepingData>  verdiepingen   = new List<VerdiepingData>();
        public List<PasSeerpuntData> passeerpunten  = new List<PasSeerpuntData>();
        public List<Vector3Data>     bezierControls = new List<Vector3Data>();
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        smoothRoute = GetComponent<SmoothRoute>();
        if (tourCamera == null) tourCamera = Camera.main;

        var lr = GetComponent<LineRenderer>();
        if (lr != null) { lr.startWidth = lijnDikte; lr.endWidth = lijnDikte; }
    }

    void Start()
    {
        // Panel 0 — modus schakelen
        if (knopModusPLY   != null) knopModusPLY  .onClick.AddListener(() => SchakelModus(true));
        if (knopModusDemo  != null) knopModusDemo .onClick.AddListener(() => SchakelModus(false));

        // Panel 0 — demo puntenwolk dropdown vullen
        if (dropdownPuntenwolk != null)
        {
            dropdownPuntenwolk.ClearOptions();
            var opties = new List<string>();
            if (puntenwolken != null)
                foreach (var pw in puntenwolken)
                    if (pw != null) opties.Add(pw.name);
            dropdownPuntenwolk.AddOptions(opties);
        }
        if (knopBevestigPuntenwolk != null) knopBevestigPuntenwolk.onClick.AddListener(BevestigDemoPuntenwolk);

        // Panel 0 — puntenwolk laden
        if (knopKiesPLYMap     != null) knopKiesPLYMap    .onClick.AddListener(KiesPLYMap);
        if (knopLaadPuntenwolk != null) knopLaadPuntenwolk.onClick.AddListener(LaadGekozenPLY);
        if (knopNaarVerdiepingen != null)
        {
            knopNaarVerdiepingen.onClick.AddListener(() => SchakelNaar(panel1Verdiepingen));
            knopNaarVerdiepingen.interactable = false;
        }
        if (knopLaadPuntenwolk != null) knopLaadPuntenwolk.interactable = false;

        // Panel 1
        if (knopVerdiepToevoegen   != null) knopVerdiepToevoegen  .onClick.AddListener(VoegVerdiepingToe);
        if (knopVerdiepVerwijderen != null) knopVerdiepVerwijderen.onClick.AddListener(VerwijderHuidigeVerdieping);
        if (dropdownVerdieping      != null) dropdownVerdieping     .onValueChanged.AddListener(SchakelVerdieping);
        if (dropdownVerdiepingPanel2 != null) dropdownVerdiepingPanel2.onValueChanged.AddListener(SchakelVerdieping);
        if (knopNaar2                  != null) knopNaar2                 .onClick.AddListener(() => SchakelNaar(panel2Punten));
        if (knopTerugNaarPuntenwolk    != null) knopTerugNaarPuntenwolk   .onClick.AddListener(() => SchakelNaar(panel0Puntenwolk));

        // Panel 2
        if (dropdownInterpolatie != null)
        {
            dropdownInterpolatie.ClearOptions();
            dropdownInterpolatie.AddOptions(new List<string> {
                "Lerp", "CatmullRom", "Centripetaal CatmullRom", "Bezier"
            });
            dropdownInterpolatie.onValueChanged.AddListener(WijzigModus);
            dropdownInterpolatie.value = (int)smoothRoute.bewegingType;
        }
        if (knopToevoegen  != null) knopToevoegen .onClick.AddListener(WisselToevoegenModus);
        if (knopVerwijderen != null) knopVerwijderen.onClick.AddListener(VerwijderLaatstePunt);
        if (knopReset       != null) knopReset      .onClick.AddListener(ResetPunten);
        if (knopTerug1      != null) knopTerug1     .onClick.AddListener(() => SchakelNaar(panel1Verdiepingen));


        // Start tour knoppen (Panel 2)
        if (knopStartMetInstellingen != null) knopStartMetInstellingen.onClick.AddListener(StartTourMetInstellingen);
        if (knopStartVolledigScherm  != null) knopStartVolledigScherm .onClick.AddListener(StartTourVolledigScherm);
        // Panel 4
        if (knopSnelheidPlus  != null) knopSnelheidPlus .onClick.AddListener(() => PasSnelheidAan(0.5f));
        if (knopSnelheidMin   != null) knopSnelheidMin  .onClick.AddListener(() => PasSnelheidAan(-0.5f));
        if (knopRotatiePlus   != null) knopRotatiePlus  .onClick.AddListener(() => PasRotatieAan(0.5f));
        if (knopRotatieMin    != null) knopRotatieMin   .onClick.AddListener(() => PasRotatieAan(-0.5f));
        if (knopOoghoogtePlus != null) knopOoghoogtePlus.onClick.AddListener(() => PasOoghoogteAan(0.1f));
        if (knopOoghoogteMin  != null) knopOoghoogteMin .onClick.AddListener(() => PasOoghoogteAan(-0.1f));
        if (knopPuntGrootterMaken != null) knopPuntGrootterMaken.onClick.AddListener(MaakPuntGroter);
        if (knopPuntKleinerMaken  != null) knopPuntKleinerMaken .onClick.AddListener(MaakPuntKleiner);
        if (knopStopTourInstellingen != null) knopStopTourInstellingen.onClick.AddListener(StopTour);

        // Stop tour overlay
        if (knopStopTourVolledig != null) knopStopTourVolledig.onClick.AddListener(StopTour);

        if (previewBol != null) previewBol.SetActive(false);

        // JSON laden
        if (knopKiesLaadMap       != null) knopKiesLaadMap      .onClick.AddListener(KiesLaadMap);
        if (knopLaden             != null) knopLaden            .onClick.AddListener(LaadGeselecteerdeTour);
        if (dropdownJsonBestanden != null) dropdownJsonBestanden.onValueChanged.AddListener(_ => { });
        if (knopLaden             != null) knopLaden.interactable = false;

        // JSON opslaan
        if (knopKiesOpslagMap != null) knopKiesOpslagMap.onClick.AddListener(KiesOpslagMap);
        if (knopOpslaan       != null) knopOpslaan      .onClick.AddListener(OpslaanTour);
        if (knopKiesOpnameMap != null) knopKiesOpnameMap.onClick.AddListener(KiesOpnameMap);

        LeesHuidigeGrootteUitMateriaal();
        BijwerkVerdiepingDropdown();

        // Pas vaste lijndikte toe op SmoothRoute
        smoothRoute.lijnDikte = lijnDikte;
        var lr = smoothRoute.GetComponent<LineRenderer>();
        if (lr != null) { lr.startWidth = lijnDikte; lr.endWidth = lijnDikte; }

        SchakelNaar(panel0Puntenwolk);
        SchakelModus(true);
        BijwerkInstellingenTekst();
    }

    void Update()
    {
        // Escape tijdens volledig scherm tour → toon stop knop
        if (tourLoopt && volledigScherm && Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
                ToonStopTourOverlay();
        }

        // Check of tour klaar is
        if (tourLoopt && !smoothRoute.TourIsActief())
            TourGedaan();

        if (!puntToevoegenActief || tourLoopt)
        {
            if (previewBol != null) previewBol.SetActive(false);
            return;
        }

        if (Mouse.current == null) return;

        Vector2 muisScherm = Mouse.current.position.ReadValue();
        Vector3 muisPos    = GetMusPosOpGrond(muisScherm);

        if (previewBol != null)
        {
            previewBol.SetActive(true);
            previewBol.transform.position = muisPos + Vector3.up * markeringGrootte * 0.5f;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame && !IsMusOpUI() && !sleepActief)
            PlaatsPunt(muisPos, false);

        if (Mouse.current.rightButton.wasPressedThisFrame && !IsMusOpUI())
            PlaatsPunt(muisPos, true);
    }

    void LateUpdate()
    {
        UpdateControlPointSleep();
        if (!tourLoopt) UpdateCameraNavigatie();
        if (frameOpnameActief) StartCoroutine(VastleggenFrame());
    }

    // ── Panel navigatie ───────────────────────────────────────────────────────

    void SchakelNaar(GameObject panel)
    {
        if (panel0Puntenwolk   != null) panel0Puntenwolk  .SetActive(panel == panel0Puntenwolk);
        panel1Verdiepingen.SetActive(panel == panel1Verdiepingen);
        panel2Punten      .SetActive(panel == panel2Punten);
        panel4Instellingen.SetActive(panel == panel4Instellingen);
        panelStopTour     .SetActive(false);

        // Stop plaatsmodus als we van panel 2 weggaan
        if (panel != panel2Punten && puntToevoegenActief)
        {
            puntToevoegenActief = false;
            SchakelTopDown(false);
        }

        BijwerkUI();
    }

    // ── Modus schakelen (PLY / Demo) ──────────────────────────────────────────

    void SchakelModus(bool ply)
    {
        plyModusActief = ply;
        if (subPanelPLY  != null) subPanelPLY .SetActive(ply);
        if (subPanelDemo != null) subPanelDemo.SetActive(!ply);

        // Verberg alle PCX puntenwolken als we naar PLY modus gaan
        if (ply && puntenwolken != null)
            foreach (var pw in puntenwolken)
                if (pw != null) pw.SetActive(false);
    }

    void BevestigDemoPuntenwolk()
    {
        if (puntenwolken == null || puntenwolken.Length == 0)
        {
            SchakelNaar(panel1Verdiepingen);
            return;
        }

        int idx = dropdownPuntenwolk != null ? dropdownPuntenwolk.value : 0;

        for (int i = 0; i < puntenwolken.Length; i++)
            if (puntenwolken[i] != null)
                puntenwolken[i].SetActive(i == idx);

        // Koppel de renderer van de gekozen PCX puntenwolk
        if (puntenwolken[idx] != null)
        {
            var rend = puntenwolken[idx].GetComponent<MeshRenderer>();
            if (rend != null) puntenwolkRenderer = rend;
        }

        // Verberg de runtime PLY als die geladen is
        if (plyLoader != null) plyLoader.gameObject.SetActive(false);

        SchakelNaar(panel1Verdiepingen);
    }

    // ── Puntenwolk laden (Panel 0) ────────────────────────────────────────────

    void KiesPLYMap()
    {
        string map = KiesMapViaDialoog("Kies map met PLY bestanden");
        if (string.IsNullOrEmpty(map)) return;

        plyMap = map;
        gevondenPLY.Clear();
        if (dropdownPLYBestanden != null) dropdownPLYBestanden.ClearOptions();

        var bestanden = Directory.GetFiles(plyMap, "*.ply");
        var opties    = new List<string>();
        foreach (var b in bestanden) { gevondenPLY.Add(b); opties.Add(Path.GetFileNameWithoutExtension(b)); }

        if (dropdownPLYBestanden != null) { dropdownPLYBestanden.AddOptions(opties); dropdownPLYBestanden.interactable = opties.Count > 0; }
        if (knopLaadPuntenwolk   != null) knopLaadPuntenwolk.interactable = opties.Count > 0;
        if (tekstLaadStatusPLY   != null) tekstLaadStatusPLY.text = opties.Count > 0 ? opties.Count + " bestand(en) gevonden" : "Geen PLY bestanden gevonden";
    }

    void LaadGekozenPLY()
    {
        if (plyLoader == null || gevondenPLY.Count == 0) return;
        int idx = dropdownPLYBestanden != null ? dropdownPLYBestanden.value : 0;
        if (idx < 0 || idx >= gevondenPLY.Count) return;

        if (knopNaarVerdiepingen != null) knopNaarVerdiepingen.interactable = false;
        if (tekstLaadStatusPLY   != null) tekstLaadStatusPLY.text = "Laden...";

        plyLoader.LaadPLY(gevondenPLY[idx]);
        StartCoroutine(WachtOpPLYLaden());
    }

    System.Collections.IEnumerator WachtOpPLYLaden()
    {
        while (plyLoader.isAanHetLaden) yield return null;

        if (tekstLaadStatusPLY   != null) tekstLaadStatusPLY.text = "Geladen: " + plyLoader.huidigBestand + " (" + plyLoader.aantalPunten + " punten)";
        if (knopNaarVerdiepingen != null) knopNaarVerdiepingen.interactable = true;

        // Koppel de PLY renderer als actieve puntenwolkRenderer
        if (plyLoader != null)
        {
            plyLoader.gameObject.SetActive(true);
            var rend = plyLoader.GetComponent<MeshRenderer>();
            if (rend != null) puntenwolkRenderer = rend;
        }

        // Verberg alle PCX demo puntenwolken
        if (puntenwolken != null)
            foreach (var pw in puntenwolken)
                if (pw != null) pw.SetActive(false);
    }

    // ── Verdiepingen ──────────────────────────────────────────────────────────

    void VoegVerdiepingToe()
    {
        if (invoerHoogte == null) return;

        string invoerTekst = invoerHoogte.text.Replace(',', '.');
        if (!float.TryParse(invoerTekst,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out float hoogte))
        {
            if (tekstStatus != null) tekstStatus.text = "Ongeldige hoogte.";
            return;
        }

        string naam = invoerVerdiepNaam != null && invoerVerdiepNaam.text.Length > 0
            ? invoerVerdiepNaam.text
            : "Verdieping " + (verdiepingen.Count + 1);

        verdiepingen.Add(new Verdieping(naam, hoogte));
        huidigeVerdiepingIdx = verdiepingen.Count - 1;

        if (invoerHoogte       != null) invoerHoogte.text       = "";
        if (invoerVerdiepNaam  != null) invoerVerdiepNaam.text  = "";

        BijwerkVerdiepingDropdown();
        PasGrondvlakAan();
        BijwerkUI();
    }

    void VerwijderHuidigeVerdieping()
    {
        if (verdiepingen.Count == 0 || huidigeVerdiepingIdx < 0) return;

        verdiepingen.RemoveAt(huidigeVerdiepingIdx);
        huidigeVerdiepingIdx = Mathf.Clamp(huidigeVerdiepingIdx, 0, verdiepingen.Count - 1);
        if (verdiepingen.Count == 0) huidigeVerdiepingIdx = -1;

        BijwerkVerdiepingDropdown();
        PasGrondvlakAan();
        BijwerkUI();
    }

    void SchakelVerdieping(int index)
    {
        huidigeVerdiepingIdx = index;
        PasGrondvlakAan();
        if (puntToevoegenActief) SchakelTopDown(true);
        BijwerkUI();
    }

    void PasGrondvlakAan()
    {
        float hoogte = HuidigeHoogte;
        grondVlak = new Plane(Vector3.up, Vector3.up * hoogte);
        if (tourCamera != null && puntToevoegenActief)
            PasClippingOpHoogte(hoogte);
    }

    void BijwerkVerdiepingDropdown()
    {
        var dropdowns = new List<TMP_Dropdown>();
        if (dropdownVerdieping      != null) dropdowns.Add(dropdownVerdieping);
        if (dropdownVerdiepingPanel2 != null) dropdowns.Add(dropdownVerdiepingPanel2);

        foreach (var dd in dropdowns)
        {
            dd.ClearOptions();
            if (verdiepingen.Count == 0)
            {
                dd.AddOptions(new List<string> { "Geen verdiepingen" });
                dd.interactable = false;
            }
            else
            {
                var opties = new List<string>();
                foreach (var v in verdiepingen)
                    opties.Add(v.naam + "  (Y=" + v.hoogte.ToString("F2") + "m)");
                dd.AddOptions(opties);
                dd.interactable = true;
                dd.SetValueWithoutNotify(
                    Mathf.Clamp(huidigeVerdiepingIdx, 0, verdiepingen.Count - 1));
            }
        }
    }

    // ── Tour starten/stoppen ──────────────────────────────────────────────────

    void StartTourMetInstellingen()
    {
        if ((smoothRoute.passeerpunten?.Length ?? 0) < 2)
        {
            if (tekstStatus != null) tekstStatus.text = "Minimum 2 punten nodig.";
            SchakelNaar(panel2Punten);
            return;
        }

        volledigScherm      = false;
        puntToevoegenActief = false;
        SchakelTopDown(false);

        if (tourCamera != null)
        {
            tourCamera.nearClipPlane = defaultNearClip;
            tourCamera.farClipPlane  = 99999f;
        }

        // Verberg markeringen
        foreach (var m in markeringen) if (m != null) m.SetActive(false);
        VerbergControlPoints();

        var lr = smoothRoute.GetComponent<LineRenderer>();
        if (lr != null) lr.enabled = false;

        tourLoopt = true;
        smoothRoute.HerstartTour();

        // Toon panel 4 met instellingen
        panel1Verdiepingen.SetActive(false);
        panel2Punten      .SetActive(false);
        panel4Instellingen.SetActive(true);
        panelStopTour     .SetActive(false);
    }

    void StartTourVolledigScherm()
    {
        if ((smoothRoute.passeerpunten?.Length ?? 0) < 2)
        {
            if (tekstStatus != null) tekstStatus.text = "Minimum 2 punten nodig.";
            SchakelNaar(panel2Punten);
            return;
        }

        volledigScherm      = true;
        puntToevoegenActief = false;
        SchakelTopDown(false);

        if (tourCamera != null)
        {
            tourCamera.nearClipPlane = defaultNearClip;
            tourCamera.farClipPlane  = 99999f;
        }

        // Verberg markeringen
        foreach (var m in markeringen) if (m != null) m.SetActive(false);
        VerbergControlPoints();

        var lr = smoothRoute.GetComponent<LineRenderer>();
        if (lr != null) lr.enabled = false;

        tourLoopt = true;
        smoothRoute.HerstartTour();

        // Start opname
        StartOpname();

        // Verberg alle UI
        panel1Verdiepingen.SetActive(false);
        panel2Punten      .SetActive(false);
        panel4Instellingen.SetActive(false);
        panelStopTour     .SetActive(false);
    }

    void TourGedaan()
    {
        tourLoopt = false;
        smoothRoute.StopTour();
        StopOpname();

        if (volledigScherm)
            ToonStopTourOverlay();
        else
            StopTour();
    }

    void ToonStopTourOverlay()
    {
        panel1Verdiepingen.SetActive(false);
        panel2Punten      .SetActive(false);
        panel4Instellingen.SetActive(false);
        panelStopTour     .SetActive(true);
    }

    void StopTour()
    {
        tourLoopt      = false;
        volledigScherm = false;
        smoothRoute.StopTour();
        StopOpname();

        // Toon markeringen terug
        foreach (var m in markeringen) if (m != null) m.SetActive(true);

        var lr = smoothRoute.GetComponent<LineRenderer>();
        if (lr != null) lr.enabled = true;

        if (smoothRoute.bewegingType == BewegingType.Bezier)
            MaakControlPointBollen();

        SchakelNaar(panel2Punten);
    }

    // ── Instellingen aanpassen ────────────────────────────────────────────────

    void PasSnelheidAan(float delta)
    {
        smoothRoute.bewegingsSnelheid = Mathf.Max(0.1f, smoothRoute.bewegingsSnelheid + delta);
        BijwerkInstellingenTekst();
    }

    void PasRotatieAan(float delta)
    {
        smoothRoute.rotatieSnelheid = Mathf.Max(0.1f, smoothRoute.rotatieSnelheid + delta);
        BijwerkInstellingenTekst();
    }

    void PasOoghoogteAan(float delta)
    {
        smoothRoute.ooghoogte = Mathf.Max(0f, smoothRoute.ooghoogte + delta);
        BijwerkInstellingenTekst();
    }

    void MaakPuntGroter()  { PasPuntGrootteToe(huidigeGrootte + 1f); }
    void MaakPuntKleiner() { PasPuntGrootteToe(huidigeGrootte - 1f); }

    void PasPuntGrootteToe(float waarde)
    {
        huidigeGrootte = Mathf.Clamp(waarde, 1f, 30f);

        // Vaste puntenwolk renderer (PCX)
        if (puntenwolkRenderer != null)
            foreach (var mat in puntenwolkRenderer.materials)
                if (mat.HasProperty("_PointSize"))
                    mat.SetFloat("_PointSize", huidigeGrootte);

        // Runtime PLY loader — ook alle child chunks updaten
        if (plyLoader != null)
        {
            var renderers = plyLoader.GetComponentsInChildren<MeshRenderer>();
            foreach (var rend in renderers)
                foreach (var mat in rend.materials)
                    if (mat.HasProperty("_PointSize"))
                        mat.SetFloat("_PointSize", huidigeGrootte);
        }

        BijwerkInstellingenTekst();
    }

    void LeesHuidigeGrootteUitMateriaal()
    {
        if (puntenwolkRenderer == null) return;
        foreach (var mat in puntenwolkRenderer.materials)
        {
            if (mat.HasProperty("_PointSize"))
            {
                huidigeGrootte = mat.GetFloat("_PointSize");
                return;
            }
        }
    }

    void BijwerkInstellingenTekst()
    {
        if (tekstSnelheid    != null) tekstSnelheid   .text = "Snelheid: "        + smoothRoute.bewegingsSnelheid.ToString("F1");
        if (tekstRotatie     != null) tekstRotatie    .text = "Rotatiesnelheid: " + smoothRoute.rotatieSnelheid  .ToString("F1");
        if (tekstOoghoogte   != null) tekstOoghoogte  .text = "Ooghoogte: "       + smoothRoute.ooghoogte        .ToString("F1") + "m";
        if (tekstPuntGrootte != null) tekstPuntGrootte.text = "Puntgrootte: "     + huidigeGrootte               .ToString("F0");
    }

    // ── Punt plaatsen ─────────────────────────────────────────────────────────

    void WisselToevoegenModus()
    {
        puntToevoegenActief = !puntToevoegenActief;
        if (puntToevoegenActief) SchakelTopDown(true);
        if (verbergBijPuntenPlaatsen != null)
            foreach (var obj in verbergBijPuntenPlaatsen)
                if (obj != null) obj.SetActive(!puntToevoegenActief);
        BijwerkUI();
    }

    void VerwijderLaatstePunt()
    {
        if (smoothRoute.passeerpunten == null || smoothRoute.passeerpunten.Length == 0) return;

        if (markeringen.Count > 0)
        {
            Destroy(markeringen[markeringen.Count - 1]);
            markeringen.RemoveAt(markeringen.Count - 1);
        }

        var laatste = smoothRoute.passeerpunten[smoothRoute.passeerpunten.Length - 1];
        if (laatste?.location != null) Destroy(laatste.location.gameObject);

        System.Array.Resize(ref smoothRoute.passeerpunten, smoothRoute.passeerpunten.Length - 1);
        smoothRoute.BerekenBezierControls();
        BijwerkUI();
    }

    void ResetPunten()
    {
        puntToevoegenActief = false;
        SchakelTopDown(false);

        foreach (var m in markeringen) if (m != null) Destroy(m);
        markeringen.Clear();

        if (smoothRoute.passeerpunten != null)
            foreach (var punt in smoothRoute.passeerpunten)
                if (punt?.location != null) Destroy(punt.location.gameObject);

        smoothRoute.passeerpunten  = new Passeerpunt[0];
        smoothRoute.bezierControls = new Vector3[0];
        cameraOpgeslagen           = false;
        VerbergControlPoints();
        BijwerkUI();
    }

    void PlaatsPunt(Vector3 positie, bool looptDood = false)
    {
        positie.y = HuidigeHoogte;
        smoothRoute.VoegPuntToe(positie, looptDood);

        GameObject bol = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bol.transform.position   = positie + Vector3.up * markeringGrootte * 0.5f;
        bol.transform.localScale = Vector3.one * markeringGrootte;
        bol.transform.SetParent(transform);
        Destroy(bol.GetComponent<SphereCollider>());

        var rend = bol.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material       = new Material(Shader.Find("Unlit/Color"));
            rend.material.color = looptDood ? Color.magenta : markeringKleur;
        }

        markeringen.Add(bol);
        smoothRoute.TekenCurve();

        if (smoothRoute.bewegingType == BewegingType.Bezier)
            MaakControlPointBollen();

        BijwerkUI();
    }

    void WijzigModus(int index)
    {
        smoothRoute.bewegingType = (BewegingType)index;
        smoothRoute.BerekenBezierControls();

        var lr = smoothRoute.GetComponent<LineRenderer>();
        if (lr != null) lr.enabled = true;
        smoothRoute.TekenCurve();

        VerbergControlPoints();
        if ((BewegingType)index == BewegingType.Bezier && !tourLoopt)
            MaakControlPointBollen();

        BijwerkUI();
    }

    // ── Camera top-down ───────────────────────────────────────────────────────

    void SchakelTopDown(bool aan)
    {
        if (tourCamera == null) return;

        if (aan)
        {
            if (!cameraOpgeslagen)
            {
                cameraStartPos   = tourCamera.transform.position;
                cameraStartRot   = tourCamera.transform.rotation;
                cameraStartNear  = tourCamera.nearClipPlane;
                cameraStartFar   = tourCamera.farClipPlane;
                cameraOpgeslagen = true;
            }

            Vector3 centrum = BerekenCentrum();
            float   hoogte  = HuidigeHoogte;

            tourCamera.transform.position = new Vector3(centrum.x, hoogte + topDownHoogte, centrum.z);
            tourCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            PasClippingOpHoogte(hoogte);
        }
        else
        {
            if (cameraOpgeslagen)
            {
                tourCamera.transform.position = cameraStartPos;
                tourCamera.transform.rotation = cameraStartRot;
                tourCamera.nearClipPlane      = cameraStartNear;
                tourCamera.farClipPlane       = cameraStartFar;
            }
        }
    }

    void PasClippingOpHoogte(float hoogte)
    {
        if (tourCamera == null) return;
        float cameraY  = hoogte + topDownHoogte;
        float snijvlak = hoogte + 2f;
        float nearClip = cameraY - snijvlak;
        float farClip  = cameraY - hoogte + 1f;
        tourCamera.nearClipPlane = Mathf.Max(nearClip, 0.01f);
        tourCamera.farClipPlane  = Mathf.Max(farClip, tourCamera.nearClipPlane + 1f);
    }

    Vector3 BerekenCentrum()
    {
        if (smoothRoute.passeerpunten == null || smoothRoute.passeerpunten.Length == 0)
            return Vector3.zero;

        Vector3 som = Vector3.zero;
        int     tel = 0;

        foreach (var punt in smoothRoute.passeerpunten)
        {
            if (punt?.location == null) continue;
            som += punt.location.position;
            tel++;
        }

        return tel > 0 ? som / tel : Vector3.zero;
    }

    // ── Camera navigatie ──────────────────────────────────────────────────────

    void UpdateCameraNavigatie()
    {
        if (tourCamera == null || Mouse.current == null) return;

        if (Mouse.current.middleButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            tourCamera.transform.position +=
                (-tourCamera.transform.right * delta.x
                - tourCamera.transform.up    * delta.y) * 0.1f;
        }

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
            tourCamera.transform.position += tourCamera.transform.forward * scroll * 0.5f;

        if (Mouse.current.rightButton.isPressed && !sleepActief && !puntToevoegenActief)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            tourCamera.transform.RotateAround(tourCamera.transform.position, Vector3.up,          delta.x * 0.2f);
            tourCamera.transform.RotateAround(tourCamera.transform.position, tourCamera.transform.right, -delta.y * 0.2f);
        }
    }

    // ── Muispositie ───────────────────────────────────────────────────────────

    Vector3 GetMusPosOpGrond(Vector2 schermPositie)
    {
        Ray   ray     = tourCamera.ScreenPointToRay(schermPositie);
        float afstand;
        if (grondVlak.Raycast(ray, out afstand)) return ray.GetPoint(afstand);
        return Vector3.zero;
    }

    bool IsMusOpUI()
    {
        return UnityEngine.EventSystems.EventSystem.current != null &&
               UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }

    // ── Bezier control points ─────────────────────────────────────────────────

    void MaakControlPointBollen()
    {
        VerbergControlPoints();

        if (smoothRoute.bezierControls == null || smoothRoute.bezierControls.Length == 0)
            smoothRoute.BerekenBezierControls();

        if (smoothRoute.bezierControls == null) return;

        for (int i = 0; i < smoothRoute.bezierControls.Length; i++)
        {
            GameObject bol = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bol.transform.position   = smoothRoute.bezierControls[i];
            bol.transform.localScale = Vector3.one * 0.3f;
            bol.name = "ControlPoint_" + i;

            var rend = bol.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material       = new Material(Shader.Find("Unlit/Color"));
                rend.material.color = new Color(0.3f, 0.6f, 1f);
            }

            Destroy(bol.GetComponent<SphereCollider>());
            bol.AddComponent<SphereCollider>();
            controlPointBollen.Add(bol);
        }
    }

    void VerbergControlPoints()
    {
        foreach (var bol in controlPointBollen) if (bol != null) Destroy(bol);
        controlPointBollen.Clear();
        gesleeptControlIdx = -1;
        gesleeptBol        = null;
        sleepActief        = false;
    }

    void UpdateControlPointSleep()
    {
        if (smoothRoute.bewegingType != BewegingType.Bezier) return;
        if (tourLoopt || puntToevoegenActief) return;
        if (Mouse.current == null) return;

        Vector2 muisScherm = Mouse.current.position.ReadValue();
        Ray ray = tourCamera.ScreenPointToRay(muisScherm);

        if (Mouse.current.leftButton.wasPressedThisFrame && !IsMusOpUI())
        {
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 1000f))
            {
                for (int i = 0; i < controlPointBollen.Count; i++)
                {
                    if (controlPointBollen[i] != null &&
                        controlPointBollen[i] == hit.collider.gameObject)
                    {
                        sleepActief        = true;
                        gesleeptControlIdx = i;
                        gesleeptBol        = controlPointBollen[i];
                        break;
                    }
                }
            }
        }

        if (sleepActief && Mouse.current.leftButton.isPressed && gesleeptBol != null)
        {
            float hoogte = gesleeptBol.transform.position.y;
            Plane vlak   = new Plane(Vector3.up, Vector3.up * hoogte);
            float afstand;

            if (vlak.Raycast(ray, out afstand))
            {
                Vector3 nieuwePos = ray.GetPoint(afstand);
                gesleeptBol.transform.position                = nieuwePos;
                smoothRoute.bezierControls[gesleeptControlIdx] = nieuwePos;
                smoothRoute.bezierAutoUpdate                  = false;

                int gespiegeldIdx = -1;
                int ankerpuntIdx  = -1;
                List<int> route   = smoothRoute.BouwGizmoRoute();

                if (gesleeptControlIdx % 2 == 0)
                {
                    int seg = gesleeptControlIdx / 2;
                    ankerpuntIdx  = seg;
                    gespiegeldIdx = (seg - 1) * 2 + 1;
                }
                else
                {
                    int seg = gesleeptControlIdx / 2;
                    ankerpuntIdx  = seg + 1;
                    gespiegeldIdx = (seg + 1) * 2;
                }

                if (gespiegeldIdx >= 0 &&
                    gespiegeldIdx < smoothRoute.bezierControls.Length &&
                    ankerpuntIdx  >= 0 &&
                    ankerpuntIdx  < route.Count &&
                    smoothRoute.IsGeldigPunt(route[ankerpuntIdx]))
                {
                    Vector3 anker     = smoothRoute.GetPuntPos(route[ankerpuntIdx]);
                    Vector3 richting  = (nieuwePos - anker).normalized;
                    float   afstandCP = Vector3.Distance(anker, smoothRoute.bezierControls[gespiegeldIdx]);
                    Vector3 gespiegeld = anker - richting * afstandCP;
                    smoothRoute.bezierControls[gespiegeldIdx] = gespiegeld;

                    if (gespiegeldIdx < controlPointBollen.Count && controlPointBollen[gespiegeldIdx] != null)
                        controlPointBollen[gespiegeldIdx].transform.position = gespiegeld;
                }

                smoothRoute.TekenCurve();
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            sleepActief        = false;
            gesleeptControlIdx = -1;
            gesleeptBol        = null;
        }
    }

    public void SchakelNaarPanel1() { SchakelNaar(panel1Verdiepingen); }

    // ── Publieke methodes voor TourOpslag ───────────────────────────────────────

    public float HuidigeGrootte() { return huidigeGrootte; }

    public List<Verdieping> GetVerdiepingen() { return verdiepingen; }

    public void ResetPuntenPubliek() { ResetPunten(); }

    public void PasPuntGroottePubliek(float grootte) { PasPuntGrootteToe(grootte); }

    public void LaadVerdiepingen(List<VerdiepingData> data)
    {
        verdiepingen.Clear();
        huidigeVerdiepingIdx = -1;

        foreach (var vd in data)
            verdiepingen.Add(new Verdieping(vd.naam, vd.hoogte));

        if (verdiepingen.Count > 0)
            huidigeVerdiepingIdx = 0;

        BijwerkVerdiepingDropdown();
        PasGrondvlakAan();
    }

    public void PlaatsPuntPubliek(Vector3 positie, bool looptDood, float doorloopsnelheid)
    {
        smoothRoute.VoegPuntToe(positie, looptDood);

        // Pas doorloopsnelheid aan op het laatste punt
        if (smoothRoute.passeerpunten != null && smoothRoute.passeerpunten.Length > 0)
            smoothRoute.passeerpunten[smoothRoute.passeerpunten.Length - 1].doorloopsnelheid = doorloopsnelheid;

        // Markering
        GameObject bol = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bol.transform.position   = positie + Vector3.up * markeringGrootte * 0.5f;
        bol.transform.localScale = Vector3.one * markeringGrootte;
        bol.transform.SetParent(transform);
        Destroy(bol.GetComponent<SphereCollider>());

        var rend = bol.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material       = new Material(Shader.Find("Unlit/Color"));
            rend.material.color = looptDood ? Color.magenta : markeringKleur;
        }

        markeringen.Add(bol);
    }

    // ── JSON opslaan ─────────────────────────────────────────────────────────────

    void KiesOpslagMap()
    {
        string map = KiesMapViaDialoog("Kies opslagmap");
        if (string.IsNullOrEmpty(map)) return;
        opslagMap = map;
        if (tekstOpslagStatus != null)
            tekstOpslagStatus.text = "Map: " + Path.GetFileName(map);
    }

    void OpslaanTour()
    {
        if (string.IsNullOrEmpty(opslagMap))
        {
            opslagMap = Path.Combine(Application.persistentDataPath, "Tours");
            Directory.CreateDirectory(opslagMap);
        }

        string naam = invoerTourNaam != null && invoerTourNaam.text.Length > 0
            ? invoerTourNaam.text
            : "Tour_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm");

        TourData data = VerzamelData(naam);
        string json   = JsonUtility.ToJson(data, true);
        string pad    = Path.Combine(opslagMap, naam + ".json");
        Directory.CreateDirectory(opslagMap);
        File.WriteAllText(pad, json);

        if (tekstOpslagStatus != null)
            tekstOpslagStatus.text = "Opgeslagen: " + naam + ".json";

        if (opslagMap == laadMap) LaadJsonLijst();
    }

    TourData VerzamelData(string naam)
    {
        TourData data          = new TourData();
        data.naam              = naam;
        data.datum             = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        data.bewegingsSnelheid = smoothRoute.bewegingsSnelheid;
        data.rotatieSnelheid   = smoothRoute.rotatieSnelheid;
        data.ooghoogte         = smoothRoute.ooghoogte;
        data.interpolatieModus = (int)smoothRoute.bewegingType;
        data.bezierAutoUpdate  = smoothRoute.bezierAutoUpdate;
        data.puntGrootte       = huidigeGrootte;

        if (smoothRoute.bezierControls != null)
            foreach (var c in smoothRoute.bezierControls)
                data.bezierControls.Add(new Vector3Data(c));

        if (smoothRoute.passeerpunten != null)
            foreach (var punt in smoothRoute.passeerpunten)
            {
                if (punt?.location == null) continue;
                data.passeerpunten.Add(new PasSeerpuntData
                {
                    x                = punt.location.position.x,
                    y                = punt.location.position.y,
                    z                = punt.location.position.z,
                    looptDood        = punt.looptDood,
                    doorloopsnelheid = punt.doorloopsnelheid
                });
            }

        foreach (var v in verdiepingen)
            data.verdiepingen.Add(new VerdiepingData { naam = v.naam, hoogte = v.hoogte });

        return data;
    }

    // ── JSON laden ────────────────────────────────────────────────────────────────

    void KiesLaadMap()
    {
        string map = KiesMapViaDialoog("Kies map met JSON tourbestanden");
        if (string.IsNullOrEmpty(map)) return;
        laadMap = map;
        LaadJsonLijst();
    }

    void LaadJsonLijst()
    {
        gevondenBestanden.Clear();

        if (!Directory.Exists(laadMap)) return;

        string[] bestanden = Directory.GetFiles(laadMap, "*.json");

        if (dropdownJsonBestanden != null)
        {
            dropdownJsonBestanden.ClearOptions();
            if (bestanden.Length == 0)
            {
                dropdownJsonBestanden.AddOptions(new List<string> { "Geen bestanden" });
                dropdownJsonBestanden.interactable = false;
                if (knopLaden != null) knopLaden.interactable = false;
                if (tekstLaadStatus != null) tekstLaadStatus.text = "Geen JSON bestanden gevonden.";
                return;
            }

            var namen = new List<string>();
            foreach (string b in bestanden)
            {
                gevondenBestanden.Add(b);
                namen.Add(Path.GetFileNameWithoutExtension(b));
            }

            dropdownJsonBestanden.AddOptions(namen);
            dropdownJsonBestanden.interactable = true;
            if (knopLaden != null) knopLaden.interactable = true;
            if (tekstLaadStatus != null) tekstLaadStatus.text = bestanden.Length + " tour(s) gevonden.";
        }
    }

    void LaadGeselecteerdeTour()
    {
        if (gevondenBestanden.Count == 0) return;
        int index = dropdownJsonBestanden != null ? dropdownJsonBestanden.value : 0;
        if (index >= gevondenBestanden.Count) return;

        string json   = File.ReadAllText(gevondenBestanden[index]);
        TourData data = JsonUtility.FromJson<TourData>(json);
        PasDataToe(data);

        if (tekstLaadStatus != null) tekstLaadStatus.text = "Geladen: " + data.naam;
    }

    void PasDataToe(TourData data)
    {
        ResetPunten();

        smoothRoute.bewegingsSnelheid = data.bewegingsSnelheid;
        smoothRoute.rotatieSnelheid   = data.rotatieSnelheid;
        smoothRoute.ooghoogte         = data.ooghoogte;
        smoothRoute.bewegingType      = (BewegingType)data.interpolatieModus;
        smoothRoute.bezierAutoUpdate  = data.bezierAutoUpdate;

        PasPuntGrootteToe(data.puntGrootte);

        // Verdiepingen laden
        verdiepingen.Clear();
        huidigeVerdiepingIdx = -1;
        foreach (var vd in data.verdiepingen)
            verdiepingen.Add(new Verdieping(vd.naam, vd.hoogte));
        if (verdiepingen.Count > 0) huidigeVerdiepingIdx = 0;
        BijwerkVerdiepingDropdown();
        PasGrondvlakAan();

        // Passeerpunten laden
        foreach (var pd in data.passeerpunten)
        {
            Vector3 pos = new Vector3(pd.x, pd.y, pd.z);
            smoothRoute.VoegPuntToe(pos, pd.looptDood);
            if (smoothRoute.passeerpunten != null && smoothRoute.passeerpunten.Length > 0)
                smoothRoute.passeerpunten[smoothRoute.passeerpunten.Length - 1].doorloopsnelheid = pd.doorloopsnelheid;

            GameObject bol = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bol.transform.position   = pos + Vector3.up * markeringGrootte * 0.5f;
            bol.transform.localScale = Vector3.one * markeringGrootte;
            bol.transform.SetParent(transform);
            Destroy(bol.GetComponent<SphereCollider>());
            var rend = bol.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material       = new Material(Shader.Find("Unlit/Color"));
                rend.material.color = pd.looptDood ? Color.magenta : markeringKleur;
            }
            markeringen.Add(bol);
        }

        // Bezier controls laden
        if (data.bezierControls.Count > 0)
        {
            smoothRoute.bezierControls = new Vector3[data.bezierControls.Count];
            for (int i = 0; i < data.bezierControls.Count; i++)
                smoothRoute.bezierControls[i] = data.bezierControls[i].ToVector3();
        }

        smoothRoute.TekenCurve();
        BijwerkInstellingenTekst();
        BijwerkUI();
    }

    // ── Windows map dialoog ───────────────────────────────────────────────────────

    string KiesMapViaDialoog(string titel)
    {
        string resultaat = "";
        var thread = new System.Threading.Thread(() =>
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            dlg.Description         = titel;
            dlg.ShowNewFolderButton = true;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                resultaat = dlg.SelectedPath;
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (!string.IsNullOrEmpty(resultaat) && resultaat.Length > 1)
            return resultaat;
        return "";
    }

    // ── UI bijwerken ──────────────────────────────────────────────────────────

    void BijwerkUI()
    {
        int aantalPunten = smoothRoute.passeerpunten?.Length ?? 0;

        if (tekstStatus != null)
        {
            if (puntToevoegenActief && huidigeVerdiepingIdx >= 0)
                tekstStatus.text = "Punten: " + aantalPunten + " — Klik op de vloer (" + verdiepingen[huidigeVerdiepingIdx].naam + ")";
            else if (puntToevoegenActief)
                tekstStatus.text = "Punten: " + aantalPunten + " — Voeg eerst een verdieping toe";
            else
                tekstStatus.text = "Punten: " + aantalPunten;
        }

        string hoogteTekst = huidigeVerdiepingIdx >= 0 && huidigeVerdiepingIdx < verdiepingen.Count
            ? verdiepingen[huidigeVerdiepingIdx].naam + "  Y = " + HuidigeHoogte.ToString("F2") + "m"
            : "Geen verdieping geselecteerd";

        if (tekstHoogte       != null) tekstHoogte      .text = hoogteTekst;
        if (tekstHoogtePanel2 != null) tekstHoogtePanel2.text = hoogteTekst;

        if (knopToevoegen != null)
        {
            var t = knopToevoegen.GetComponentInChildren<TMP_Text>();
            if (t != null) t.text = puntToevoegenActief ? "Stop plaatsen" : "+ Punt toevoegen";
        }

        if (knopVerwijderen != null) knopVerwijderen.interactable = aantalPunten > 0;
        if (knopReset       != null) knopReset      .interactable = aantalPunten > 0;

        if (knopStartMetInstellingen != null) knopStartMetInstellingen.interactable = aantalPunten >= 2;
        if (knopStartVolledigScherm  != null) knopStartVolledigScherm .interactable = aantalPunten >= 2;

        if (knopVerdiepVerwijderen != null) knopVerdiepVerwijderen.interactable = verdiepingen.Count > 0;

        BijwerkInstellingenTekst();
    }

    // ── Opname ────────────────────────────────────────────────────────────────

    void KiesOpnameMap()
    {
        string map = KiesMapViaDialoog("Kies map voor video opname");
        if (string.IsNullOrEmpty(map)) return;
        opnameMap = map;
        if (tekstOpnameMap != null) tekstOpnameMap.text = "Map: " + System.IO.Path.GetFileName(map);
        Debug.Log("Opname map: " + opnameMap);
    }

    void StartOpname()
    {
        if (invoerOpnameNaam != null && !string.IsNullOrEmpty(invoerOpnameNaam.text))
            opnameNaam = invoerOpnameNaam.text.Trim();

        if (string.IsNullOrEmpty(opnameMap))
            opnameMap = Application.persistentDataPath + "/Recordings";

        frameOpnameMap = Path.Combine(opnameMap, opnameNaam + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(frameOpnameMap);

        frameIndex        = 0;
        frameOpnameActief = true;

        Application.targetFrameRate = opnameFramerate;
        Time.captureFramerate       = opnameFramerate;

        Debug.Log("Frame opname gestart in: " + frameOpnameMap);
    }

    void StopOpname()
    {
        if (!frameOpnameActief) return;

        frameOpnameActief           = false;
        Time.captureFramerate       = 0;
        Application.targetFrameRate = -1;

        Debug.Log($"Opname gestopt — {frameIndex} frames opgeslagen in: {frameOpnameMap}");
    }

    System.Collections.IEnumerator VastleggenFrame()
    {
        yield return new WaitForEndOfFrame();
        if (!frameOpnameActief) yield break;
        string pad = Path.Combine(frameOpnameMap, $"frame_{frameIndex:D6}.png");
        ScreenCapture.CaptureScreenshot(pad);
        frameIndex++;
    }
}
