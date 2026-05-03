// 02_create_topic_for_idea_pre.js
// Purpose: generate dynamic topic fields for a topic that will hold ideas.

const runId = pm.collectionVariables.get("run_id") || String(Date.now());
pm.collectionVariables.set("run_id", runId);

const ts = Date.now();
const v = pm.collectionVariables.get("api_version") || "v1";

pm.collectionVariables.set("topic_title", `QA Ideas Topic ${v} ${runId} ${ts}`);
pm.collectionVariables.set("topic_description", `Auto-generated topic for Ideas tests. run_id=${runId}, ts=${ts}`);

