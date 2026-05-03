// 04_cast_vote_pre.js
// Purpose: sanity-check required vars exist (idea_id, seed_token).

pm.variables.set("__idea_id_present", !!pm.collectionVariables.get("idea_id"));
pm.variables.set("__seed_token_present", !!pm.collectionVariables.get("seed_token"));

