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

// One run id used across the entire 7-step flow (helps correlate data across requests).
if (!pm.collectionVariables.get("run_id")) {
  pm.collectionVariables.set("run_id", String(Date.now()));
}

// Clear variables from previous runs to avoid false positives.
[
  "seed_token",
  "topic_id",
  "topic_title",
  "topic_description",
  "topic_title_updated",
  "topic_description_updated",
  "topic_ownerId",
  "idea1_id",
  "idea2_id",
  "vote1_id",
  "expected_winning_idea_id",
  "topic_setup_in_progress",
  "topic_setup_done"
].forEach((k) => pm.collectionVariables.unset(k));

pm.collectionVariables.set("topic_setup_attempts", "0");

// Basic sanity checks (fail fast in Tests if missing).
pm.variables.set("__seed_login_present", !!pm.collectionVariables.get("seed_login"));
pm.variables.set("__seed_password_present", !!pm.collectionVariables.get("seed_password"));

