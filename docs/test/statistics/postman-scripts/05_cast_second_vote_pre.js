// 05_cast_second_vote_pre.js
// Purpose:
// - Create idea #2 under the same topic (via pm.sendRequest) and store `idea2_id`
// - Ensure the current request (POST /votes) can cast a vote for idea #2 using {{idea2_id}}

function asBool(v) {
  return String(v || "").toLowerCase() === "true";
}

function incAttempts() {
  const current = parseInt(pm.collectionVariables.get("stats_second_idea_setup_attempts") || "0", 10);
  const next = current + 1;
  pm.collectionVariables.set("stats_second_idea_setup_attempts", String(next));
  return next;
}

const base = pm.collectionVariables.get("base_url");
const v = pm.collectionVariables.get("api_version") || "v1";
const token = pm.collectionVariables.get("seed_token");
const topicId = pm.collectionVariables.get("topic_id");

// Preconditions: if missing, let request run and fail in Tests.
if (!base || !token || !topicId) {
  pm.collectionVariables.set("stats_second_idea_setup_done", "true");
}

const done = asBool(pm.collectionVariables.get("stats_second_idea_setup_done"));
const inProgress = asBool(pm.collectionVariables.get("stats_second_idea_setup_in_progress"));

if (done) return;

const attempts = incAttempts();
if (attempts > 25) {
  pm.collectionVariables.set("stats_second_idea_setup_done", "true");
  return;
}

function createIdea2Once() {
  if (inProgress) return;
  pm.collectionVariables.set("stats_second_idea_setup_in_progress", "true");

  const runId = pm.collectionVariables.get("run_id") || String(Date.now());
  pm.collectionVariables.set("run_id", runId);

  const ts = Date.now();
  // Prefix with "B" so when votes tie, idea #1 sorts before idea #2 by title.
  const title = `B QA Statistics Idea ${v} ${runId} ${ts}`;
  pm.collectionVariables.set("idea2_title", title);
  pm.collectionVariables.set("idea2_description", `Auto-generated idea #2 for Statistics tests. run_id=${runId}, ts=${ts}`);

  pm.sendRequest({
    url: `${base}/api/${v}/ideas`,
    method: "POST",
    header: {
      "Content-Type": "application/json",
      "Authorization": `Bearer ${token}`
    },
    body: {
      mode: "raw",
      raw: JSON.stringify({
        topicId: topicId,
        title: title,
        description: pm.collectionVariables.get("idea2_description")
      })
    }
  }, function (err, res) {
    if (err || !res) {
      pm.collectionVariables.set("stats_second_idea_setup_in_progress", "false");
      return;
    }
    if (res.code !== 201) {
      pm.collectionVariables.set("stats_second_idea_setup_in_progress", "false");
      return;
    }

    const json = res.json();
    pm.collectionVariables.set("idea2_id", String(json.id));
    pm.collectionVariables.set("stats_second_idea_setup_done", "true");
    pm.collectionVariables.set("stats_second_idea_setup_in_progress", "false");
  });
}

createIdea2Once();

pm.variables.set("__idea2_id_present", !!pm.collectionVariables.get("idea2_id"));
pm.variables.set("__seed_token_present", !!pm.collectionVariables.get("seed_token"));

