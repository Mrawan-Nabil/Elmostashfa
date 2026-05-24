// =============================================================================
// ResourceManager.js
// Creates and manages all resource units (X-Ray machines, ECG units, etc.).
// On every tick it counts down the treatment timer for each busy unit.
// When a treatment finishes, it either pulls the next patient from the local
// queue or marks the unit as free.
// Each algorithm (CSP and FCFS) gets its own separate ResourceManager instance.
// =============================================================================

import { PriorityQueue }                                    from './02_PriorityQueue.js';
import { LOCAL_QUEUE_COMPARATOR, FCFS_COMPARATOR }          from './03_Comparators.js';

export class ResourceManager {
  constructor(bus, resourceConfig, algorithm) {
    this.bus       = bus;
    this.algorithm = algorithm;
    this.units     = this._buildUnits(resourceConfig);

    // Count down treatment timers on every tick
    this.bus.on("tick", (simTime) => this._advanceTimers(simTime));

    // Listen for assignment requests from the engine
    this.bus.on("unit:assign", (data) => {
      if (data.algorithm === this.algorithm) {
        this.assignPatient(data.unit, data.patient, data.simTime);
      }
    });
  }

  // Build the flat list of unit objects from the resource counts in config
  _buildUnits(config) {
    const units = [];

    // Each entry maps the config key (from the UI) to the domain type (used in CONDITION_DOMAINS)
    const types = [
      { configKey: 'doctors', type: 'doctors', label: 'Doctor'   },
      { configKey: 'nurses',  type: 'nurses',  label: 'Nurse'    },
      { configKey: 'xray',    type: 'xray',    label: 'X-Ray'    },
      { configKey: 'ecg',     type: 'ecg',     label: 'ECG'      },
      { configKey: 'labs',    type: 'lab',     label: 'Lab Unit' },
      { configKey: 'opRooms', type: 'opRoom',  label: 'Op. Room' },
    ];

    for (const t of types) {
      const count = config[t.configKey] || 0;
      for (let i = 1; i <= count; i++) {
        units.push({
          id:             `${t.type}-${i}`,
          type:           t.type,
          label:          `${t.label} #${i}`,
          isBusy:         false,
          currentPatient: null,
          // CSP local queues sort by severity; FCFS local queues sort by arrival time
          localQueue:     new PriorityQueue(
                            this.algorithm === "csp" ? LOCAL_QUEUE_COMPARATOR : FCFS_COMPARATOR
                          ),
          remainingTime:  0, // seconds left in current treatment
          totalTime:      0, // total duration of current treatment (for progress bar)
          busyTime:       0  // total seconds this unit has been busy (for utilization stats)
        });
      }
    }

    return units;
  }

  // Start treating a patient in a unit
  assignPatient(unit, patient, simTime) {
    unit.isBusy           = true;
    unit.currentPatient   = patient;
    unit.remainingTime    = patient.treatment_duration;
    unit.totalTime        = patient.treatment_duration;
    patient.status             = "in-treatment";
    patient.treatmentStartTime = simTime;
    patient.assignedUnit       = unit.id;

    this.bus.emit("log:event",        { type: "treatment_start", patient, unit, algorithm: this.algorithm });
    this.bus.emit("resource:updated", { algorithm: this.algorithm });
  }

  // Called every tick — subtract elapsed time from each busy unit's timer
  _advanceTimers(simTime) {
    // Skip the very first tick so we have a valid previous time to compare against
    if (this.lastSimTime === undefined) {
      this.lastSimTime = simTime;
      return;
    }

    const tickSecs = simTime - this.lastSimTime;
    this.lastSimTime = simTime;
    if (tickSecs <= 0) return;

    let anyUnitUpdated = false;

    for (const unit of this.units) {
      if (!unit.isBusy) continue;

      unit.remainingTime -= tickSecs;
      unit.busyTime      += tickSecs;
      anyUnitUpdated      = true;

      // Treatment finished — wrap up and move to the next patient
      if (unit.remainingTime <= 0) {
        this._completeTreatment(unit, simTime);
      }
    }

    if (anyUnitUpdated) {
      this.bus.emit("resource:updated", { algorithm: this.algorithm });
    }
  }

  // Called when a unit finishes treating its current patient
  _completeTreatment(unit, simTime) {
    const patient = unit.currentPatient;

    // Record final stats for this patient
    patient.status           = "treated";
    patient.treatmentEndTime = simTime;
    patient.actualWaitTime   = patient.treatmentStartTime - patient.arrival_time;

    this.bus.emit("log:event",       { type: "treatment_done", patient, unit, algorithm: this.algorithm });
    this.bus.emit("patient:treated", { patient, algorithm: this.algorithm });

    if (!unit.localQueue.isEmpty) {
      // Pull the next waiting patient from this unit's local queue
      const next = unit.localQueue.dequeue();
      this.assignPatient(unit, next, simTime);
    } else {
      // No one waiting — mark unit as free
      unit.isBusy         = false;
      unit.currentPatient = null;
      unit.remainingTime  = 0;
    }
  }

  getUnits() { return this.units; }
}
