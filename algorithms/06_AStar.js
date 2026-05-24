// =============================================================================
// AStar.js
// Heuristic Allocation — picks the best resource unit for a patient using
// a scoring formula. Higher score = better match.
//
// Score formula:
//   + severity * 10       (sicker patients get priority)
//   - localQueue.size * 3 (avoid units with long waiting lists)
//   - 15 if unit is busy  (prefer free units)
//   + 5  if unit is free  (bonus for immediately available units)
// =============================================================================

export class AStar {
  // Returns the best unit from the list, or null if none qualify
  allocate(patient, validResourceTypes, resourceUnits) {
    let bestUnit  = null;
    let bestScore = -Infinity;

    for (const unit of resourceUnits) {
      // Only consider units whose type is in the valid list
      if (!validResourceTypes.includes(unit.type)) continue;

      const score = this._score(patient, unit);

      if (score > bestScore) {
        bestScore = score;
        bestUnit  = unit;
      } else if (score === bestScore && bestUnit) {
        // Tiebreak: prefer the unit with the shorter local queue
        if (unit.localQueue.size < bestUnit.localQueue.size) {
          bestUnit = unit;
        }
      }
    }

    return bestUnit;
  }

  // Calculate how good a unit is for this patient right now
  _score(patient, unit) {
    return (patient.severity      * 10)
         - (unit.localQueue.size  *  3)
         - (unit.isBusy           ? 15 : 0)
         + (unit.isBusy           ?  0 :  5);
  }
}
