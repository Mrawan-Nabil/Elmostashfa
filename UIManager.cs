using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // If using legacy text/inputs
// using TMPro; // Uncomment if using TextMeshPro for inputs

public class UIManager : MonoBehaviour
{
    public SimulationManager simManager;
    
    [Header("Input References")]
    // public TMP_InputField patientInput; // Use this if using TextMeshPro
    public InputField patientInput; // Legacy UI 
    public Text startStopButtonText;

    private bool isRunning = false;

    // Hook this method up to your UI Button's OnClick event in the Unity Inspector
    public void OnStartStopButtonClicked()
    {
        // Read input box (default to 10 if empty or invalid)
        int incomingCount = 10;
        if (patientInput != null && int.TryParse(patientInput.text, out int parsed))
        {
            incomingCount = parsed;
        }

        // Toggle the Simulation
        simManager.ToggleSimulation(incomingCount, DifficultyLevel.Manager); // Hardcoding difficulty for now

        // Update UI Button Text
        isRunning = !isRunning;
        startStopButtonText.text = isRunning ? "STOP SIM" : "START SIM";
    }

    public void UpdateDashboardMetrics(List<Patient> queue, List<Patient> active, Dictionary<TreatmentType, Room> rooms)
    {
        // TODO: Update your UI Canvas Text elements here using the live lists.
        // E.g., int totalWaiting = queue.Count;
    }
}