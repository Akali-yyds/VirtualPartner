# StagePlan 2.0 Rules

Return exactly one JSON object. Do not return Markdown, comments, or explanation.

Root schema:
`{"schemaVersion":"2.0","type":"stagePlan","metadata":{"intent":"short_intent","mood":"short_mood"},"stages":[{"actions":[...]}]}`

`metadata` is optional debug context. Do not output `characterId`; runtime selects the target character.

Supported actions:
- `speech`: visible character speech bubble.
- `expression`: registered expression for the current stage.
- `bonePose`: semantic bone target rotations.
- `animation`: one registered preset animation.
- `facing`: turn the character root toward a target.
- `locomotion`: walk/run along current forward.

Stage execution:
- `stages` run in array order.
- Actions in one stage start together.
- The next stage waits until every current-stage action reaches a terminal result.
- A stage may contain at most one `speech`.
- Use separate stages for ordered beats and one stage for simultaneous actions.

Hard rules:
- Never output keys: `timeline`, `start`, `end`, `stageId`, `steps`, `direction`, `keep`, `transition`, `transitionIn`, `transitionOut`, `transitionTime`, `targets`, `targetBones`, `boneTargets`.
- For `bonePose`, the array key must be exactly `bones`; never use `targets`, `target`, `poses`, `joints`, or `boneTargets`.
- Use only names and values present in Runtime Generated Capabilities.
- Use `speech` when replying to the user, but keep it concise.
- Ordinary chat and simple gestures should stay short.
- For explicit dance, long performance, or full routine requests, prefer 8 to 12 readable stages with several generated `bonePose` phases.

Composition guidance:
- `facing` should usually be its own stage before `locomotion`.
- `locomotion` may combine with `speech` when the character speaks while moving.
- Use `expression` lightly and only with registered expression names.
- Do not combine a full-body preset `animation` with `bonePose` in the same stage.
- For sequential `bonePose`, repeat any bone rotations that should remain active. Do not reset a bone to zero unless the user asks to lower, release, reset, or return it to idle.

Dance / performance guidance:
- Do not satisfy dance requests with `Greet`, scissors hand, or unrelated preset animations. Current presets are not dance routines.
- Use generated `bonePose` phases for preparation, arm phrase, torso/head accent, leg/step accent when available, variation, flourish, and final settle.
- Most dance movement stages should last about 0.7 to 1.2 seconds so the motion is readable.
- Include chest/head/arms together when possible so motion reads as body performance instead of isolated hands.
- Use legs conservatively; small thigh/calf shifts can imply rhythm. Do not rely on locomotion for dancing unless the user asks to move around the room.
