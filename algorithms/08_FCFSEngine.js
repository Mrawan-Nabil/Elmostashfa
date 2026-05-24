// =============================================================================
// FCFSEngine.js
// The traditional "first come, first served" engine.
// No priority sorting, no constraint checking, no scoring.
// Patients are served strictly in the order they arrived.
// =============================================================================

import { PriorityQueue }   from './02_PriorityQueue.js';
import { FCFS_COMPARATOR } from './03_Comparators.js';
import { CONDITION_DOMAINS } from './04_ConditionDomains.js';

export class FCFSEngine {
  constructor(bus, resourcesConfig) {
    this.bus             = bus;
    this.resourcesConfig = resourcesConfig;
    this.globalQueue     = new PriorityQueue(FCFS_COMPARATOR);
    this.resourceUnits   = []; // filled by ResourceManager after construction
    this.algorithm       = "fcfs";

    this.bus.on("tick", (simTime) => this._onTick(simTime));
  }

  _onTick(simTime) {
    this._runAllocation(simTime);
  }

  // Assign patients in arrival order — no scoring, just first available unit
  _runAllocation(simTime) {
    let attempts      = 0;
    const maxAttempts = this.globalQueue.size + 1;

    while (!this.globalQueue.isEmpty && attempts < maxAttempts) {
      attempts++;
      const patient = this.globalQueue.peek();
      if (!patient) break;

      // Find all units that can handle this patient's condition
      const validTypes = CONDITION_DOMAINS[patient.condition] || [];
      const validUnits = this.resourceUnits.filter(u => validTypes.includes(u.type));

      // No matching units exist at all — stop for this tick
      if (validUnits.length === 0) break;

      // Pick the first free unit, or fall back to the first unit if all are busy
      const bestUnit = validUnits.find(u => !u.isBusy) || validUnits[0];

      // Remove patient from the global queue
      this.globalQueue.remove(p => p.id === patient.id);

      if (!bestUnit.isBusy) {
        // Unit is free — start treatment immediately
        this.bus.emit("unit:assign", {
          unit: bestUnit, patient, simTime, algorithm: this.algorithm
        });
      } else {
        // Unit is busy — join its local queue
        bestUnit.localQueue.enqueue(patient);
        this.bus.emit("queue:updated",    { algorithm: this.algorithm });
        this.bus.emit("resource:updated", { algorithm: this.algorithm });
      }
    }
  }

  // Called when a new patient arrives
  addPatient(patient) {
    this.globalQueue.enqueue(patient);
    this.bus.emit("log:event",     { type: "arrival", patient, algorithm: this.algorithm });
    this.bus.emit("queue:updated", { algorithm: this.algorithm });
  }
}
