# Named Gesture Library

These are verified, ready-to-use `bonePose` values for specific named gestures
that are hard to derive from rotations alone, especially precise two-hand poses.

Rules:
- Use a gesture from this list only when the user explicitly asks for that exact gesture by name or by an unmistakable description.
- When you use one, copy its `bones` rotations exactly as written. Do not change the rotation numbers.
- Left and right values are intentionally identical for symmetric gestures; runtime mirrors the right side internally.
- You may wrap the gesture in your own `speech`, `expression`, `facing`, or additional stages.
- Do not reuse these values, angles, or bone sets for any other request. They are not general motion templates.
- If no listed gesture matches the request, ignore this section and generate `bonePose` normally from Runtime Generated Capabilities.

## heart_hands / bixin / two-hand heart

Aliases and descriptions: heart hands, two-hand heart, finger heart made with both hands, hands meet at the chest to form a heart outline.

Both hands are drawn in to the chest so they meet at the center of the chest and form a heart outline. Hands are the most distal controllable joint because this rig has no finger bones, so the heart shape is approximate. Use this only for an explicit heart-hands request.

Action JSON to place inside a StagePlan stage:
`{"type":"bonePose","duration":1,"bones":[{"bone":"Clavicle","side":"L","rotation":{"x":-15.399,"y":-5.817,"z":-34.221}},{"bone":"Clavicle","side":"R","rotation":{"x":-15.399,"y":-5.817,"z":-34.221}},{"bone":"UpperArm","side":"L","rotation":{"x":2.738,"y":-20.532,"z":29.772}},{"bone":"UpperArm","side":"R","rotation":{"x":2.738,"y":-20.532,"z":29.772}},{"bone":"Forearm","side":"L","rotation":{"x":38.669,"y":-78.707,"z":85.209}},{"bone":"Forearm","side":"R","rotation":{"x":38.669,"y":-78.707,"z":85.209}},{"bone":"Hand","side":"L","rotation":{"x":90,"y":-2.053,"z":-29.772}},{"bone":"Hand","side":"R","rotation":{"x":90,"y":-2.053,"z":-29.772}}]}`

Minimal StagePlan:
`{"schemaVersion":"2.0","type":"stagePlan","metadata":{"intent":"heart_hands","mood":"warm"},"stages":[{"actions":[{"type":"bonePose","duration":1,"bones":[{"bone":"Clavicle","side":"L","rotation":{"x":-15.399,"y":-5.817,"z":-34.221}},{"bone":"Clavicle","side":"R","rotation":{"x":-15.399,"y":-5.817,"z":-34.221}},{"bone":"UpperArm","side":"L","rotation":{"x":2.738,"y":-20.532,"z":29.772}},{"bone":"UpperArm","side":"R","rotation":{"x":2.738,"y":-20.532,"z":29.772}},{"bone":"Forearm","side":"L","rotation":{"x":38.669,"y":-78.707,"z":85.209}},{"bone":"Forearm","side":"R","rotation":{"x":38.669,"y":-78.707,"z":85.209}},{"bone":"Hand","side":"L","rotation":{"x":90,"y":-2.053,"z":-29.772}},{"bone":"Hand","side":"R","rotation":{"x":90,"y":-2.053,"z":-29.772}}]}]}]}`
