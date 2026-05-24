// =============================================================================
// StatsEngine.js
// Tracks all statistics for one algorithm (CSP or FCFS).
// Listens for patient events and samples time-series data every 60 sim-seconds.
// Provides live stats for the dashboard and final stats for the comparison panel.
// =============================================================================

export class StatsEngine {
  // label  = "csp" or "fcfs"
  // engine = the matching CSPEngine or FCFSEngine (used to read queue size)
  constructor(bus, label, engine) {
    this.label  = label;
    this.bus    = bus;
    this.engine = engine;

    this.treatedPatients   = []; // patients who finished treatment
    this.deceasedPatients  = []; // patients who died waiting
    this.waitTimeSeries    = []; // [{ t, avgWait }] sampled every 60 sim-seconds
    this.queueLengthSeries = []; // [{ t, length  }] sampled every 60 sim-seconds
    this.maxQueueLength    = 0;
    this.lastSampleTime    = 0;
    this.totalArrived      = 0;  // total patients added to this engine's queue

    // Listen for patient outcome events
    bus.on("patient:treated",  (data) => {
      if (data.algorithm === this.label) this.treatedPatients.push(data.patient);
    });
    bus.on("patient:deceased", (data) => {
      if (data.algorithm === this.label) this.deceasedPatients.push(data.patient);
    });

    // Count arrivals from the event log
    bus.on("log:event", (data) => {
      if (data.algorithm === this.label && data.type === "arrival") this.totalArrived++;
    });

    bus.on("tick", (t) => this._onTick(t));
  }

  _onTick(simTime) {
    // Push live stats to the UI on every tick
    this._updateLiveStats();

    // Sample time-series data once per minute of sim-time
    if (simTime - this.lastSampleTime >= 60) {
      this._sampleTimeSeries(simTime);
      this.lastSampleTime = simTime;
    }
  }

  // Emit the current stats so the UI can update the stat cards
  _updateLiveStats() {
    const mortalityRate = this.totalArrived > 0
      ? (this.deceasedPatients.length / this.totalArrived * 100)
      : 0;

    this.bus.emit("stats:updated", {
      algorithm: this.label,
      stats: {
        totalPatients:  this.totalArrived,
        treated:        this.treatedPatients.length,
        deceased:       this.deceasedPatients.length,
        avgWaitMinutes: this._currentAvgWait(),
        mortalityRate:  mortalityRate,
      }
    });
  }

  // Average wait time in minutes across all treated patients so far
  _currentAvgWait() {
    if (this.treatedPatients.length === 0) return 0;
    const totalWait = this.treatedPatients.reduce((sum, p) => sum + p.actualWaitTime, 0);
    return totalWait / this.treatedPatients.length / 60;
  }

  // Save a snapshot of current wait time and queue length
  _sampleTimeSeries(simTime) {
    this.waitTimeSeries.push({ t: simTime, avgWait: this._currentAvgWait() });

    const queueLen = this.engine?.globalQueue?.size ?? 0;
    this.queueLengthSeries.push({ t: simTime, length: queueLen });
  }

  // Called once at the end of the simulation to build the final summary
  computeFinalStats(allPatients) {
    let severity5Met   = 0; // sev-5 patients treated within 5 minutes
    let severity5Total = 0;

    for (const p of this.treatedPatients) {
      if (p.severity === 5) {
        severity5Total++;
        if (p.actualWaitTime <= 300) severity5Met++; // 300 seconds = 5 minutes
      }
    }

    return {
      avgWaitMinutes:        this._currentAvgWait(),
      livesSaved:            this.treatedPatients.length,
      mortalityRate:         this.totalArrived > 0
                               ? (this.deceasedPatients.length / this.totalArrived * 100)
                               : 0,
      maxQueueLength:        Math.max(...this.queueLengthSeries.map(s => s.length), 0),
      severity5MetThreshold: `${severity5Met} / ${allPatients.filter(p => p.severity === 5).length}`
    };
  }

  // Calculate how busy each resource type was as a percentage of total sim time
  computeUtilization(resourceUnits, simTime) {
    const grouped = {};

    for (const unit of resourceUnits) {
      if (!grouped[unit.type]) grouped[unit.type] = { busy: 0, count: 0 };
      grouped[unit.type].busy  += unit.busyTime;
      grouped[unit.type].count += 1;
    }

    const result = {};
    for (const type in grouped) {
      const totalPossibleTime = grouped[type].count * simTime;
      result[type] = totalPossibleTime > 0
        ? (grouped[type].busy / totalPossibleTime * 100)
        : 0;
    }

    return result;
  }
}
