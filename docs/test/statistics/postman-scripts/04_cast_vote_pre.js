// 04_cast_vote_pre.js
// Purpose: sanity-check required vars exist (idea1_id, seed_token).

pm.variables.set("__idea1_id_present", !!pm.collectionVariables.get("idea1_id"));
pm.variables.set("__seed_token_present", !!pm.collectionVariables.get("seed_token"));

