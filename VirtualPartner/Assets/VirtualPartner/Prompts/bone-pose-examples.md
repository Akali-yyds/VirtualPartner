# Verified Bone Pose Examples

Visual: raise one arm upward. For both left and right arms, use positive `y` on `UpperArm`.
Action JSON:
`{"type":"bonePose","duration":0.8,"bones":[{"bone":"UpperArm","side":"L","rotation":{"x":0,"y":85,"z":0}},{"bone":"Forearm","side":"L","rotation":{"x":0,"y":15,"z":0}},{"bone":"Hand","side":"L","rotation":{"x":0,"y":0,"z":0}}]}`

Visual: extend one arm forward. For both left and right arms, use positive `z` on `UpperArm`.
Action JSON:
`{"type":"bonePose","duration":0.8,"bones":[{"bone":"UpperArm","side":"L","rotation":{"x":0,"y":0,"z":75}},{"bone":"Forearm","side":"L","rotation":{"x":0,"y":0,"z":10}},{"bone":"Hand","side":"L","rotation":{"x":0,"y":0,"z":0}}]}`

Visual: raise the left arm first, then raise the right arm while keeping the left arm raised.
StagePlan JSON:
`{"schemaVersion":"2.0","type":"stagePlan","metadata":{"intent":"raise_both_arms","mood":"active"},"stages":[{"actions":[{"type":"bonePose","duration":0.8,"bones":[{"bone":"UpperArm","side":"L","rotation":{"x":0,"y":85,"z":0}},{"bone":"Forearm","side":"L","rotation":{"x":0,"y":15,"z":0}}]}]},{"actions":[{"type":"bonePose","duration":0.8,"bones":[{"bone":"UpperArm","side":"L","rotation":{"x":0,"y":85,"z":0}},{"bone":"Forearm","side":"L","rotation":{"x":0,"y":15,"z":0}},{"bone":"UpperArm","side":"R","rotation":{"x":0,"y":85,"z":0}},{"bone":"Forearm","side":"R","rotation":{"x":0,"y":15,"z":0}}]}]}]}`

Visual: classic two-hand heart gesture ("heart hands" / "bixin"). Both arms are drawn in to the chest so the hands meet at the center of the chest and form a heart outline. Use the same semantic rotations for left and right (runtime mirrors the right side). Hands are the most distal controllable joint (no finger bones), so the heart shape is approximate.
Action JSON:
`{"type":"bonePose","duration":1,"bones":[{"bone":"Clavicle","side":"L","rotation":{"x":-15.399,"y":-5.817,"z":-34.221}},{"bone":"Clavicle","side":"R","rotation":{"x":-15.399,"y":-5.817,"z":-34.221}},{"bone":"UpperArm","side":"L","rotation":{"x":2.738,"y":-20.532,"z":29.772}},{"bone":"UpperArm","side":"R","rotation":{"x":2.738,"y":-20.532,"z":29.772}},{"bone":"Forearm","side":"L","rotation":{"x":38.669,"y":-78.707,"z":85.209}},{"bone":"Forearm","side":"R","rotation":{"x":38.669,"y":-78.707,"z":85.209}},{"bone":"Hand","side":"L","rotation":{"x":90,"y":-2.053,"z":-29.772}},{"bone":"Hand","side":"R","rotation":{"x":90,"y":-2.053,"z":-29.772}}]}`

Add only visually checked examples here. Use this template for future entries; do not treat the template as an example:

Visual: short description of the visible result.
Action JSON or StagePlan JSON:
`<paste verified JSON here>`
