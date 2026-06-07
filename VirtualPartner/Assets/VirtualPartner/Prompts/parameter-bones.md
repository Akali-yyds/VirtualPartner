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
- A `roll(...)` effect twists the segment: it does not change where the limb points, but it rotates the segment's facing (for example the palm or wrist orientation). Use roll axes to orient hands, wrists, and palms.
- Single small rotations are the easiest to predict, but expressive gestures and two-hand poses normally need several axes at once (including roll) and larger angles. Combine axes and use bigger values when the pose requires it; do not avoid roll or multi-axis just because a single axis is simpler.

Hand and end-effector poses:
- For named precise gestures listed in Named Gestures, copy the listed rotations exactly instead of re-deriving them.
- For unlisted hand poses, think in two layers: use Clavicle/UpperArm/Forearm to place the hand, then use Forearm/Hand roll to orient the palm or wrist.
- The generated end-effector hints are approximate FK guidance, not IK targets. If a request requires both hands to meet at an exact point, prefer a listed named gesture when available.
- Hand/finger detail is limited: there are no finger bones. Represent hand shapes by wrist/hand orientation and arm placement.

Side policy:
- `side:"L"` means the character's left; `side:"R"` means the character's right.
- Runtime mirrors right-side controls internally. Do not manually invert right-side values.
- Use `side:"None"` only for bones listed as side=none.

Eye:
- `Eye` is one paired control. Do not add side. Use X/Y only; Z is disabled.
