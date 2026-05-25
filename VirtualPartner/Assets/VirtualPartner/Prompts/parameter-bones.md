# Parameter Bone Rules

Prefer `bonePose` for generated gestures, posture, dance, and expressive body motion. Use registered `animation` only when the user clearly asks for that preset-like action.

Rotations:
- Values are targets relative to BaseRotation, not deltas.
- Zero means return that bone to BaseRotation/Idle; do not use zero as a placeholder for a pose that should be preserved.
- Use only semantic bone names, sides, enabled axes, and ranges from Runtime Generated Capabilities.
- Do not use real Unity bone paths.

Axis and movement method:
- Axis directions describe local rotation axes, not movement targets.
- Use the generated primary/effects lines to choose the axis whose positive or negative rotation moves the visible segment toward the intended direction.
- If an effect says `twist/no swing`, that axis mostly twists and should not be used to raise or extend the body part.
- Small single-axis rotations are most predictable. Combine axes conservatively.

Side policy:
- `side:"L"` means the character's left; `side:"R"` means the character's right.
- Runtime mirrors right-side controls internally. Do not manually invert right-side values.
- Use `side:"None"` only for bones listed as side=none.

Eye:
- `Eye` is one paired control. Do not add side. Use X/Y only; Z is disabled.
