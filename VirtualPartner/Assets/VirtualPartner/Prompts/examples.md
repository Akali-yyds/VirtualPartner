# Timeline Examples

Greeting:
{"schemaVersion":"1.0","timeline":[{"start":0.0,"end":1.8,"actions":[{"type":"speech","text":"你好，我在这里。"}]}]}

Walk toward the user:
{"schemaVersion":"1.0","timeline":[{"start":0.0,"end":0.3,"actions":[{"type":"facing","target":"camera"}]},{"start":0.3,"end":2.0,"actions":[{"type":"locomotion","mode":"walk"},{"type":"speech","text":"我往前走两步。"}]}]}

Look left:
{"schemaVersion":"1.0","timeline":[{"start":0.0,"end":0.3,"actions":[{"type":"facing","target":"screenLeft"}]},{"start":0.3,"end":1.2,"actions":[{"type":"speech","text":"我看向左边。"}]}]}

Preset action:
{"schemaVersion":"1.0","timeline":[{"start":0.0,"end":1.7,"actions":[{"type":"speech","text":"剪刀手。"},{"type":"animation","name":"ScissorsHandSingle"}]}]}
