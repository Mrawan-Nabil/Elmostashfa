using System.Collections;
using UnityEngine;

public class VisualManager : MonoBehaviour
{
    public SimulationManager simManager;

    public void SpawnPatientInWaitingRoom(Patient p)        
    {
        // TODO: Instantiate prefab.
        Debug.Log($"Visuals: Spawning Patient #{p.ID} (Severity {p.Severity}) in Triage.");
    }

    public void ChangeToHospitalGown(Patient p)
    {
        // TODO: Swap material/sprite.
        Debug.Log($"Visuals: Patient #{p.ID} changed into hospital gown.");
    }

    public void MovePatientAlongPath(Patient p, TreatmentType targetRoom, int specificSlotID)
    {
        // TODO: Use pathfinding to route GameObject to targetRoom at 'specificSlotID'.
        Debug.Log($"Visuals: Routing Patient #{p.ID} to {targetRoom} (Bed {specificSlotID}).");
        
        // Simulating travel time. Your teammate will replace this Coroutine 
        // by calling simManager.OnPatientArrivedAtRoom(p) when the animation finishes.
        StartCoroutine(SimulateTravelTime(p)); 
    }

    private IEnumerator SimulateTravelTime(Patient p)
    {
        // Fake 2 seconds of walking
        yield return new WaitForSeconds(2f); 
        simManager.OnPatientArrivedAtRoom(p);
    }

    public void HandlePatientDeath(Patient p)
    {
        // TODO: Play grim reaper animation or remove GameObject.
        Debug.LogWarning($"Visuals: Patient #{p.ID} DIED in the waiting room! Severity {p.Severity} waited too long.");
    }

    public void HandlePatientDischarge(Patient p)
    {
        // TODO: Route them to the exit door and destroy GameObject.
        Debug.Log($"Visuals: Patient #{p.ID} successfully treated and DISCHARGED.");
    }
}