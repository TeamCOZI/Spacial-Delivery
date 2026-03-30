# Assembly Cell Mapping Rules

## Core Rule
- Do not use distance-based checks for part placement, port connection, pipe connection, or transfer routing.
- Do not use `transform.position`, Euclidean distance, or nearest-object heuristics to decide whether something is connected.

## Source Of Truth
- Connection state must be derived only from Cell Mapping Info and port direction/type.
- A connection exists only when mapped cells and input/output sides actually match.
- If a Link exists, it is connected. If no Link exists, it is not connected.

## Split Pipe Rule
- `Split Pipe` output selection must be based only on the current split order and connected output sides.
- Skip directions that are not connected.
- Receiver selection must not use distance priority. Use only reachable mapped cells and matching port side.

## Placement Rule
- Future placement logic must not add distance-based decision rules.
- Placement validity must be determined only by cell occupancy, cell mapping, port type, port direction, and link compatibility.