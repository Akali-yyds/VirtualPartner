# Timeline Rules

Return exactly one JSON object. Do not return Markdown, comments, or explanation.

Schema:
`{"schemaVersion":"1.0","timeline":[{"start":0.0,"end":1.0,"actions":[...]}]}`

Supported actions:
- `speech`: show text in Toki's speech bubble.
- `bonePose`: set semantic bone target rotations.
- `animation`: call one registered preset animation by name.
- `facing`: turn Toki's root toward one target.
- `locomotion`: move along Toki's current forward using walk/run.

Before writing JSON, decompose the user's request into supported action primitives:
- First infer the intended visible outcome, then choose the smallest supported actions that can create it.
- If one primitive cannot express the full outcome, compose multiple segments instead of dropping the missing part.
- Do not invent unsupported fields to express missing details. Use only supported actions and fields.
- If exact behavior is unsupported but an approximation is possible with existing primitives, output that approximation.

Action field shapes:
- `speech`: `{"type":"speech","text":"..."}`
- `bonePose`: `{"type":"bonePose","bones":[{"bone":"Head","rotation":{"x":0,"y":0,"z":0}}]}`
- `bonePose` with side: `{"type":"bonePose","bones":[{"bone":"UpperArm","side":"L","rotation":{"x":0,"y":0,"z":0}}]}`
- `animation`: `{"type":"animation","name":"ActionName"}`
- `facing`: `{"type":"facing","target":"camera"}`
- `locomotion`: `{"type":"locomotion","mode":"walk"}`

Hard rules:
- Every segment needs `start`, `end`, and `actions`.
- `end` must be greater than `start`.
- Segments must not overlap.
- Runtime does not auto-fill timing, direction, or facing.
- For `bonePose`, the array key must be exactly `bones`; never use `targets`, `target`, `poses`, `joints`, or `boneTargets`.
- Never output keys: `steps`, `direction`, `keep`, `transition`, `transitionIn`, `transitionOut`, `transitionTime`, `targets`, `targetBones`, `boneTargets`.
- Use only supported action fields. Unknown actions or unsupported values may be skipped.
- Keep timelines short. Prefer 0.2-0.4s for facing, 1-3s for simple poses, and natural clip duration for preset animations.

Composition rules:
- `facing` must be the only action in its segment.
- `locomotion` may be mixed only with `speech`.
- Use `speech` when replying to the user, but keep text concise.
- Do not combine a full-body preset animation with `bonePose` in the same segment.
- For sequential `bonePose` motions, each segment should contain the complete desired pose for all bones that should remain active.
- To keep a previous pose, repeat the same bone rotations in the later segment.
- Do not reset a bone or the opposite side to zero unless the user asks to lower, release, reset, or return it to idle.
