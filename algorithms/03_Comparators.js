// =============================================================================
// Comparators.js
// Sort functions used by PriorityQueue instances throughout the simulation.
// Each comparator returns a negative number if (a) should come before (b).
// =============================================================================

// CSP global queue: highest severity first, then earliest arrival as tiebreak
export const CSP_COMPARATOR = (a, b) => {
  if (b.severity !== a.severity) return b.severity - a.severity;
  return a.arrival_time - b.arrival_time;
};

// FCFS global queue: strictly by arrival time (first come, first served)
export const FCFS_COMPARATOR = (a, b) => {
  return a.arrival_time - b.arrival_time;
};

// Local room queue (CSP only): highest severity first among patients waiting for the same unit
export const LOCAL_QUEUE_COMPARATOR = (a, b) => {
  return b.severity - a.severity;
};
