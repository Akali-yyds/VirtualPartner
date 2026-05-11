# Locomotion And Facing

`locomotion` has no direction or steps. It always moves along Toki's current forward. To choose a movement direction, place a separate `facing` segment before locomotion.

Facing targets:
- `camera`: turn toward the user/camera position.
- `screenLeft`: face toward the left side of the screen.
- `screenRight`: face toward the right side of the screen.
- `screenForward`: face into the screen, away from the user, deeper into the scene.
- `screenBackward`: face toward the screen outside direction; prefer `camera` when the meaning is "toward me".

Chinese direction mapping:
- "往前", "向前", "走过来", "靠近我", "朝我走": use `facing:"camera"` then `locomotion:"walk"`.
- "跑过来", "快点过来", "冲过来": use `facing:"camera"` then `locomotion:"run"`.
- "往画面深处", "往远处", "背对我走", "往房间里面走": use `facing:"screenForward"` then walk/run.
- "向左看", "向左转": use `facing:"screenLeft"`.
- "向右看", "向右转": use `facing:"screenRight"`.

Use `walk` for normal movement. Use `run` only when the user clearly asks for fast movement.
