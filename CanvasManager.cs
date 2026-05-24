using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SimulationUIManager : MonoBehaviour
{
    [Header("--- UI Panels ---")]
    public GameObject homeScreenPanel;
    public GameObject dashboardPanel;

    [Header("--- Inputs ---")]
    // Inputs from Dashboard
    public TMP_InputField inputIncomingPatients; 
    
    // Internal variables to store the inputs
    private int selectedDifficultyLevel = 1;
    private int currentIncomingPatients = 0;

    [Header("--- Outputs (Dashboard Metrics) ---")]
    public TextMeshProUGUI txtTotalPatients;
    public TextMeshProUGUI txtAvgWaitTime;
    public TextMeshProUGUI txtMortalityRisk;
    public TextMeshProUGUI txtResourceUtil;
    public TextMeshProUGUI txtSurgeriesToday;
    public TextMeshProUGUI txtMriScans;

    private void Start()
    {
        // Initialize UI state
        ShowHomeScreen();
    }

    // ==========================================
    // 1. INPUT HANDLING (From UI to Script)
    // ==========================================

    /// <summary>
    /// Call this from the OnClick events of your Level 1, 2, and 3 buttons on the Home Screen.
    /// Pass 1, 2, or 3 as the integer parameter in the Inspector.
    /// </summary>
    public void SelectLevelAndStart(int levelNumber)
    {
        selectedDifficultyLevel = levelNumber;
        Debug.Log($"Level {selectedDifficultyLevel} selected. Starting Sim...");
        
        ShowDashboard();
        ProcessSimulationData(); // Run initial calculation
    }

    /// <summary>
    /// Call this from the OnValueChanged event of your Incoming Patients input field,
    /// or from your +/- buttons.
    /// </summary>
    public void UpdateIncomingPatients(string patientInput)
    {
        if (int.TryParse(patientInput, out int result))
        {
            currentIncomingPatients = result;
            ProcessSimulationData(); // Recalculate whenever inputs change
        }
    }

    // ==========================================
    // 2. THE PROCESSOR (Where the magic will happen)
    // ==========================================

    /// <summary>
    /// This is the empty processing hub. It takes the current inputs, 
    /// runs the simulation logic, and prepares the outputs.
    /// </summary>
    private void ProcessSimulationData()
    {
        // TODO: Write your actual simulation algorithms here based on 'selectedDifficultyLevel' 
        // and 'currentIncomingPatients'.
        
        // --- DUMMY LOGIC FOR NOW ---
        int calculatedTotalPatients = currentIncomingPatients * selectedDifficultyLevel;
        float calculatedWaitTime = currentIncomingPatients * 1.5f;
        float calculatedMortality = selectedDifficultyLevel * 0.5f;
        float calculatedResourceUtil = (currentIncomingPatients / 100f) * 100f;
        int calculatedSurgeries = currentIncomingPatients / 4;
        int calculatedMRIs = currentIncomingPatients / 10;

        // After processing, send the results to the UI
        UpdateDashboardDisplay(
            calculatedTotalPatients, 
            calculatedWaitTime, 
            calculatedMortality, 
            calculatedResourceUtil, 
            calculatedSurgeries, 
            calculatedMRIs
        );
    }

    // ==========================================
    // 3. OUTPUT HANDLING (From Script to UI)
    // ==========================================

    private void UpdateDashboardDisplay(int totalPts, float waitTime, float mortality, float resources, int surgeries, int mris)
    {
        txtTotalPatients.text = totalPts.ToString();
        txtAvgWaitTime.text = $"{waitTime:F1} min";
        txtMortalityRisk.text = $"{mortality:F1}%";
        txtResourceUtil.text = $"{Mathf.Clamp(resources, 0, 100):F0}%";
        txtSurgeriesToday.text = surgeries.ToString();
        txtMriScans.text = mris.ToString();
    }

    // ==========================================
    // PANEL MANAGEMENT
    // ==========================================

    private void ShowHomeScreen()
    {
        homeScreenPanel.SetActive(true);
        dashboardPanel.SetActive(false);
    }

    private void ShowDashboard()
    {
        homeScreenPanel.SetActive(false);
        dashboardPanel.SetActive(true);
    }
}