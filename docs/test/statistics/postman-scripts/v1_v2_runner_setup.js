// v1_v2_runner_setup.js (optional)
// Paste into the *collection-level* Pre-request Script to set defaults.

function ensureCollectionVar(key, defaultValue) {
  const current = pm.collectionVariables.get(key);
  if (current === undefined || current === null || current === "") {
    pm.collectionVariables.set(key, defaultValue);
  }
}

ensureCollectionVar("base_url", "http://localhost:5257");
ensureCollectionVar("api_version", "v1"); // switch to v2 to run the same flow against Mongo
ensureCollectionVar("phase1_base_url", "http://localhost:8080");
ensureCollectionVar("perf_threshold_ms", "500");

pm.collectionVariables.set("run_id", String(Date.now()));

