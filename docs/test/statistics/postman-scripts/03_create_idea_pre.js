// 03_create_idea_pre.js
// Purpose: generate deterministic idea #1 fields (idea titles are used in sorting validations).

const runId = pm.collectionVariables.get("run_id") || String(Date.now());
pm.collectionVariables.set("run_id", runId);

const ts = Date.now();
const v = pm.collectionVariables.get("api_version") || "v1";

// Prefix with "A" so when votes tie, idea #1 sorts before idea #2 by title.
pm.collectionVariables.set("idea1_title", `A QA Statistics Idea ${v} ${runId} ${ts}`);
pm.collectionVariables.set("idea1_description", `Auto-generated idea #1 for Statistics tests. run_id=${runId}, ts=${ts}`);

