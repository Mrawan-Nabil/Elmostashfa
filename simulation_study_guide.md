# Hospital Simulation Algorithmic Architecture: Technical Study Guide

This document provides a deep, full-scale analysis of your `algorithms.js` architecture. It is designed to prepare you for a technical discussion or defense by breaking down the algorithmic paradigms, data structures, operational flow, and potential "traps" a senior developer or professor might ask about.

---

## 1. Architectural & JavaScript Overview

### High-Level Summary
The simulation engine models a deterministic hospital routing system comparing two algorithms: a "smart" Constraint Satisfaction Problem (CSP) approach with Heuristic A* routing, and a traditional First-Come, First-Served (FCFS) approach. The core logic handles time-based discrete event simulation (ticking), resource allocation, queue management, and mortality tracking.

### Core Paradigms Used
Your system successfully employs a hybrid architectural approach:
- **Event-Driven Architecture (Pub/Sub):** The `EventBus` (`01_EventBus.js`) acts as the central nervous system. Instead of the UI tightly coupling to the simulation loop, or the `PatientManager` directly calling UI updates, systems `emit` events (e.g., `"tick"`, `"log:event"`, `"queue:updated"`). The visualizer front-end listens to these events to update the DOM, ensuring perfect decoupling between the "Model" (algorithms) and the "View" (HTML/JS).
- **Object-Oriented Programming (OOP):** The logic is strictly encapsulated into specialized classes (`CSPEngine`, `ResourceManager`, `StatsEngine`). Each class maintains its own internal state and single responsibility.
- **Discrete Event Simulation:** The system progresses through simulated time via a global `tick` mechanism rather than real-time intervals.

---

## 2. File & Data Structure Deep Dive

### Key Data Structures & Complexity Implications

#### 1. The Custom `PriorityQueue` (`02_PriorityQueue.js`)
- **Structure:** Wraps a standard JavaScript `Array` (`this._items`) and uses a custom `comparator` function to sort items.
- **Why it was chosen:** JavaScript does not have a built-in Priority Queue. Using an Array with custom sort functions (`CSP_COMPARATOR`, `FCFS_COMPARATOR`) makes the code incredibly readable and flexible for different engines.
- **Time Complexity Implications:** 
  - **Insertion (`enqueue`):** You use `.push()` followed by `.sort()`. In modern JS engines (V8), sorting is typically $O(N \log N)$. 
  - **Removal (`dequeue`):** Uses `.shift()`, which is $O(N)$ because it requires re-indexing the entire array in memory.
  - *Note: See "Professor's Traps" below for defense strategies.*

#### 2. Constraint Domains (`04_ConditionDomains.js`)
- **Structure:** Standard JavaScript Object (Hash Map/Dictionary).
- **Why it was chosen:** Provides $O(1)$ constant-time lookups to map a `condition` (e.g., `"heart"`) to its required resources (`["ecg", "opRoom"]`).

#### 3. Deep State Duplication (`PatientManager.js`)
- **Structure:** `JSON.parse(JSON.stringify(patient))`
- **Why it was chosen:** The simulation runs the CSP and FCFS algorithms side-by-side on the exact same patient data. You must pass by value (deep copy) so that when CSP modifies a patient's status, it doesn't accidentally mutate FCFS's version of that patient.

### Core Engine Functions & Rationale

- **`AC3.run()` (`05_AC3.js`):** Implements Arc Consistency 3 (Constraint Propagation). It takes the patient's domain (allowed resources) and cross-references it with actual hospital inventory. **Rationale:** It prevents the system from routing a patient to an X-Ray if zero X-Rays exist in the hospital, failing fast and updating eliminated paths.
- **`AStar.allocate()` (`06_AStar.js`):** A heuristic scoring function. **Rationale:** Standard CSP just finds *a* valid match. A* ensures we find the *best* match by calculating a score: `(Severity * 10) - (Queue Size * 3) + (Availability Bonus)`. It intelligently balances clinical urgency with hospital load balancing.
- **`CSPEngine._runAllocation()` (`07_CSPEngine.js`):** The master orchestrator. **Rationale:** It features a safety cap (`maxAttempts = globalQueue.size + 1`) to prevent infinite `while` loops if patients cannot be routed.

---

## 3. The Algorithmic Flow (Step-by-Step)

The entire system is driven by an external "tick" (usually a `setInterval` in the UI) that emits a `"tick"` event with the current `simTime`.

1. **Global Tick Broadcast:** `bus.emit("tick", simTime)` is fired.
2. **Arrivals & Mortality (PatientManager):** 
   - Iterates through the master patient list. If a patient's `arrival_time <= simTime`, they are duplicated and injected into both `CSPEngine` and `FCFSEngine`.
   - Checks if any waiting patient's `simTime >= mortalityDeadline`. If so, they are removed from all queues and marked `"deceased"`.
