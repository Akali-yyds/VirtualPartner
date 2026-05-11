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

Hard rules:
- Every segment needs `start`, `end`, and `actions`.
- `end` must be greater than `start`.
- Segments must not overlap.
- Runtime does not auto-fill timing, direction, or facing.
- Never output keys: `steps`, `direction`, `keep`, `transition`, `transitionIn`, `transitionOut`, `transitionTime`.
- Use only supported action fields. Unknown actions or unsupported values may be skipped.
- Keep timelines short. Prefer 0.2-0.4s for facing, 1-3s for simple poses, and natural clip duration for preset animations.

Composition rules:
- `facing` must be the only action in its segment.
- `locomotion` may be mixed only with `speech`.
- Use `speech` when replying to the user, but keep text concise.
- Do not combine a full-body preset animation with `bonePose` in the same segment.
