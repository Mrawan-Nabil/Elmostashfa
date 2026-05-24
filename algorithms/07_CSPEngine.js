// =============================================================================
// CSPEngine.js
// The smart allocation engine (Constraint Satisfaction Problem).
//
// Each tick it takes the highest-priority patient from the global queue,
// runs AC3 to find valid resource types, then uses AStar to pick the best unit.
// If the best unit is free  → patient starts treatment immediately.
// If the best unit is busy  → patient joins that unit's local waiting queue.
// =============================================================================

import { PriorityQueue }  from './02_PriorityQueue.js';
import { CSP_COMPARATOR } from './03_Comparators.js';
import { AC3 }            from './05_AC3.js';
import { AStar }          from './06_AStar.js';

export class CSPEngine {
  constructor(bus, resourcesConfig) {
    this.bus = bus;

    // Translate the config keys used in the UI to the keys used in CONDITION_DOMAINS
    this.resourcesConfig = {
      ecg:    resourcesConfig.ecg     || 0,
      opRoom: resourcesConfig.opRooms || 0,
      xray:   resourcesConfig.xray    || 0,
      lab:    resourcesConfig.labs    || 0,
    };

    this.globalQueue   = new PriorityQueue(CSP_COMPARATOR);
    this.resourceUnits = []; // filled by ResourceManager after construction
    this.ac3           = new AC3();
    this.astar         = new AStar();
    this.algorithm     = "csp";

    // Run allocation on every simulation tick
    this.bus.on("tick", (simTime) => this._onTick(simTime));
  }

  _onTick(simTime) {
    this._runAllocation(simTime);
  }

  // Try to assign every waiting patient to a resource unit
  _runAllocation(simTime) {
    let attempts      = 0;
    const maxAttempts = this.globalQueue.size + 1; // safety cap to prevent infinite loops

    while (!this.globalQueue.isEmpty && attempts < maxAttempts) {
      attempts++;
      const patient = this.globalQueue.peek();
      if (!patient) break;

      // Step 1: find which resource types are available for this patient
      const { valid, eliminated } = this.ac3.run(patient, this.resourcesConfig);

      // Log any resource types that were ruled out
      for (const resourceType of eliminated) {
        if (!patient.eliminatedPaths) patient.eliminatedPaths = [];
        if (!patient.eliminatedPaths.includes(resourceType)) {
          patient.eliminatedPaths.push(resourceType);
          this.bus.emit("log:event", {
            type: "ac3_eliminated", patient, resourceType, algorithm: this.algorithm
          });
        }
      }

      // If no valid resource types exist at all, move patient to end and stop this tick
      if (valid.length === 0) {
        this.globalQueue.remove(p => p.id === patient.id);
        this.globalQueue.enqueue(patient);
        break;
      }

      // Step 2: pick the best unit using the AStar heuristic
      const bestUnit = this.astar.allocate(patient, valid, this.resourceUnits);

      // If no matching unit exists at all, leave patient in queue and stop
      if (!bestUnit) break;

      // Remove patient from the global queue — they are being handled
      this.globalQueue.remove(p => p.id === patient.id);

      if (!bestUnit.isBusy) {
        // Unit is free — start treatment right now
        this.bus.emit("unit:assign", {
          unit: bestUnit, patient, simTime, algorithm: this.algorithm
        });
      } else {
        // Unit is busy — add patient to that unit's local waiting queue
        bestUnit.localQueue.enqueue(patient);
        this.bus.emit("queue:updated",    { algorithm: this.algorithm });
        this.bus.emit("resource:updated", { algorithm: this.algorithm });
      }
    }
  }

  // Called when a new patient arrives — adds them to the global queue
  addPatient(patient) {
    this.globalQueue.enqueue(patient);
    this.bus.emit("log:event",     { type: "arrival", patient, algorithm: this.algorithm });
    this.bus.emit("queue:updated", { algorithm: this.algorithm });
  }
}
