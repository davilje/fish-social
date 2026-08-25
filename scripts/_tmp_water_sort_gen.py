import random
import json
from collections import Counter

CAPACITY = 4
COLOR_COUNT = 7
CONTENT_TUBES = 19
EMPTY = 2
SEED = 20260825

random.seed(SEED)

# 5 colors x 3 tubes + 2 colors x 2 tubes = 19
tube_counts = [3, 3, 3, 3, 3, 2, 2]
random.shuffle(tube_counts)
tubes_per_color = {c: tube_counts[c - 1] for c in range(1, COLOR_COUNT + 1)}

# flat pool: each color contributes tubes_per_color[c] * CAPACITY units
pool = []
for c, n in tubes_per_color.items():
    pool.extend([c] * (n * CAPACITY))
random.shuffle(pool)

tubes = []
idx = 0
for _ in range(CONTENT_TUBES):
    tubes.append(pool[idx : idx + CAPACITY])
    idx += CAPACITY

# add empty tubes
tubes.extend([[] for _ in range(EMPTY)])

assert idx == len(pool)
assert sum(1 for t in tubes if t) == CONTENT_TUBES
assert sum(1 for t in tubes if not t) == EMPTY
assert Counter(x for t in tubes for x in t) == {
    c: n * CAPACITY for c, n in tubes_per_color.items()
}

out = {
    "levelId": "gen-19f-2e-7c",
    "tubeCapacity": CAPACITY,
    "colorCount": COLOR_COUNT,
    "tubesPerColorWhenSolved": [tubes_per_color[c] for c in range(1, COLOR_COUNT + 1)],
    "emptyTubes": EMPTY,
    "tubes": tubes,
}

print(json.dumps(out, ensure_ascii=False, indent=2))
