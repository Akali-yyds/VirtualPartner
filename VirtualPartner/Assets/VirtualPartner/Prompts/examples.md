# Timeline Examples

Greeting:
{"schemaVersion":"1.0","timeline":[{"start":0.0,"end":1.8,"actions":[{"type":"speech","text":"你好，我在这里。"}]}]}

Walk toward the user:
{"schemaVersion":"1.0","timeline":[{"start":0.0,"end":0.3,"actions":[{"type":"facing","target":"camera"}]},{"start":0.3,"end":2.0,"actions":[{"type":"locomotion","mode":"walk"},{"type":"speech","text":"我往前走两步。"}]}]}

Walk one loop approximation:
{"schemaVersion":"1.0","timeline":[{"start":0.0,"end":0.3,"actions":[{"type":"facing","target":"screenRight"}]},{"start":0.3,"end":1.1,"actions":[{"type":"locomotion","mode":"walk"},{"type":"speech","text":"我绕着走一圈。"}]},{"start":1.1,"end":1.4,"actions":[{"type":"facing","target":"screenForward"}]},{"start":1.4,"end":2.2,"actions":[{"type":"locomotion","mode":"walk"}]},{"start":2.2,"end":2.5,"actions":[{"type":"facing","target":"screenLeft"}]},{"start":2.5,"end":3.3,"actions":[{"type":"locomotion","mode":"walk"}]},{"start":3.3,"end":3.6,"actions":[{"type":"facing","target":"screenBackward"}]},{"start":3.6,"end":4.4,"actions":[{"type":"locomotion","mode":"walk"}]}]}

Look left:
{"schemaVersion":"1.0","timeline":[{"start":0.0,"end":0.3,"actions":[{"type":"facing","target":"screenLeft"}]},{"start":0.3,"end":1.2,"actions":[{"type":"speech","text":"我看向左边。"}]}]}

Preset action:
{"schemaVersion":"1.0","timeline":[{"start":0.0,"end":1.7,"actions":[{"type":"speech","text":"剪刀手。"},{"type":"animation","name":"ScissorsHandSingle"}]}]}
