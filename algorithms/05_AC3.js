// =============================================================================
// AC3.js
// Constraint Propagation — checks which resource types are actually available
// for a patient right now.
//
// It looks at the patient's condition, finds the allowed resource types from
// CONDITION_DOMAINS, then removes any type that has zero units available.
// Returns two lists: valid (can use) and eliminated (no units left).
// =============================================================================

import { CONDITION_DOMAINS } from './04_ConditionDomains.js';

export class AC3 {
  // resources = { ecg: 2, xray: 1, lab: 3, opRoom: 1 }
  run(patient, resources) {
    // Start with all resource types allowed for this condition
    const domain     = [...(CONDITION_DOMAINS[patient.condition] ?? [])];
    const valid      = [];
    const eliminated = [];

    for (const resourceType of domain) {
      if ((resources[resourceType] ?? 0) > 0) {
        // At least one unit of this type exists — keep it
        valid.push(resourceType);
      } else {
        // No units of this type — remove it from consideration
        eliminated.push(resourceType);
      }
    }

    return { valid, eliminated };
  }
}
