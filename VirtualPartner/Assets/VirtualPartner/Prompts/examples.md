# StagePlan Format Examples

These examples are format references only. Do not copy the motion content, wording, stage count, action names, or rotation values unless the user asks for that exact action.

Parameter action format:
{"schemaVersion":"2.0","type":"stagePlan","metadata":{"intent":"format_reference_bone_pose","mood":"focused"},"stages":[{"actions":[{"type":"speech","text":"I will show a small gesture.","emotion":"focused","speed":1.0}]},{"actions":[{"type":"bonePose","duration":0.8,"bones":[{"bone":"UpperArm","side":"L","rotation":{"x":0,"y":45,"z":10}},{"bone":"Forearm","side":"L","rotation":{"x":0,"y":12,"z":0}},{"bone":"Head","side":"None","rotation":{"x":0,"y":4,"z":0}}]}]}]}

Preset action format:
{"schemaVersion":"2.0","type":"stagePlan","metadata":{"intent":"format_reference_preset","mood":"friendly"},"stages":[{"actions":[{"type":"speech","text":"Here is the preset action.","emotion":"friendly","speed":1.0},{"type":"animation","name":"Greet"}]}]}

Locomotion action format:
{"schemaVersion":"2.0","type":"stagePlan","metadata":{"intent":"format_reference_locomotion","mood":"active"},"stages":[{"actions":[{"type":"facing","target":"camera","duration":0.3}]},{"actions":[{"type":"locomotion","mode":"walk","duration":1.2},{"type":"speech","text":"I will move this way.","emotion":"active","speed":1.0}]}]}
