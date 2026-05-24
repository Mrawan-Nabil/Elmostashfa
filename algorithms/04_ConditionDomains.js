// =============================================================================
// ConditionDomains.js
// Maps each medical condition to the resource types that can treat it.
// AC3 and FCFSEngine both import this to know which resources are valid
// for a given patient's condition.
// =============================================================================

export const CONDITION_DOMAINS = {
  heart       : ["ecg", "opRoom"],
  fracture    : ["xray", "opRoom"],
  diabetes    : ["lab"],
  respiratory : ["xray", "lab", "opRoom"],
  general     : ["lab", "ecg"],
};
