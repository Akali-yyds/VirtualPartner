# StagePlan Examples

Greeting:
{"schemaVersion":"2.0","type":"stagePlan","metadata":{"intent":"greeting","mood":"friendly"},"stages":[{"actions":[{"type":"speech","text":"Hello, I am here.","emotion":"friendly","speed":1.0},{"type":"animation","name":"Greet"}]}]}

Walk toward the user:
{"schemaVersion":"2.0","type":"stagePlan","metadata":{"intent":"walk_to_user","mood":"focused"},"stages":[{"actions":[{"type":"facing","target":"camera","duration":0.3}]},{"actions":[{"type":"locomotion","mode":"walk","duration":1.4},{"type":"speech","text":"I will walk toward you.","emotion":"focused","speed":1.0}]}]}

Walk one loop approximation:
{"schemaVersion":"2.0","type":"stagePlan","metadata":{"intent":"walk_loop","mood":"active"},"stages":[{"actions":[{"type":"facing","target":"screenRight","duration":0.3}]},{"actions":[{"type":"locomotion","mode":"walk","duration":0.8},{"type":"speech","text":"I will make a small loop.","emotion":"active","speed":1.0}]},{"actions":[{"type":"facing","target":"screenForward","duration":0.3}]},{"actions":[{"type":"locomotion","mode":"walk","duration":0.8}]},{"actions":[{"type":"facing","target":"screenLeft","duration":0.3}]},{"actions":[{"type":"locomotion","mode":"walk","duration":0.8}]},{"actions":[{"type":"facing","target":"screenBackward","duration":0.3}]},{"actions":[{"type":"locomotion","mode":"walk","duration":0.8}]}]}

Look left:
{"schemaVersion":"2.0","type":"stagePlan","metadata":{"intent":"look_left","mood":"attentive"},"stages":[{"actions":[{"type":"facing","target":"screenLeft","duration":0.3}]},{"actions":[{"type":"speech","text":"I am looking left.","emotion":"attentive","speed":1.0}]}]}

Preset action:
{"schemaVersion":"2.0","type":"stagePlan","metadata":{"intent":"peace_sign","mood":"playful"},"stages":[{"actions":[{"type":"speech","text":"Peace sign.","emotion":"playful","speed":1.0},{"type":"animation","name":"ScissorsHandSingle"}]}]}
