// 02_create_topic_pre.js
// Purpose: generate dynamic topic fields for a topic that will hold ideas/votes for statistics.

const runId = pm.collectionVariables.get("run_id") || String(Date.now());
pm.collectionVariables.set("run_id", runId);

const ts = Date.now();
const v = pm.collectionVariables.get("api_version") || "v1";

pm.collectionVariables.set("topic_title", `QA Statistics Topic ${v} ${runId} ${ts}`);
pm.collectionVariables.set("topic_description", `Auto-generated topic for Statistics tests. run_id=${runId}, ts=${ts}`);

