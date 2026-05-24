using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    [Header("Dependencies")]
    public UIManager uiManager;
    public VisualManager visualManager;

    [Header("Hospital Resources (Manual Setup)")]
    public int startingBeds = 10;
    public int startingMRIs = 2;
    public int startingOpsRooms = 3;
    public int startingICUs = 4;

    // State Variables
    public List<Patient> patientQueue = new List<Patient>();
    public List<Patient> activePatients = new List<Patient>();
    public List<Patient> completedPatients = new List<Patient>(); // Holds Dead(deseased)/Discharged
    
    private Dictionary<TreatmentType, Room> hospitalRooms = new Dictionary<TreatmentType, Room>();
    
    private bool isSimRunning = false;
    private int totalGenerated = 0;

    void Start()
    {
        // Initialize Rooms with specific Slots
        hospitalRooms.Add(TreatmentType.Ward, new Room(TreatmentType.Ward, startingBeds));
        hospitalRooms.Add(TreatmentType.MRI, new Room(TreatmentType.MRI, startingMRIs));
        hospitalRooms.Add(TreatmentType.Surgery, new Room(TreatmentType.Surgery, startingOpsRooms));
        hospitalRooms.Add(TreatmentType.ICU, new Room(TreatmentType.ICU, startingICUs));
    }

    // Called by the UIManager when the button is clicked
    public void ToggleSimulation(int incomingPatients, DifficultyLevel difficulty)
    {
        if (isSimRunning)
        {
            // STOP SIMULATION
            isSimRunning = false;
            StopAllCoroutines();
            Debug.Log("Simulation Stopped.");
        }
        else
        {
            // START SIMULATION
            GeneratePatients(incomingPatients, difficulty);
            isSimRunning = true;
            StartCoroutine(SimulationTickRoutine());
            Debug.Log("Simulation Started.");
        }
    }

    private void GeneratePatients(int count, DifficultyLevel difficulty)
    {
        TreatmentType[] possibleTreatments = { TreatmentType.Ward, TreatmentType.MRI, TreatmentType.Surgery, TreatmentType.ICU };

        for (int i = 0; i < count; i++)
        {
            totalGenerated++;
            int severity = GenerateSeverity(difficulty);
            TreatmentType randomTreatment = possibleTreatments[Random.Range(0, possibleTreatments.Length)];
            
            Patient newPatient = new Patient(totalGenerated, severity, randomTreatment);
            patientQueue.Add(newPatient);

            visualManager.SpawnPatientInWaitingRoom(newPatient);
        }
    }

    private int GenerateSeverity(DifficultyLevel diff)
    {
        int roll = Random.Range(1, 101);
        if (diff == DifficultyLevel.Coordinator) return roll > 40 ? Random.Range(7, 11) : Random.Range(1, 7);
        else if (diff == DifficultyLevel.Manager) return roll > 70 ? Random.Range(7, 11) : Random.Range(1, 7);
        else return roll > 90 ? Random.Range(7, 11) : Random.Range(1, 7);
    }

    private IEnumerator SimulationTickRoutine()
    {
        while (isSimRunning)
        {
            yield return new WaitForSeconds(1f); // 1 Tick = 1 Real Second

            ProcessWaitTimesAndMortality();
            SortQueue();
            GreedyAllocate();
            ProcessTreatments();
            
            uiManager.UpdateDashboardMetrics(patientQueue, activePatients, hospitalRooms);
        }
    }

    private void ProcessWaitTimesAndMortality()
    {
        // Iterate backwards because we might remove items from the list
        for (int i = patientQueue.Count - 1; i >= 0; i--)
        {
            Patient p = patientQueue[i];
            p.WaitTime++;

            // Mortality Check
            if (p.Severity >= 8 && p.WaitTime > p.MaxWaitLimit)
            {
                p.State = PatientState.Deceased;
                patientQueue.RemoveAt(i);
                completedPatients.Add(p);
                visualManager.HandlePatientDeath(p);
            }
        }
    }

    private void SortQueue()
    {
        patientQueue = patientQueue.OrderByDescending(p => p.Severity)
                                   .ThenByDescending(p => p.WaitTime)
                                   .ToList();
    }

    private void GreedyAllocate()
    {
        HashSet<TreatmentType> fullRoomsThisTick = new HashSet<TreatmentType>();
        List<Patient> allocatedThisTick = new List<Patient>();

        foreach (var patient in patientQueue)
        {
            TreatmentType tx = patient.RequiredTreatment;
            if (fullRoomsThisTick.Contains(tx)) continue;

            if (hospitalRooms.ContainsKey(tx) && hospitalRooms[tx].IsAvailable)
            {
                // Find the exact specific slot/bed that is open
                RoomSlot openSlot = hospitalRooms[tx].GetFreeSlot();
                
                openSlot.IsOccupied = true;
                openSlot.Occupant = patient;
                
                patient.AssignedSlot = openSlot;
                patient.State = PatientState.InTransit;
                
                allocatedThisTick.Add(patient);

                visualManager.ChangeToHospitalGown(patient);
                visualManager.MovePatientAlongPath(patient, tx, openSlot.SlotID);
            }
            else
            {
                fullRoomsThisTick.Add(tx);
            }
        }

        foreach (var p in allocatedThisTick)
        {
            patientQueue.Remove(p);
            activePatients.Add(p);
        }
    }

    // CALLBACK: Triggered by VisualManager when the patient reaches the bed
    public void OnPatientArrivedAtRoom(Patient patient)
    {
        if (patient.State == PatientState.InTransit)
        {
            patient.State = PatientState.InTreatment;
            Debug.Log($"Logic: Patient #{patient.ID} arrived at {patient.RequiredTreatment} Slot {patient.AssignedSlot.SlotID}. Treatment starting!");
        }
    }

    private void ProcessTreatments()
    {
        for (int i = activePatients.Count - 1; i >= 0; i--)
        {
            Patient p = activePatients[i];
            if (p.State == PatientState.InTreatment)
            {
                p.TreatmentTimeRemaining--;

                // Discharge Check
                if (p.TreatmentTimeRemaining <= 0)
                {
                    p.State = PatientState.Discharged;
                    
                    // Free the bed!
                    p.AssignedSlot.IsOccupied = false;
                    p.AssignedSlot.Occupant = null;
                    p.AssignedSlot = null;

                    activePatients.RemoveAt(i);
                    completedPatients.Add(p);
                    
                    visualManager.HandlePatientDischarge(p);
                }
            }
        }
    }
}