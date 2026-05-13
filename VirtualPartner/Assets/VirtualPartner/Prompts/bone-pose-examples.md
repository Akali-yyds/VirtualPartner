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

Add only visually checked examples here. Use this template for future entries; do not treat the template as an example:

Visual: short description of the visible result.
Action JSON or StagePlan JSON:
`<paste verified JSON here>`
