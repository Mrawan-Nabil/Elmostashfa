// =============================================================================
// PatientManager.js
// Holds the master list of all patients.
// On every tick it checks two things:
//   1. Have any new patients arrived? If so, add them to both engines.
//   2. Has any waiting patient exceeded their mortality deadline? Mark them deceased.
// =============================================================================

export class PatientManager {
  constructor(bus, cspEngine, fcfsEngine) {
    this.bus        = bus;
    this.cspEngine  = cspEngine;
    this.fcfsEngine = fcfsEngine;
    this.patients   = []; // master list — never modified by the engines

    this.bus.on("tick", (simTime) => this._onTick(simTime));
  }

  // Enrich raw patient data with runtime fields and store the master list
  loadPatients(rawPatients) {
    this.patients = rawPatients.map(p => ({
      ...p,
      status:             "waiting",
      treatmentStartTime: null,
      treatmentEndTime:   null,
      actualWaitTime:     null,
      assignedUnit:       null,
      eliminatedPaths:    [],
      mortalityDeadline:  p.arrival_time + p.mortality_window, // absolute sim-time of death
      isAtRisk:           false, // true when 75% of mortality window has passed
      deceased:           false,
      _queued:            false  // internal flag — prevents adding the same patient twice
    }));
  }

  _onTick(simTime) {
    this._checkArrivals(simTime);
    this._checkMortality(simTime);
  }

  // Add patients to both engines when their arrival time is reached
  _checkArrivals(simTime) {
    for (const patient of this.patients) {
      const hasArrived  = patient.arrival_time <= simTime;
      const isWaiting   = patient.status === "waiting";
      const notYetAdded = !patient._queued;

      if (hasArrived && isWaiting && notYetAdded) {
        patient._queued = true;
        // Each engine gets its own deep copy so they never share state
        this.cspEngine.addPatient(JSON.parse(JSON.stringify(patient)));
        this.fcfsEngine.addPatient(JSON.parse(JSON.stringify(patient)));
      }
    }
  }

  // Check every waiting patient in both engines for mortality deadline
  _checkMortality(simTime) {
    const processMortality = (engine) => {
      let queueUpdated = false;

      // --- Check patients in the global waiting queue ---
      const globalPatients = engine.globalQueue.toArray();
      for (const p of globalPatients) {
        if (simTime >= p.mortalityDeadline
            && p.status !== "in-treatment"
            && p.status !== "treated") {
          // Patient has passed their deadline — mark as deceased
          p.status   = "deceased";
          p.deceased = true;
          engine.globalQueue.remove(x => x.id === p.id);
          this.bus.emit("patient:deceased", { patient: p, algorithm: engine.algorithm });
          this.bus.emit("log:event",        { type: "deceased", patient: p, algorithm: engine.algorithm });
          queueUpdated = true;

        } else if (simTime >= p.mortalityDeadline * 0.75
                   && p.status === "waiting"
                   && !p.isAtRisk) {
          // Patient has used 75% of their time — flag as at risk
          p.isAtRisk = true;
          this.bus.emit("patient:at_risk", { patient: p, algorithm: engine.algorithm });
          this.bus.emit("log:event", {
            type: "at_risk", patient: p, algorithm: engine.algorithm,
            timeLeft: p.mortalityDeadline - simTime
          });
          queueUpdated = true;
        }
      }

      // --- Check patients waiting in each unit's local queue ---
      for (const unit of engine.resourceUnits) {
        const localPatients = unit.localQueue.toArray();
        for (const p of localPatients) {
          if (simTime >= p.mortalityDeadline
              && p.status !== "in-treatment"
              && p.status !== "treated") {
            p.status   = "deceased";
            p.deceased = true;
            unit.localQueue.remove(x => x.id === p.id);
            this.bus.emit("patient:deceased", { patient: p, algorithm: engine.algorithm });
            this.bus.emit("log:event",        { type: "deceased", patient: p, algorithm: engine.algorithm });
            queueUpdated = true;

          } else if (simTime >= p.mortalityDeadline * 0.75
                     && p.status === "waiting"
                     && !p.isAtRisk) {
            p.isAtRisk = true;
            this.bus.emit("patient:at_risk", { patient: p, algorithm: engine.algorithm });
            this.bus.emit("log:event", {
              type: "at_risk", patient: p, algorithm: engine.algorithm,
              timeLeft: p.mortalityDeadline - simTime
            });
            queueUpdated = true;
          }
        }
      }

      if (queueUpdated) {
        this.bus.emit("queue:updated",    { algorithm: engine.algorithm });
        this.bus.emit("resource:updated", { algorithm: engine.algorithm });
      }
    };

    processMortality(this.cspEngine);
    processMortality(this.fcfsEngine);
  }
}
