# Full Simulation Scenario Trace: CSP vs. FCFS

This document traces a complete, end-to-end simulation scenario for a specific patient. It follows the exact logical arrangement of functions executed during the simulation, covering all possible outcomes (immediate treatment, queuing, and mortality) under the CSP algorithm, followed by a direct comparison of how the FCFS algorithm handles the exact same scenario.

---

## 1. The Scenario Setup

### The Patient: "Patient 42"
- **Condition:** `heart` (Requires: `ecg` or `opRoom`)
- **Severity:** `5` (Critical)
- **Arrival Time:** `simTime = 10`
- **Treatment Duration:** `300` seconds (5 minutes)
- **Mortality Window:** `600` seconds (Deadline: `simTime = 610`)

### The Hospital State (at `simTime = 10`)
- `ecg-1`: Busy (Treating someone else, `remainingTime: 120`)
- `ecg-2`: Free
- `opRoom-1`: Busy (Treating someone else, `remainingTime: 400`)

---

## 2. The CSP Algorithm Trace (Step-by-Step)

### Phase 1: Arrival & Injection
At `simTime = 10`, the UI triggers the global tick: `bus.emit("tick", 10)`.

1. **`PatientManager._onTick(10)` -> `_checkArrivals(10)`**
   - The loop reaches Patient 42. Since `arrival_time (10) <= simTime (10)`, the patient has arrived.
   - A deep copy is created: `JSON.parse(JSON.stringify(patient))`.
   - `cspEngine.addPatient(patientCopy)` is called.

2. **`CSPEngine.addPatient()`**
   - The patient is inserted into the `globalQueue`.
   - **Function run:** `PriorityQueue.enqueue()`
   - **Sort logic:** `CSP_COMPARATOR` triggers. Because Patient 42 has severity 5, they immediately jump to the **front** of the global queue, bypassing any lower-severity patients who arrived earlier.

### Phase 2: The Allocation Loop
Immediately after arrivals, the engine tries to allocate waiting patients.

3. **`CSPEngine._runAllocation(10)`**
   - The `while` loop starts. `patient = this.globalQueue.peek()` grabs Patient 42.

4. **Constraint Checking:** `AC3.run(patient, resources)`
   - Looks up `CONDITION_DOMAINS["heart"]` $\rightarrow$ `["ecg", "opRoom"]`.
   - Checks hospital inventory. Let's assume the hospital actually has `ecg` and `opRoom` units built.
   - **Returns:** `{ valid: ["ecg", "opRoom"], eliminated: [] }`.

5. **Heuristic Scoring:** `AStar.allocate(patient, valid, resourceUnits)`
   - Loops through the valid units.
   - **`_score()` for `ecg-1` (Busy):** `(5 * 10) - (0 * 3) - 15 + 0 = 35`
   - **`_score()` for `ecg-2` (Free):** `(5 * 10) - (0 * 3) - 0 + 5 = 55`
   - **`_score()` for `opRoom-1` (Busy):** `(5 * 10) - (0 * 3) - 15 + 0 = 35`
   - `AStar` selects `ecg-2` as the best unit because its score (55) is the highest.

### Phase 3: Possible Outcomes (The Scenarios)

#### Scenario A: The Best Unit is Free (Our current setup)
6. **Assignment:** Since `ecg-2.isBusy` is false, `CSPEngine` emits `"unit:assign"`.
7. **`ResourceManager.assignPatient(ecg-2, patient, 10)`**
   - `ecg-2.isBusy = true`.
   - `ecg-2.remainingTime = 300`.
   - `patient.status = "in-treatment"`.
   - **Result:** Patient 42 starts treatment instantly.

#### Scenario B: All Units are Busy
*Assume `ecg-2` was actually busy with a 1-person local queue.*
6. **Scoring Change:** `AStar` now scores `ecg-2` lower because of its local queue penalty `(-3)`. It might tie with `ecg-1`. `AStar` picks the busy unit with the shortest queue.
7. **Queuing:** Since the best unit is busy, `CSPEngine` does NOT emit `"unit:assign"`. Instead:
   - `bestUnit.localQueue.enqueue(patient)`.
   - Patient 42 waits.
   - Because of `LOCAL_QUEUE_COMPARATOR`, Patient 42 (Severity 5) jumps to the front of this specific unit's local queue, bumping less critical patients.

