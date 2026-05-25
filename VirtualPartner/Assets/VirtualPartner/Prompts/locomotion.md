# Locomotion And Facing

`locomotion` has no direction or steps. It always moves along the character's current forward. Use a separate `facing` stage first when direction matters.

Facing targets:
- `camera`: face the user/camera.
- `screenLeft`: face toward the left side of the screen.
- `screenRight`: face toward the right side of the screen.
- `screenForward`: face deeper into the scene, away from the user.
- `screenBackward`: face toward the screen outside direction; prefer `camera` when the meaning is "toward me".

Movement rules:
- Use `walk` for normal movement and `run` only when the user clearly asks for fast movement.
- For path-like requests, decompose into several short straight locomotion stages, each preceded by `facing`.
- Do not use locomotion as a substitute for dancing unless the user asks to move around the room.
