# StagePlan 2.0 Rules

Return exactly one JSON object. Do not return Markdown, comments, or explanation.

Schema:
`{"schemaVersion":"2.0","type":"stagePlan","metadata":{"intent":"short_debug_intent","mood":"short_debug_mood"},"stages":[{"actions":[...]}]}`

`metadata` is optional debug information only. Use it for intent or mood if helpful. Do not output `characterId`; the runtime target character is chosen outside the JSON.

Supported actions:
- `speech`: show text in the target character's speech bubble.
- `bonePose`: set semantic bone target rotations.
- `animation`: call one registered preset animation by name.
- `facing`: turn the character root toward one target.
- `locomotion`: move along the character's current forward using walk/run.
- `expression`: set one registered lightweight expression until the current stage ends.

Stage execution:
- `stages` run in array order.
- Actions inside one stage start together.
- The next stage starts after every action in the current stage reaches a terminal result.
- A stage may contain at most one `speech` action.
- Use separate stages for ordered behavior.
- Use one stage for behavior that should happen at the same time, such as speech plus animation.

Action field shapes:
- `speech`: `{"type":"speech","text":"...","emotion":"happy","speed":1.0}`
- `expression`: `{"type":"expression","name":"smile","duration":0.3}`
- `bonePose`: `{"type":"bonePose","duration":0.8,"bones":[{"bone":"Head","rotation":{"x":0,"y":0,"z":0}}]}`
- `bonePose` with side: `{"type":"bonePose","duration":0.8,"bones":[{"bone":"UpperArm","side":"L","rotation":{"x":0,"y":0,"z":0}}]}`
- `animation`: `{"type":"animation","name":"ActionName"}`
- `facing`: `{"type":"facing","target":"camera","duration":0.3}`
- `locomotion`: `{"type":"locomotion","mode":"walk","duration":1.0}`

Hard rules:
- Never output keys: `timeline`, `start`, `end`, `stageId`, `steps`, `direction`, `keep`, `transition`, `transitionIn`, `transitionOut`, `transitionTime`, `targets`, `targetBones`, `boneTargets`.
- For `bonePose`, the array key must be exactly `bones`; never use `targets`, `target`, `poses`, `joints`, or `boneTargets`.
- Use only supported action fields. Unknown actions or unsupported values may be skipped by runtime.
- Do not invent character routing fields. The selected target character is supplied by runtime.
- Keep StagePlans short. Prefer one to five stages.

Composition rules:
- `facing` should usually be its own stage before locomotion.
- `locomotion` may be combined with `speech` when the character should speak while moving.
- Use `speech` when replying to the user, but keep text concise.
- Use `expression` lightly. Ordinary chat can be speech only; add expression only when emotion or intent clearly benefits, and only use expressions listed in Runtime Generated Capabilities.
- Do not combine a full-body preset animation with `bonePose` in the same stage.
- For sequential `bonePose` motions, each later stage should contain the complete desired pose for all bones that should remain active.
- To keep a previous pose, repeat the same bone rotations in the later stage.
- Do not reset a bone or the opposite side to zero unless the user asks to lower, release, reset, or return it to idle.
