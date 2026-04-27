// 03_create_idea_pre.js
// Purpose: generate dynamic idea fields for request body variables.

const runId = pm.collectionVariables.get("run_id") || String(Date.now());
pm.collectionVariables.set("run_id", runId);

const ts = Date.now();
const v = pm.collectionVariables.get("api_version") || "v1";

pm.collectionVariables.set("idea_title", `QA Vote Idea ${v} ${runId} ${ts}`);
pm.collectionVariables.set("idea_description", `Auto-generated idea for Votes tests. run_id=${runId}, ts=${ts}`);

