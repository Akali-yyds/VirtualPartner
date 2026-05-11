# Parameter Bone Rules

Prefer `bonePose` for normal gestures and posture control. Use registered `animation` only when the user clearly asks for a preset-like action such as scissors hand, shooting, or armed idle.

All rotations are target values, not deltas. Use small values first. Do not use real Unity bone names.

For side bones, describe the character's own side:
- `side:"L"` means Toki's left.
- `side:"R"` means Toki's right.
- Axis effects below are written for the left side. The runtime mirrors the right side; do not manually invert right-side values.

Use only bones, sides, axes, and ranges listed in the generated runtime capability list.

| Bone | Side | Safe range | X effect | Y effect | Z effect |
| --- | --- | --- | --- | --- | --- |
| Pelvis | none | -45..45 | lean body forward/back | twist hips left/right | tilt hips left/right |
| Spine | none | -45..45 | bend torso forward/back | twist torso left/right | side-bend torso |
| Chest | none | -45..45 | lift/lower upper torso | turn upper torso left/right | tilt shoulders |
| Neck | none | -45..45 | nod neck up/down | turn neck left/right | tilt neck |
| Head | none | -45..45 | nod head up/down | look left/right | tilt head |
| Clavicle | L/R | -90..90 | shoulder forward/back | shoulder up/down | roll shoulder |
| UpperArm | L/R | -90..90 | raise/lower arm forward/back | open/close arm sideways | roll upper arm |
| Forearm | L/R | -90..90 | bend/straighten elbow | swing forearm inward/outward | twist forearm |
| Hand | L/R | -90..90 | bend wrist up/down | wave wrist left/right | twist wrist |
| Thigh | L/R | -90..90 | lift/lower leg forward/back | open/close leg sideways | roll thigh |
| Calf | L/R | -90..90 | bend/straighten knee | minor knee side motion | minor knee twist |
| Foot | L/R | -90..90 | point/flex foot | turn foot inward/outward | roll ankle |
| Toe | L/R | -90..90 | curl/raise toe | minor toe side motion | minor toe roll |
| Eye | none | -30..30 | look up/down with both eyes | look left/right with both eyes | disabled |

Use `Eye` as one paired control. Do not add side for `Eye`.
