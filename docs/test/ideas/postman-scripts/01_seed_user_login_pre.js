// 01_seed_user_login_pre.js
// Purpose: normalize required collection variables and clear prior run artifacts.

function ensureCollectionVar(key, defaultValue) {
  const current = pm.collectionVariables.get(key);
  if (current === undefined || current === null || current === "") {
    pm.collectionVariables.set(key, defaultValue);
  }
}

ensureCollectionVar("base_url", "http://localhost:5257");
ensureCollectionVar("api_version", "v1");
ensureCollectionVar("phase1_base_url", "http://localhost:8080");
ensureCollectionVar("perf_threshold_ms", "500");

// One run id used across the 9-step flow.
if (!pm.collectionVariables.get("run_id")) {
  pm.collectionVariables.set("run_id", String(Date.now()));
}

// Clear variables from previous runs to avoid false positives.
[
  "seed_token",
  "topic_id",
  "topic_title",
  "topic_description",
  "idea_id",
  "idea_title",
  "idea_description",
  "idea_ownerId",
  "idea_title_updated",
  "idea_description_updated"
].forEach((k) => pm.collectionVariables.unset(k));

// Sanity flags (checked in Tests).
pm.variables.set("__seed_login_present", !!pm.collectionVariables.get("seed_login"));
pm.variables.set("__seed_password_present", !!pm.collectionVariables.get("seed_password"));

