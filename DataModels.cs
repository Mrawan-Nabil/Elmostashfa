using System.Collections.Generic;

public enum TreatmentType { Ward, MRI, Surgery, ICU, Triage }
public enum PatientState { Waiting, InTransit, InTreatment, Discharged, Deceased }
public enum DifficultyLevel { Trainee = 1, Manager = 2, Coordinator = 3 }

[System.Serializable]
public class RoomSlot
{
    public int SlotID;
    public bool IsOccupied;
    public Patient Occupant;

    public RoomSlot(int id)
    {
        SlotID = id;
        IsOccupied = false;
        Occupant = null;
    }
}


[System.Serializable]
public class Room
{
    public TreatmentType Type;
    public List<RoomSlot> Slots;

    public bool IsAvailable => Slots.Exists(s => !s.IsOccupied);

    public Room(TreatmentType type, int capacity)
    {
        Type = type;
        Slots = new List<RoomSlot>();
        for (int i = 0; i < capacity; i++)
        {
            Slots.Add(new RoomSlot(i));
        }
    }

    public RoomSlot GetFreeSlot()
    {
        return Slots.Find(s => !s.IsOccupied);
    }
}

[System.Serializable]
public class Patient
{
    public int ID;
    public int Severity; // 1-10
    public TreatmentType RequiredTreatment;
    public int WaitTime;
    public int MaxWaitLimit; // Determines when a critical patient dies
    public int TreatmentTimeRemaining;
    
    public PatientState State;
    public RoomSlot AssignedSlot;

    public Patient(int id, int severity, TreatmentType treatment)
    {
        ID = id;
        Severity = severity;
        RequiredTreatment = treatment;
        WaitTime = 0;
        State = PatientState.Waiting;
        AssignedSlot = null;
        
        // Randomize treatment duration for variety (in ticks/seconds)
        TreatmentTimeRemaining = UnityEngine.Random.Range(10, 25); 

        // If high severity (7-10), they only have a limited time before expiring
        MaxWaitLimit = (severity >= 7) ? UnityEngine.Random.Range(20, 45) : 9999;
    }
}