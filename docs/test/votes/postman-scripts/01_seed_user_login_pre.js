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

if (!pm.collectionVariables.get("run_id")) {
  pm.collectionVariables.set("run_id", String(Date.now()));
}

[
  "seed_token",
  "topic_id",
  "topic_title",
  "topic_description",
  "idea_id",
  "idea_title",
  "idea_description",
  "vote_id",
  "new_idea_id",
  "new_idea_title",
  "vote_move_setup_done",
  "vote_move_setup_in_progress",
  "vote_move_setup_attempts"
].forEach((k) => pm.collectionVariables.unset(k));

pm.collectionVariables.set("vote_move_setup_attempts", "0");

pm.variables.set("__seed_login_present", !!pm.collectionVariables.get("seed_login"));
pm.variables.set("__seed_password_present", !!pm.collectionVariables.get("seed_password"));

