// 08_update_vote_pre.js
// Purpose:
// - create a second idea under the same topic to move the vote to
// - set collection var `new_idea_id` for the PUT body
//
// Strategy:
// - If setup isn't done, create new idea via pm.sendRequest() and repeatedly skip this request
//   until `vote_move_setup_done=true`.

function asBool(v) {
  return String(v || "").toLowerCase() === "true";
}

function incAttempts() {
  const current = parseInt(pm.collectionVariables.get("vote_move_setup_attempts") || "0", 10);
  const next = current + 1;
  pm.collectionVariables.set("vote_move_setup_attempts", String(next));
  return next;
}

const base = pm.collectionVariables.get("base_url");
const v = pm.collectionVariables.get("api_version") || "v1";
const token = pm.collectionVariables.get("seed_token");
const topicId = pm.collectionVariables.get("topic_id");

// Preconditions: if missing, let request run and fail in Tests.
if (!base || !token || !topicId) {
  pm.collectionVariables.set("vote_move_setup_done", "true");
}

const done = asBool(pm.collectionVariables.get("vote_move_setup_done"));
const inProgress = asBool(pm.collectionVariables.get("vote_move_setup_in_progress"));

if (done) return;

const attempts = incAttempts();
if (attempts > 25) {
  pm.collectionVariables.set("vote_move_setup_done", "true");
  return;
}

function createSecondIdeaOnce() {
  if (inProgress) return;
  pm.collectionVariables.set("vote_move_setup_in_progress", "true");

  const runId = pm.collectionVariables.get("run_id") || String(Date.now());
  pm.collectionVariables.set("run_id", runId);

  const ts = Date.now();
  const title = `QA Vote Move Target ${v} ${runId} ${ts}`;
  pm.collectionVariables.set("new_idea_title", title);

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
        description: `Second idea created to move vote. run_id=${runId}, ts=${ts}`
      })
    }
  }, function (err, res) {
    if (err || !res) {
      pm.collectionVariables.set("vote_move_setup_error", `Failed creating new idea: ${String(err)}`);
      pm.collectionVariables.set("vote_move_setup_in_progress", "false");
      return;
    }
    if (res.code !== 201) {
      pm.collectionVariables.set("vote_move_setup_error", `Failed creating new idea, status=${res.code}`);
      pm.collectionVariables.set("vote_move_setup_in_progress", "false");
      return;
    }

    const json = res.json();
    pm.collectionVariables.set("new_idea_id", String(json.id));
    pm.collectionVariables.set("vote_move_setup_done", "true");
    pm.collectionVariables.set("vote_move_setup_in_progress", "false");
  });
}

createSecondIdeaOnce();

