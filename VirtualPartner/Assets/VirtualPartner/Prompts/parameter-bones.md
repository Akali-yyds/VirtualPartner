# Parameter Bone Rules

Prefer `bonePose` for normal gestures and posture control. Use registered `animation` only when the user clearly asks for a preset-like action such as scissors hand, shooting, or armed idle.

All rotations are target values relative to BaseRotation, not deltas. Do not use real Unity bone names.
Zero rotation means return that bone to BaseRotation/Idle. Do not use zero as a placeholder for bones that should keep their previous pose.

Think of each controllable bone as a local coordinate frame captured from the Base/T pose. The generated runtime capability list provides each bone's `+X`, `+Y`, and `+Z` axis directions in character space:
- `Right` means Toki's right.
- `Up` means Toki's upward direction.
- `Forward` means Toki's facing direction.
- A negative sign means the opposite direction.

Axis directions describe rotation axes, not movement targets. Do not choose `z` just because `+Z` points upward. To move a limb or body part, use the generated primary-direction effects first.

Positive rotation direction:
- `+X` rotation moves the `+Y` axis toward the `+Z` axis.
- `+Y` rotation moves the `+Z` axis toward the `+X` axis.
- `+Z` rotation moves the `+X` axis toward the `+Y` axis.
- Negative values rotate in the opposite direction.

These rules are most reliable for small single-axis rotations. When multiple axes are combined, the final pose depends on quaternion/Euler composition and the skeleton hierarchy.

For limb and body pose planning:
- Read `primary` as the visible segment direction controlled by that bone in the Base pose.
- Read `effects` as where that visible segment moves for one positive or negative single-axis rotation.
- Prefer the `effects` entry that moves `primary` toward the requested visual direction.
- If an effect says `twist/no swing`, that axis mostly twists around the segment and will not raise or extend it.

For side bones, describe the character's own side:
- `side:"L"` means Toki's left.
- `side:"R"` means Toki's right.
- Axis directions and effects are listed for the left side. Use the same semantic values for the right side; the runtime mirrors the right side internally. Do not manually invert right-side values.

Use only bones, sides, axes, and ranges listed in the generated runtime capability list.

Use `Eye` as one paired control. Do not add side for `Eye`. `Eye` supports X/Y only; Z is disabled.
