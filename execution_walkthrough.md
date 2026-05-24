# JavaScript Execution Walkthrough: Hospital Simulation

This document provides a granular, step-by-step trace of the V8 JavaScript engine as it executes your `algorithms.js` code. It focuses strictly on syntax, variable state, memory allocation, and the synchronous call stack.

---

## 1. The Initialization Trace

When the scripts first load, the JavaScript engine reads the class definitions into memory and then instantiates your core objects.

### Instantiating the EventBus
```javascript
const bus = new EventBus();
```
When this line executes, the V8 engine allocates memory for a new `EventBus` object. Its constructor creates a single property:
```javascript
constructor() {
  this.listeners = {}; // An empty JavaScript object (Hash Map) in memory
}
```

### Instantiating the Engines
Next, the engines and managers are instantiated, passing the `bus` by reference:
```javascript
const cspEngine = new CSPEngine(bus, resourcesConfig);
```
Inside the `CSPEngine` constructor, after initializing queues and algorithms, it registers an event listener:
```javascript
this.bus.on("tick", (simTime) => this._onTick(simTime));
```
**What happens in memory?**
1. The JS engine evaluates the arrow function `(simTime) => this._onTick(simTime)`. This creates a new Function Object in memory, bound lexically to the `CSPEngine` instance (so `this` correctly refers to the engine).
2. It calls `bus.on("tick", [Function Object])`.
3. Inside `EventBus.js`:
```javascript
if (!this.listeners[event]) this.listeners[event] = [];
this.listeners[event].push(callback);
```
The V8 engine checks the `this.listeners` object for the key `"tick"`. It doesn't exist, so it creates an empty array `[]`. It then pushes the arrow function reference into `this.listeners["tick"]`.

---

## 2. The Execution Path: The Global Tick

The simulation is driven by a timer (likely `setInterval` in the UI) which triggers the tick:
```javascript
bus.emit("tick", simTime);
```

### The `emit` Call Stack
Inside `EventBus.js`:
```javascript
emit(event, data) {
  if (this.listeners[event]) {
    for (const cb of this.listeners[event]) cb(data);
  }
}
```
1. The engine looks up `this.listeners["tick"]` and finds the array of callback functions.
2. The `for...of` loop executes synchronously. It pulls the first callback (e.g., the one registered by `PatientManager`), passes `simTime` to it, and pushes that callback onto the Call Stack.

### Injecting Duplicates (PatientManager)
Inside `PatientManager._checkArrivals()`:
```javascript
if (hasArrived && isWaiting && notYetAdded) {
  patient._queued = true;
  this.cspEngine.addPatient(JSON.parse(JSON.stringify(patient)));
  this.fcfsEngine.addPatient(JSON.parse(JSON.stringify(patient)));
}
```
**Syntactic breakdown of deep cloning:**
1. `JSON.stringify(patient)`: The engine traverses the `patient` object in memory, converting all its keys and primitive values into a single massive String.
2. `JSON.parse(...)`: The engine takes that String, parses it, and allocates an entirely **new** object in heap memory.
3. This new object has a completely different memory address. If `CSPEngine` mutates `patient.status`, `FCFSEngine`'s patient remains untouched.

---

## 3. The Allocation Loop Walkthrough (Line-by-Line)

Once `CSPEngine._onTick(simTime)` is on the stack, it calls `this._runAllocation(simTime)`.

### The `while` Loop and Safety Cap
```javascript
let attempts = 0;
const maxAttempts = this.globalQueue.size + 1; 

while (!this.globalQueue.isEmpty && attempts < maxAttempts) {
  attempts++;
  const patient = this.globalQueue.peek();
  if (!patient) break;
  // ... allocation logic ...
}
```
1. `attempts` and `maxAttempts` are allocated as primitive numbers in the local execution context.
2. The `while` condition checks if the queue has items AND if `attempts` is less than `maxAttempts`.
3. **The Safety Cap:** If a patient cannot be allocated, they are moved to the back of the queue. Without `attempts < maxAttempts`, the loop would immediately process that same patient again, infinitely. The engine would hang, and the browser would crash. The cap forces the loop to break after checking every patient exactly once per tick.

