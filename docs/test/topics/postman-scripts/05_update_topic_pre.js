// 05_update_topic_pre.js
// Purpose:
// - generate dynamic updated title/description for the PUT body
// - deterministically seed 2 ideas + 1 vote (via pm.sendRequest) so closing computes winningIdea
//
// Strategy:
// - If setup isn't done, we start seeding once and then repeatedly skip this request
//   until `topic_setup_done=true` is set by the async callbacks.

function asBool(v) {
  return String(v || "").toLowerCase() === "true";
}

function incAttempts() {
  const current = parseInt(pm.collectionVariables.get("topic_setup_attempts") || "0", 10);
  const next = current + 1;
  pm.collectionVariables.set("topic_setup_attempts", String(next));
  return next;
}

const runId = pm.collectionVariables.get("run_id") || String(Date.now());
pm.collectionVariables.set("run_id", runId);

const topicId = pm.collectionVariables.get("topic_id");
const token = pm.collectionVariables.get("seed_token");
const baseUrl = pm.collectionVariables.get("base_url");
const v = pm.collectionVariables.get("api_version") || "v1";

// Update body variables (request body uses these).
const ts = Date.now();
pm.collectionVariables.set("topic_title_updated", `QA Topics Updated ${v} ${runId} ${ts}`);
pm.collectionVariables.set("topic_description_updated", `Updated by automation. run_id=${runId}, ts=${ts}`);
pm.collectionVariables.set("expected_topic_status", "CLOSED");

// Preconditions
if (!topicId || !token || !baseUrl) {
  // Let the request run and fail loudly in Tests (misconfigured collection).
  pm.collectionVariables.set("topic_setup_done", "true");
}

const setupDone = asBool(pm.collectionVariables.get("topic_setup_done"));
const setupInProgress = asBool(pm.collectionVariables.get("topic_setup_in_progress"));

if (setupDone) {
  // Setup already complete; allow PUT request to run.
  // Ensure we still have the expected winning idea id.
  if (!pm.collectionVariables.get("expected_winning_idea_id") && pm.collectionVariables.get("idea1_id")) {
    pm.collectionVariables.set("expected_winning_idea_id", pm.collectionVariables.get("idea1_id"));
  }
  return;
}

// Prevent infinite loops if something is badly broken.
const attempts = incAttempts();
if (attempts > 25) {
  pm.collectionVariables.set("topic_setup_done", "true");
  return;
}

function seedIdeasAndVotesOnce() {
  if (setupInProgress) return;
  pm.collectionVariables.set("topic_setup_in_progress", "true");

  const idea1Title = `Idea A ${v} ${runId} ${Date.now()}`;
  const idea2Title = `Idea B ${v} ${runId} ${Date.now()}`;

  const createIdea = (title, cb) => {
    pm.sendRequest({
      url: `${baseUrl}/api/${v}/ideas`,
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
          description: `Seeded idea for winningIdea computation. title=${title}`
        })
      }
    }, cb);
  };

  createIdea(idea1Title, function (err1, res1) {
    if (err1 || !res1) {
      pm.collectionVariables.set("topic_setup_error", `Failed creating idea1: ${String(err1)}`);
      pm.collectionVariables.set("topic_setup_in_progress", "false");
      return;
    }
    if (res1.code !== 201) {
      pm.collectionVariables.set("topic_setup_error", `Failed creating idea1, status=${res1.code}`);
      pm.collectionVariables.set("topic_setup_in_progress", "false");
      return;
    }

    const j1 = res1.json();
    pm.collectionVariables.set("idea1_id", String(j1.id));

    createIdea(idea2Title, function (err2, res2) {
      if (err2 || !res2) {
        pm.collectionVariables.set("topic_setup_error", `Failed creating idea2: ${String(err2)}`);
        pm.collectionVariables.set("topic_setup_in_progress", "false");
        return;
      }
      if (res2.code !== 201) {
        pm.collectionVariables.set("topic_setup_error", `Failed creating idea2, status=${res2.code}`);
        pm.collectionVariables.set("topic_setup_in_progress", "false");
        return;
      }

      const j2 = res2.json();
      pm.collectionVariables.set("idea2_id", String(j2.id));

      // Cast exactly 1 vote for idea1 (VoteService prevents duplicate votes by same user on same idea).
      pm.sendRequest({
        url: `${baseUrl}/api/${v}/votes`,
        method: "POST",
        header: {
          "Content-Type": "application/json",
          "Authorization": `Bearer ${token}`
        },
        body: {
          mode: "raw",
          raw: JSON.stringify({ ideaId: j1.id })
        }
      }, function (err3, res3) {
        if (err3 || !res3) {
          pm.collectionVariables.set("topic_setup_error", `Failed casting vote: ${String(err3)}`);
          pm.collectionVariables.set("topic_setup_in_progress", "false");
          return;
        }

        if (res3.code === 201) {
          const j3 = res3.json();
          pm.collectionVariables.set("vote1_id", String(j3.id));
        } else if (res3.code === 409) {
          // Duplicate vote is not expected in a clean run, but tolerate to avoid blocking the flow.
          pm.collectionVariables.set("vote1_id", "");
        } else {
          pm.collectionVariables.set("topic_setup_error", `Vote status=${res3.code}`);
          pm.collectionVariables.set("topic_setup_in_progress", "false");
          return;
        }

        pm.collectionVariables.set("expected_winning_idea_id", String(j1.id));
        pm.collectionVariables.set("topic_setup_done", "true");
        pm.collectionVariables.set("topic_setup_in_progress", "false");
      });
    });
  });
}

seedIdeasAndVotesOnce();

