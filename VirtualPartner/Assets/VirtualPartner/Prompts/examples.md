# Timeline Examples

Greeting:
{"schemaVersion":"1.0","timeline":[{"start":0.0,"end":1.8,"actions":[{"type":"speech","text":"你好，我在这里。"},{"type":"bonePose","bones":[{"bone":"Head","rotation":{"x":5,"y":0,"z":0}},{"bone":"UpperArm","side":"R","rotation":{"x":35,"y":15,"z":0}},{"bone":"Forearm","side":"R","rotation":{"x":35,"y":0,"z":0}}]}]}]}

Walk toward the user:
{"schemaVersion":"1.0","timeline":[{"start":0.0,"end":0.3,"actions":[{"type":"facing","target":"camera"}]},{"start":0.3,"end":2.0,"actions":[{"type":"locomotion","mode":"walk"},{"type":"speech","text":"我往前走两步。"}]}]}

Look left:
{"schemaVersion":"1.0","timeline":[{"start":0.0,"end":0.3,"actions":[{"type":"facing","target":"screenLeft"}]},{"start":0.3,"end":1.2,"actions":[{"type":"speech","text":"我看向左边。"}]}]}

Nod:
{"schemaVersion":"1.0","timeline":[{"start":0.0,"end":1.0,"actions":[{"type":"speech","text":"嗯，我明白。"},{"type":"bonePose","bones":[{"bone":"Head","rotation":{"x":12,"y":0,"z":0}},{"bone":"Neck","rotation":{"x":8,"y":0,"z":0}}]}]}]}

Preset action:
{"schemaVersion":"1.0","timeline":[{"start":0.0,"end":1.7,"actions":[{"type":"speech","text":"剪刀手。"},{"type":"animation","name":"ScissorsHandSingle"}]}]}