3. **Allocation Loop (CSPEngine / FCFSEngine):**
   - The engine peaks at the highest priority patient.
   - **(CSP Only):** `AC3` domain reduction filters out unavailable resource types.
   - **(CSP Only):** `AStar` heuristic scores remaining valid units and picks the best one.
   - **(FCFS Only):** Skips AC3/A* and simply grabs the first available matching unit.
4. **Queue Assignment:**
   - If the chosen unit is free, the patient is assigned immediately (`unit:assign` emitted).
   - If the chosen unit is busy, the patient is pushed into the unit's `localQueue`.
5. **Resource Progress (ResourceManager):**
   - Subtracts the elapsed tick time from `unit.remainingTime`. 
   - If `remainingTime <= 0`, it emits `"patient:treated"`, checks its `localQueue`, and immediately assigns the next waiting patient.
6. **Telemetry (StatsEngine):**
   - Samples queue lengths and average wait times every 60 sim-seconds to build historical graphs.

---

## 4. Technical Discussion Prep (The "Why" & Edge Cases)

### Core CS Concepts to Highlight
- **Greedy Allocation:** Your `AStar` implementation is technically a *Greedy Best-First Search*. It looks at the *immediate* state of the hospital and makes the best local choice right now. It does not look into the future or backtrack (which would be computationally explosive).
- **Domain Reduction (AC-3):** You are using constraint logic programming concepts. By filtering domains *before* searching for units, you drastically reduce the search space.
- **Discrete Event Simulation (DES):** The system state only changes at discrete points in time (ticks), allowing you to decouple simulation time from real wall-clock time (enabling fast-forward features).

### The JavaScript Factor: UI Threading
JavaScript is strictly single-threaded. 
- **The Challenge:** Heavy synchronous loops (like searching arrays, sorting queues, and deep copying objects on every tick) block the main thread. If the simulation processes 10,000 patients, the browser UI will freeze and the DOM won't update.
- **Your Defense / Future Optimization:** "Currently, the simulation utilizes a safety cap (`maxAttempts`) in the `while` loops to prevent thread locking. However, for a massive enterprise scale, I would optimize this by moving the simulation engines into a **Web Worker**. The Worker would run the calculations on a background thread and pass state differences back to the main UI thread via `postMessage()`."

### Professor's Traps & How to Defend Them

#### Trap 1: The `PriorityQueue` Time Complexity
**The Question:** *"I see you are using `array.sort()` on every enqueue and `.shift()` on dequeue. This makes your queue $O(N \log N)$ for insertion and $O(N)$ for removal. Why didn't you use a Binary Min/Max Heap which is $O(\log N)$?"*
**The Defense:** *"You're absolutely right. I intentionally chose to wrap a standard Array for this prototype because it allowed me to easily inject dynamic comparators (like `CSP_COMPARATOR`) and iterate over the queue for DOM rendering (`toArray()`). Given the current simulation constraint (usually < 1000 concurrent patients), the V8 engine handles Array sorting in sub-millisecond time. However, if this were deployed for a real national hospital network, swapping the internal `_items` array for a standard Binary Heap would be my first scalability refactor."*

#### Trap 2: The JSON Deep Copy Bottleneck
**The Question:** *"In `PatientManager`, you use `JSON.parse(JSON.stringify(patient))`. This is notoriously slow. Why do this?"*
**The Defense:** *"This was a deliberate choice to ensure absolute state isolation between the CSP and FCFS engines, preventing unintended pass-by-reference mutations. While it is slower than a custom copy function, our patient objects contain simple primitive strings and numbers without circular references, making JSON serialization safe and functionally reliable. In a production environment with modern JS, I would upgrade this to `structuredClone()` for native, optimized deep copying."*

#### Trap 3: A* vs Greedy Algorithm Terminology
**The Question:** *"You named your file `AStar.js`, but A* traditionally uses $f(n) = g(n) + h(n)$ (cost so far + estimated distance to goal). You are just scoring the best unit right now. Isn't this just a Heuristic Greedy Algorithm?"*
**The Defense:** *"That is a fair critique. In classic pathfinding, A* looks at total cost to a node. In this deterministic allocation context, I am using 'A*' conceptually as a heuristic scoring function $h(n)$ to evaluate the 'cost' of a hospital unit (combining severity urgency with local queue wait times). It does not do graph-based backtracking, so strictly speaking, it is a **Heuristic-driven Greedy Best-First Allocator**. The naming reflects the use of multi-variable heuristic weighting."*
