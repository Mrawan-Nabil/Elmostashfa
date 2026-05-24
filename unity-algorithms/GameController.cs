using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameController : MonoBehaviour
{
    // ─── Patient data ────────────────────────────────────────────────────────
    private class PatientInfo
    {
        public int id;
        public int priority;       // 1 (lowest) → 5 (highest)
        public string targetRoom;
        public float waitStartTime;
        public bool isWaiting;     // spawned and in transit to room
        public bool isInRoom;
        public bool isDone;
    }

    // ─── Inspector ───────────────────────────────────────────────────────────
    [Header("Config")]
    public PathManager pathManager;
    public CharacterMover characterPrefab;

    [Header("UI – Input")]
    public TMP_InputField inputPatientCount;
    public Button btnLevel1;
    public Button btnLevel2;
    public Button btnLevel3;
    [Tooltip("Optional: hides when simulation starts")]
    public GameObject inputPanel;

    [Header("Config (fallback defaults)")]
    public int defaultPatients = 10;

    [Header("Spawn Points (4 side points)")]
    public Transform spawnPointA;
    public Transform spawnPointB;
    public Transform spawnPointC;
    public Transform spawnPointD;

    [Header("Main Path Merge Point")]
    [Tooltip("The single main-path node that all 4 spawn points feed into")]
    public Transform mainMergePoint;

    [Header("Out Room")]
    [Tooltip("Room name in PathManager used as exit destination")]
    public string outRoomName = "out";

    [Header("UI – Stats")]
    public TextMeshProUGUI txtPatientsNumber;
    public TextMeshProUGUI txtRoomsNumber;
    public TextMeshProUGUI txtFreeRooms;
    public TextMeshProUGUI txtBusyRooms;
    public TextMeshProUGUI txtPatientsWaiting;
    public TextMeshProUGUI txtAvgWaitTime;
    public TextMeshProUGUI txtSurgeriesNumber;
    public TextMeshProUGUI txtMriScans;

    [Header("UI – Queue")]
    [Tooltip("Shows next 4 patients: ID | Severity | Room")]
    public TextMeshProUGUI txtPatientsQueue;

    // ─── Priority colours (index = priority - 1) ─────────────────────────────
    private static readonly Color[] PriorityColors =
    {
        new Color(0.20f, 0.85f, 0.20f),  // 1 – green
        new Color(0.60f, 1.00f, 0.20f),  // 2 – lime
        new Color(1.00f, 0.90f, 0.00f),  // 3 – yellow
        new Color(1.00f, 0.50f, 0.00f),  // 4 – orange
        new Color(1.00f, 0.10f, 0.10f),  // 5 – red
    };

    // ─── Runtime state ────────────────────────────────────────────────────────
    private int level;
    private int numberOfPatients;

    private Transform[] spawnPoints;
    private bool[] spawnOccupied;
    private List<PatientInfo> pendingList;   // sorted highest priority first
    private List<PatientInfo> allPatients;

    private HashSet<string> occupiedRooms = new HashSet<string>();
    private float totalWaitTime;
    private int servedCount;
    private int surgeriesCount;
    private int mriCount;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        spawnPoints = new[] { spawnPointA, spawnPointB, spawnPointC, spawnPointD };
        spawnOccupied = new bool[4];

        if (btnLevel1) btnLevel1.onClick.AddListener(() => StartSimulation(1));
        if (btnLevel2) btnLevel2.onClick.AddListener(() => StartSimulation(2));
        if (btnLevel3) btnLevel3.onClick.AddListener(() => StartSimulation(3));

        if (inputPanel) inputPanel.SetActive(true);
    }

    public void StartSimulation(int selectedLevel)
    {
        if (allPatients != null) return; // already running

        level = selectedLevel;

        // Read patient count from input field; fall back to default
        numberOfPatients = defaultPatients;
        if (inputPatientCount != null && !string.IsNullOrWhiteSpace(inputPatientCount.text))
        {
            if (int.TryParse(inputPatientCount.text, out int parsed) && parsed > 0)
                numberOfPatients = parsed;
        }

        // Disable buttons so the user can't restart mid-simulation
        if (btnLevel1) btnLevel1.interactable = false;
        if (btnLevel2) btnLevel2.interactable = false;
        if (btnLevel3) btnLevel3.interactable = false;
        if (inputPatientCount != null) inputPatientCount.interactable = false;
        if (inputPanel) inputPanel.SetActive(false);

        allPatients = BuildPatientList();
        pendingList = new List<PatientInfo>(allPatients);

        StartCoroutine(SpawnLoop());
        StartCoroutine(UILoop());
    }

    // ─── Patient generation ──────────────────────────────────────────────────
    List<PatientInfo> BuildPatientList()
    {
        float dangerRatio = level == 1 ? 0.25f : level == 2 ? 0.50f : 0.75f;
        int dangerCount = Mathf.RoundToInt(numberOfPatients * dangerRatio);
        int normalCount = numberOfPatients - dangerCount;

        var list = new List<PatientInfo>();
        int id = 1;

        // Dangerous batch: alternate priority 5 / 4
        for (int i = 0; i < dangerCount; i++)
        {
            int pri = (i % 2 == 0) ? 5 : 4;
            list.Add(new PatientInfo { id = id++, priority = pri });
        }

        // Normal batch: cycle priority 3 → 2 → 1
        for (int i = 0; i < normalCount; i++)
        {
            int pri = 3 - (i % 3);
            list.Add(new PatientInfo { id = id++, priority = pri });
        }

        // Highest priority first
        list.Sort((a, b) => b.priority.CompareTo(a.priority));
        return list;
    }

    // Returns any free room that matches this priority level, or null if all are busy.
    // No yield happens between this call and occupiedRooms.Add(), so the claim is atomic.
    string FindFreeRoom(int priority)
    {
        if (priority == 5)
            return !occupiedRooms.Contains("operating3") ? "operating3" : null;
        if (priority == 4)
            return !occupiedRooms.Contains("operating2") ? "operating2" : null;

        // Priority 1-3: any normal room that is currently free
        var pool = pathManager.rooms
            .Select(r => r.roomName)
            .Where(n => n != "operating2" && n != "operating3" && !occupiedRooms.Contains(n))
            .ToList();

        return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
    }

    // ─── Slot → priority lane ─────────────────────────────────────────────────
    // Slot 0 owns priority 5, slot 1 owns priority 4, slot 2 owns priority 3,
    // slot 3 owns priority 1 and 2 (both low tiers share the last point).
    static int HomeSlot(int priority) =>
        priority == 5 ? 0 : priority == 4 ? 1 : priority == 3 ? 2 : 3;

    // Try to fill slot i respecting the lane rule:
    //   1. Take a patient whose home slot == i  (own lane — always preferred)
    //   2. If own lane is exhausted, take any patient who is the last survivor of
    //      their lane (no other patients of the same lane remain → they can overflow)
    // Returns null if no eligible patient exists yet (slot waits).
    PatientInfo TakeForSlot(int slotIndex)
    {
        // 1 — own lane
        var p = pendingList.FirstOrDefault(x => HomeSlot(x.priority) == slotIndex);
        if (p != null) { pendingList.Remove(p); return p; }

        // 2 — overflow: patient whose lane has no other waiting members
        p = pendingList.FirstOrDefault(x =>
            !pendingList.Any(o => o != x && HomeSlot(o.priority) == HomeSlot(x.priority)));
        if (p != null) { pendingList.Remove(p); return p; }

        return null; // own lane empty, other lanes still have patients — wait
    }

    // ─── Spawn loop ──────────────────────────────────────────────────────────
    IEnumerator SpawnLoop()
    {
        while (pendingList.Count > 0)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (!spawnOccupied[i])
                {
                    var patient = TakeForSlot(i);
                    if (patient != null)
                    {
                        spawnOccupied[i] = true;
                        StartCoroutine(RunPatient(patient, i));
                    }
                }
            }
            yield return new WaitForSeconds(0.3f);
        }
    }

    // ─── Per-patient coroutine ───────────────────────────────────────────────
    IEnumerator RunPatient(PatientInfo patient, int spawnIdx)
    {
        Transform spawnPt = spawnPoints[spawnIdx];

        CharacterMover character = Instantiate(characterPrefab, spawnPt.position, spawnPt.rotation);
        character.SetPriorityColor(PriorityColors[patient.priority - 1]);

        patient.waitStartTime = Time.time;
        patient.isWaiting = true;

        // ── Wait at spawn point until ANY compatible room is free ─────────
        // FindFreeRoom + Add have no yield between them → atomic claim.
        string claimedRoom = null;
        while (claimedRoom == null)
        {
            claimedRoom = FindFreeRoom(patient.priority);
            if (claimedRoom == null)
                yield return new WaitForSeconds(0.2f);
        }
        occupiedRooms.Add(claimedRoom);
        patient.targetRoom = claimedRoom;

        // Room claimed → free spawn slot so the next patient can fill it
        // while this one is still walking to the room.
        spawnOccupied[spawnIdx] = false;
        patient.isWaiting = false;
        totalWaitTime += Time.time - patient.waitStartTime;
        servedCount++;

        if (patient.targetRoom.StartsWith("operating")) surgeriesCount++;
        if (patient.targetRoom == "mri") mriCount++;

        // ── Leg 1: spawn point → merge point ──────────────────────────────
        bool reachedMerge = false;
        character.MoveAlongPath(new List<Transform> { mainMergePoint }, () => reachedMerge = true);
        yield return new WaitUntil(() => reachedMerge);

        // ── Leg 2: merge point → target room ──────────────────────────────
        List<Transform> toRoom = pathManager.GetPath(mainMergePoint.name, patient.targetRoom);
        if (toRoom != null && toRoom.Count > 0)
        {
            bool arrived = false;
            character.MoveAlongPath(toRoom, () => arrived = true);
            yield return new WaitUntil(() => arrived);
        }

        // ── In room ───────────────────────────────────────────────────────
        patient.isInRoom = true;
        yield return new WaitForSeconds(3f);

        // ── Leg 3: leave room → out ────────────────────────────────────────
        patient.isInRoom = false;
        occupiedRooms.Remove(patient.targetRoom);   // room is free again

        List<Transform> toOut = pathManager.GetPath(patient.targetRoom, outRoomName);
        if (toOut != null && toOut.Count > 0)
        {
            bool returned = false;
            character.MoveAlongPath(toOut, () => returned = true);
            yield return new WaitUntil(() => returned);
        }

        patient.isDone = true;
        Destroy(character.gameObject);
    }

    // ─── UI ──────────────────────────────────────────────────────────────────
    IEnumerator UILoop()
    {
        while (true)
        {
            RefreshUI();
            yield return new WaitForSeconds(0.5f);
        }
    }

    void RefreshUI()
    {
        if (allPatients == null) return;

        int totalRooms = pathManager.rooms.Count;
        int busyRooms = occupiedRooms.Count;
        int freeRooms = Mathf.Max(0, totalRooms - busyRooms);
        int waiting = allPatients.Count(p => p.isWaiting);
        int done = allPatients.Count(p => p.isDone);

        Set(txtPatientsNumber, $"Total Patients: {numberOfPatients}");
        Set(txtRoomsNumber, $"Total Rooms: {totalRooms}");
        Set(txtFreeRooms, $"Free Rooms: {freeRooms}");
        Set(txtBusyRooms, $"Busy Rooms: {busyRooms}");
        Set(txtPatientsWaiting, $"Waiting: {waiting}");
        Set(txtAvgWaitTime, $"Avg Wait: {(servedCount > 0 ? $"{totalWaitTime / servedCount:F1}s" : "—")}");
        Set(txtSurgeriesNumber, $"Surgeries: {surgeriesCount}");
        Set(txtMriScans, $"MRI Scans: {mriCount}");

        if (txtPatientsQueue == null) return;

        var sb = new System.Text.StringBuilder();
        int lines = 0;
        foreach (var p in allPatients.Where(p => !p.isDone).OrderByDescending(p => p.priority))
        {
            if (lines >= 4) break;
            string sev = p.priority >= 4 ? "High" : p.priority == 3 ? "Medium" : "Low";
            string room = string.IsNullOrEmpty(p.targetRoom) ? "Pending" : p.targetRoom;
            string status = p.isInRoom ? "in room" : p.isWaiting ? "waiting" : "en route";
            sb.AppendLine($"P{p.id:D2} | {sev,-6} | {room,-12} | {status}");
            lines++;
        }
        txtPatientsQueue.text = sb.ToString().TrimEnd();
    }

    void Set(TextMeshProUGUI t, string value)
    {
        if (t != null) t.text = value;
    }
}
