// 07_update_idea_pre.js
// Purpose: generate dynamic updated title/description variables for the PUT body.

const runId = pm.collectionVariables.get("run_id") || String(Date.now());
pm.collectionVariables.set("run_id", runId);

const ts = Date.now();
const v = pm.collectionVariables.get("api_version") || "v1";

pm.collectionVariables.set("idea_title_updated", `QA Idea Updated ${v} ${runId} ${ts}`);
pm.collectionVariables.set("idea_description_updated", `Updated by automation. run_id=${runId}, ts=${ts}`);