#### Scenario C: Mortality Deadline Approaching / Reached
*Assume Patient 42 is stuck in a local queue for a long time.*
8. **Tick 460:** `PatientManager._checkMortality(460)` runs.
   - 450 seconds have passed (75% of the 600s window).
   - Patient 42 is flagged `isAtRisk = true`. UI turns red.
9. **Tick 610:** `PatientManager._checkMortality(610)` runs.
   - `simTime (610) >= mortalityDeadline (610)`.
   - Patient 42 is removed from the queue. `status = "deceased"`.
   - `StatsEngine` records the death.

---

## 3. The FCFS Algorithm Trace (Comparison)

Now, let's trace exactly how the **First-Come, First-Served** engine handles the exact same Patient 42 arriving at `simTime = 10`.

### Phase 1: Arrival & Injection
1. **`PatientManager._checkArrivals(10)`**
   - A deep copy is sent to `FCFSEngine.addPatient()`.
2. **Global Queue Sorting:**
   - **Function run:** `PriorityQueue.enqueue()` using `FCFS_COMPARATOR`.
   - **CRITICAL DIFFERENCE:** `FCFS_COMPARATOR` strictly compares `arrival_time`. It completely ignores severity. Even though Patient 42 is Severity 5, they are placed at the **back** of the global queue, behind any Severity 1 patients who arrived at `simTime = 8` or `9`.

### Phase 2: The Allocation Loop
3. **`FCFSEngine._runAllocation(10)`**
   - Wait! Patient 42 is at the back of the queue. The loop must process all the earlier, less critical patients first.
   - Once the loop finally reaches Patient 42:
4. **No Constraint Checking (No AC3):**
   - `FCFSEngine` just blindly looks at `CONDITION_DOMAINS["heart"]`.
5. **No Heuristic Scoring (No AStar):**
   - `FCFSEngine` maps `validUnits`.
   - It runs: `const bestUnit = validUnits.find(u => !u.isBusy) || validUnits[0];`
   - **CRITICAL DIFFERENCE:** It grabs the *very first* free unit it finds in the array. If all units are busy, it blindly throws the patient into the local queue of `validUnits[0]` (`ecg-1`), completely ignoring that `opRoom-1` might have a much shorter wait time.

### Phase 3: The Outcomes (FCFS Disadvantages)

#### If All Units are Busy (Queuing)
- In CSP, Patient 42 jumped to the front of the local queue.
- In FCFS, Patient 42 is added to the local queue using `FCFS_COMPARATOR`. They sit at the back of the local queue, waiting for all previously arrived patients to finish, drastically increasing their chance of hitting the mortality deadline.

---

## 4. Full Functional Comparison Summary

| Feature / Step | CSP (Constraint Satisfaction + Heuristics) | FCFS (First-Come, First-Served) |
| :--- | :--- | :--- |
| **Global Queue Sorting** | `CSP_COMPARATOR`: Severity first, Arrival time second. Critical patients skip the line. | `FCFS_COMPARATOR`: Strict arrival time. Severity is completely ignored. |
| **Domain Validation** | `AC3.run()`: Intelligently eliminates resource types that have 0 units built in the hospital. | Hardcoded mapping. May attempt to find units that don't exist, failing silently. |
| **Unit Selection** | `AStar.allocate()`: Calculates a mathematical score balancing clinical urgency, unit availability, and load-balancing (local queue size). | Blind `find()`: Grabs the first free unit in the array, or dumps the patient on the first busy unit, creating massive bottlenecks. |
| **Local Queue Sorting** | `LOCAL_QUEUE_COMPARATOR`: Patients wait at the door of the unit, sorted by severity. | `FCFS_COMPARATOR`: Patients wait in strict chronological order. |
| **Mortality Risk** | Low for severe patients (they skip lines), higher for mild patients (they keep getting bumped). | High for severe patients if hospital is under heavy load, as they cannot bypass the queue. |
