// 02_create_topic_pre.js
// Purpose: generate dynamic topic fields for request body variables.

const runId = pm.collectionVariables.get("run_id") || String(Date.now());
pm.collectionVariables.set("run_id", runId);

const ts = Date.now();
const apiVersion = pm.collectionVariables.get("api_version") || "v1";

pm.collectionVariables.set("topic_title", `QA Topics E2E ${apiVersion} ${runId} ${ts}`);
pm.collectionVariables.set("topic_description", `Auto-generated topic for integration tests. run_id=${runId}, ts=${ts}`);