### Stepping into `AC3.run()`
```javascript
const { valid, eliminated } = this.ac3.run(patient, this.resourcesConfig);
```
Inside `AC3.js`:
```javascript
const domain = [...(CONDITION_DOMAINS[patient.condition] ?? [])];
const valid = [];
const eliminated = [];
```
1. **Spread Syntax `[...]`:** The engine accesses the hash map `CONDITION_DOMAINS` using `patient.condition` as the string key. It takes the returned array (e.g., `["ecg", "opRoom"]`) and creates a shallow copy of it using the spread operator. The nullish coalescing operator `?? []` ensures it won't crash if the condition is missing.
2. **Filtering Loop:**
```javascript
for (const resourceType of domain) {
  if ((resources[resourceType] ?? 0) > 0) {
    valid.push(resourceType);
  } else {
    eliminated.push(resourceType);
  }
}
```
The V8 engine loops over `"ecg"`, checking `resources["ecg"]`. If the value is > 0, the string `"ecg"` is pushed into the `valid` array.

### Stepping into `AStar.allocate()`
```javascript
const bestUnit = this.astar.allocate(patient, valid, this.resourceUnits);
```
Inside `AStar.js`:
```javascript
let bestUnit  = null;
let bestScore = -Infinity;

for (const unit of resourceUnits) {
  if (!validResourceTypes.includes(unit.type)) continue;
  const score = this._score(patient, unit);
  // ...
```
1. Variables `bestUnit` (reference) and `bestScore` (number) are instantiated in local scope.
2. **Filtering:** `validResourceTypes.includes(unit.type)` is an $O(N)$ array search. If `unit.type` isn't in the `valid` list, `continue` skips the rest of the block and jumps to the next iteration.
3. **Scoring Evaluation:**
```javascript
return (patient.severity      * 10)
     - (unit.localQueue.size  *  3)
     - (unit.isBusy           ? 15 : 0)
     + (unit.isBusy           ?  0 :  5);
```
The JS engine evaluates this math strictly left-to-right. It performs property lookups (e.g., getting `unit.localQueue.size`), applies the multipliers, and evaluates the ternary operators (`? :`) to output a single primitive number representing the heuristic weight.
4. **Selection:**
```javascript
if (score > bestScore) {
  bestScore = score;
  bestUnit  = unit;
} 
```
If the new score beats `-Infinity`, `bestScore` is updated, and `bestUnit` is assigned the memory reference of the current `unit` object.

---

## 4. The JavaScript Event Loop & UI Rendering

It is critical to understand the execution context of this tick regarding the browser's Event Loop.

1. **Synchronous Execution:** When the timer interval fires, the browser places the tick function onto the **Call Stack**. JavaScript is strictly single-threaded. Everything described in sections 1, 2, and 3 happens completely synchronously.
2. **Blocking the Thread:** While `CSPEngine`, `FCFSEngine`, and `PatientManager` are looping, copying objects, and sorting arrays, the JavaScript main thread is "blocked." The browser **cannot** render HTML, CSS animations pause, and user clicks are queued.
3. **Event Emitting & DOM Updates:** Throughout the tick, your algorithms emit events (`bus.emit("queue:updated")`). The UI code listening to these events executes immediately as part of the same synchronous call stack, updating the DOM tree in memory (e.g., modifying `div.innerHTML`).
4. **Handing Control Back:** Only when the `_runAllocation` loop finishes, and the original `emit("tick")` function returns, does the Call Stack finally become empty.
5. **The Render Phase:** Once the stack is empty, the browser takes over, looks at the changes made to the DOM tree during the tick, and paints the new pixels to the screen.

If your code had an infinite `while` loop (or no `maxAttempts` cap), the Call Stack would never empty, the Render Phase would never trigger, and the browser tab would permanently freeze.
