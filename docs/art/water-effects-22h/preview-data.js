window.waterPreviewData = {
  "effects": {
    "schemaVersion": 2,
    "artVersion": "22h-water-v2.1",
    "logicalCanvas": [
      128,
      64
    ],
    "anchor": [
      64,
      44
    ],
    "logicalSlot": 128,
    "variantSelection": "stable-event-key-plus-occurrence-parity",
    "sizeThresholdsMeters": [
      0.3,
      0.8,
      1.5
    ],
    "visibleBodyThresholdsPixels": [
      16,
      32,
      56
    ],
    "unknownSizeTier": "S",
    "objectSizeTier": "S",
    "tiers": {
      "S": {
        "width": 24,
        "height": 8,
        "durationMs": 280,
        "dropCount": 2,
        "arcCount": 1,
        "crownCount": 1,
        "spawnCount": 4
      },
      "M": {
        "width": 40,
        "height": 15,
        "durationMs": 360,
        "dropCount": 3,
        "arcCount": 2,
        "crownCount": 1,
        "spawnCount": 6
      },
      "L": {
        "width": 60,
        "height": 24,
        "durationMs": 480,
        "dropCount": 4,
        "arcCount": 2,
        "crownCount": 2,
        "spawnCount": 8
      },
      "XL": {
        "width": 84,
        "height": 35,
        "durationMs": 620,
        "dropCount": 6,
        "arcCount": 2,
        "crownCount": 2,
        "spawnCount": 10
      }
    },
    "cues": [
      {
        "id": "FX01",
        "cue": "float.contact",
        "anchor": "bobber.contact",
        "sizeTier": "object",
        "durationMs": 320,
        "spawnCount": 3,
        "priority": 65,
        "reducedVariant": "arcs",
        "allowedPhases": [
          "casting",
          "waiting"
        ],
        "cooldownMs": 0
      },
      {
        "id": "FX02",
        "cue": "waiting.ripple",
        "anchor": "bobber.contact",
        "sizeTier": "object",
        "durationMs": 850,
        "spawnCount": 1,
        "priority": 10,
        "reducedVariant": "none",
        "allowedPhases": [
          "waiting"
        ],
        "cooldownMs": 2400
      },
      {
        "id": "FX03",
        "cue": "bite.onset",
        "anchor": "fish.contact",
        "sizeTier": "shared",
        "durationMs": 0,
        "spawnCount": 0,
        "priority": 100,
        "reducedVariant": "arcs",
        "allowedPhases": [
          "hooked",
          "effort"
        ],
        "cooldownMs": 0
      },
      {
        "id": "FX04",
        "cue": "fish.struggle",
        "anchor": "fish.contact",
        "sizeTier": "shared",
        "durationMs": 0,
        "spawnCount": 0,
        "priority": 60,
        "reducedVariant": "none",
        "allowedPhases": [
          "hooked",
          "effort"
        ],
        "cooldownMs": 700
      },
      {
        "id": "FX05",
        "cue": "fish.surface",
        "markers": [
          "fish.surface",
          "fish.slap"
        ],
        "anchor": "fish.contact",
        "sizeTier": "shared",
        "durationMs": 0,
        "spawnCount": 0,
        "priority": 90,
        "reducedVariant": "arcs",
        "allowedPhases": [
          "hooked",
          "effort"
        ],
        "cooldownMs": 0,
        "requiresSourceMarker": true,
        "requiresContact": true
      },
      {
        "id": "FX08",
        "cue": "groundbait.contact",
        "anchor": "bait.contact",
        "sizeTier": "object",
        "durationMs": 280,
        "spawnCount": 3,
        "priority": 50,
        "reducedVariant": "none",
        "allowedPhases": [
          "groundbaiting"
        ],
        "cooldownMs": 720
      }
    ],
    "budget": {
      "instances": 24,
      "groups": 8,
      "groupsPerActor": 2,
      "selfReservedGroups": 2,
      "selfReservedInstances": 12,
      "maxDecodedBytes": 4194304
    },
    "reduced": {
      "durationMs": 200,
      "arcCount": 2,
      "dropCount": 0
    },
    "endedFadeMs": 100,
    "withdrawn": [
      "FX06",
      "FX07",
      "FX09"
    ],
    "assets": [
      {
        "id": "ripple-s-a",
        "file": "ripple-s-a.png",
        "kind": "ripple",
        "sizeTier": "S",
        "variant": "a",
        "layer": "back",
        "anchor": [
          64,
          44
        ],
        "width": 128,
        "height": 64
      },
      {
        "id": "splash-s-a",
        "file": "splash-s-a.png",
        "kind": "splash",
        "sizeTier": "S",
        "variant": "a",
        "layer": "back",
        "anchor": [
          64,
          44
        ],
        "width": 128,
        "height": 64
      },
      {
        "id": "ripple-s-b",
        "file": "ripple-s-b.png",
        "kind": "ripple",
        "sizeTier": "S",
        "variant": "b",
        "layer": "back",
        "anchor": [
          64,
          44
        ],
        "width": 128,
        "height": 64
      },
      {
        "id": "splash-s-b",
        "file": "splash-s-b.png",
        "kind": "splash",
        "sizeTier": "S",
        "variant": "b",
        "layer": "back",
        "anchor": [
          64,
          44
        ],
        "width": 128,
        "height": 64
      },
      {
        "id": "ripple-m-a",
        "file": "ripple-m-a.png",
        "kind": "ripple",
        "sizeTier": "M",
        "variant": "a",
        "layer": "back",
        "anchor": [
          64,
          44
        ],
        "width": 128,
        "height": 64
      },
      {
        "id": "splash-m-a",
        "file": "splash-m-a.png",
        "kind": "splash",
        "sizeTier": "M",
        "variant": "a",
        "layer": "back",
        "anchor": [
          64,
          44
        ],
        "width": 128,
        "height": 64
      },
      {
        "id": "ripple-m-b",
        "file": "ripple-m-b.png",
        "kind": "ripple",
        "sizeTier": "M",
        "variant": "b",
        "layer": "back",
        "anchor": [
          64,
          44
        ],
        "width": 128,
        "height": 64
      },
      {
        "id": "splash-m-b",
        "file": "splash-m-b.png",
        "kind": "splash",
        "sizeTier": "M",
        "variant": "b",
        "layer": "back",
        "anchor": [
          64,
          44
        ],
        "width": 128,
        "height": 64
      },
      {
        "id": "ripple-l-a",
        "file": "ripple-l-a.png",
        "kind": "ripple",
        "sizeTier": "L",
        "variant": "a",
        "layer": "back",
        "anchor": [
          64,
          44
        ],
        "width": 128,
        "height": 64
      },
      {
        "id": "splash-l-a",
        "file": "splash-l-a.png",
        "kind": "splash",
        "sizeTier": "L",
        "variant": "a",
        "layer": "back",
        "anchor": [
          64,
          44
        ],
        "width": 128,
        "height": 64
      },
      {
        "id": "ripple-l-b",
        "file": "ripple-l-b.png",
        "kind": "ripple",
        "sizeTier": "L",
        "variant": "b",
        "layer": "back",
        "anchor": [
          64,
          44
        ],
        "width": 128,
        "height": 64
      },
      {
        "id": "splash-l-b",
        "file": "splash-l-b.png",
        "kind": "splash",
        "sizeTier": "L",
        "variant": "b",
        "layer": "back",
        "anchor": [
          64,
          44
        ],
        "width": 128,
        "height": 64
      },
      {
        "id": "ripple-xl-a",
        "file": "ripple-xl-a.png",
        "kind": "ripple",
        "sizeTier": "XL",
        "variant": "a",
        "layer": "back",
        "anchor": [
          64,
          44
        ],
        "width": 128,
        "height": 64
      },
      {
        "id": "splash-xl-a",
        "file": "splash-xl-a.png",
        "kind": "splash",
        "sizeTier": "XL",
        "variant": "a",
        "layer": "back",
        "anchor": [
          64,
          44
        ],
        "width": 128,
        "height": 64
      },
      {
        "id": "ripple-xl-b",
        "file": "ripple-xl-b.png",
        "kind": "ripple",
        "sizeTier": "XL",
        "variant": "b",
        "layer": "back",
        "anchor": [
          64,
          44
        ],
        "width": 128,
        "height": 64
      },
      {
        "id": "splash-xl-b",
        "file": "splash-xl-b.png",
        "kind": "splash",
        "sizeTier": "XL",
        "variant": "b",
        "layer": "back",
        "anchor": [
          64,
          44
        ],
        "width": 128,
        "height": 64
      },
      {
        "id": "drop-round",
        "file": "drop-round.png",
        "kind": "drop",
        "layer": "front",
        "anchor": [
          32,
          44
        ],
        "width": 64,
        "height": 64
      },
      {
        "id": "drop-short",
        "file": "drop-short.png",
        "kind": "drop",
        "layer": "front",
        "anchor": [
          32,
          44
        ],
        "width": 64,
        "height": 64
      }
    ]
  },
  "pets": {
    "black": {
      "version": 2,
      "sourceVersion": 19,
      "canvas": [
        640,
        512
      ],
      "layers": [
        "body",
        "head",
        "far_hand",
        "rod",
        "line",
        "fish",
        "hands",
        "bobber",
        "bait"
      ],
      "bodyLayers": [
        "body",
        "head",
        "far_hand",
        "hands"
      ],
      "place": {
        "foot": [
          264,
          450
        ],
        "bodyHeight": 257,
        "reference": 512,
        "referenceFoot": [
          138.5,
          404
        ],
        "referenceBody": 279
      },
      "maskModel": "pose-contours-v1",
      "sourceFrameCount": 63,
      "sourceManifestSha256": "083866a4e7c2a1128f5aa52fb00b93b03fa610bce88546711ea68e0bea6b4190",
      "clips": {
        "idle": {
          "playback": "hold",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            }
          ]
        },
        "sit": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            },
            {
              "clip": "sit",
              "index": 1,
              "atlasIndex": 1
            },
            {
              "clip": "sit",
              "index": 2,
              "atlasIndex": 2
            },
            {
              "clip": "sit",
              "index": 3,
              "atlasIndex": 3
            }
          ],
          "nextClip": "ready"
        },
        "ready": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            180
          ],
          "sourceFrames": [
            {
              "clip": "ready",
              "index": 0,
              "atlasIndex": 4
            }
          ]
        },
        "bait": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            220,
            220,
            260,
            300
          ],
          "sourceFrames": [
            {
              "clip": "bait",
              "index": 0,
              "atlasIndex": 5
            },
            {
              "clip": "bait",
              "index": 1,
              "atlasIndex": 6
            },
            {
              "clip": "bait",
              "index": 2,
              "atlasIndex": 7
            },
            {
              "clip": "bait",
              "index": 3,
              "atlasIndex": 8
            }
          ]
        },
        "cast": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "cast",
              "index": 0,
              "atlasIndex": 9
            },
            {
              "clip": "cast",
              "index": 1,
              "atlasIndex": 10
            },
            {
              "clip": "cast",
              "index": 2,
              "atlasIndex": 11
            },
            {
              "clip": "cast",
              "index": 3,
              "atlasIndex": 12
            }
          ]
        },
        "fishing": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            500
          ],
          "sourceFrames": [
            {
              "clip": "wait",
              "index": 0,
              "atlasIndex": 13
            }
          ]
        },
        "bite": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            }
          ],
          "nextClip": "effort"
        },
        "effort": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 0
        },
        "hooked": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160,
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            },
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 4
        },
        "reel": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve",
              "index": 0,
              "atlasIndex": 24
            },
            {
              "clip": "retrieve",
              "index": 1,
              "atlasIndex": 25
            },
            {
              "clip": "retrieve",
              "index": 2,
              "atlasIndex": 26
            },
            {
              "clip": "retrieve",
              "index": 3,
              "atlasIndex": 27
            },
            {
              "clip": "retrieve",
              "index": 4,
              "atlasIndex": 28
            },
            {
              "clip": "retrieve",
              "index": 5,
              "atlasIndex": 29
            }
          ],
          "nextClip": "catch"
        },
        "reel_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve_empty",
              "index": 0,
              "atlasIndex": 30
            },
            {
              "clip": "retrieve_empty",
              "index": 1,
              "atlasIndex": 31
            },
            {
              "clip": "retrieve_empty",
              "index": 2,
              "atlasIndex": 32
            },
            {
              "clip": "retrieve_empty",
              "index": 3,
              "atlasIndex": 33
            },
            {
              "clip": "retrieve_empty",
              "index": 4,
              "atlasIndex": 34
            },
            {
              "clip": "retrieve_empty",
              "index": 5,
              "atlasIndex": 35
            }
          ],
          "nextClip": "reset_empty"
        },
        "catch": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            160,
            160,
            180,
            360
          ],
          "sourceFrames": [
            {
              "clip": "catch",
              "index": 0,
              "atlasIndex": 36
            },
            {
              "clip": "catch",
              "index": 1,
              "atlasIndex": 37
            },
            {
              "clip": "catch",
              "index": 2,
              "atlasIndex": 38
            },
            {
              "clip": "catch",
              "index": 3,
              "atlasIndex": 39
            },
            {
              "clip": "catch",
              "index": 4,
              "atlasIndex": 40
            },
            {
              "clip": "catch",
              "index": 5,
              "atlasIndex": 41
            },
            {
              "clip": "catch",
              "index": 6,
              "atlasIndex": 42
            }
          ],
          "nextClip": "reset_catch"
        },
        "reset_catch": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_catch",
              "index": 0,
              "atlasIndex": 43
            },
            {
              "clip": "reset_catch",
              "index": 1,
              "atlasIndex": 44
            },
            {
              "clip": "reset_catch",
              "index": 2,
              "atlasIndex": 45
            },
            {
              "clip": "reset_catch",
              "index": 3,
              "atlasIndex": 46
            }
          ],
          "nextClip": "ready"
        },
        "reset_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_empty",
              "index": 0,
              "atlasIndex": 47
            },
            {
              "clip": "reset_empty",
              "index": 1,
              "atlasIndex": 48
            },
            {
              "clip": "reset_empty",
              "index": 2,
              "atlasIndex": 49
            },
            {
              "clip": "reset_empty",
              "index": 3,
              "atlasIndex": 50
            }
          ],
          "nextClip": "ready"
        },
        "groundbait": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            120,
            120,
            120,
            120,
            160
          ],
          "sourceFrames": [
            {
              "clip": "groundbait",
              "index": 0,
              "atlasIndex": 51
            },
            {
              "clip": "groundbait",
              "index": 1,
              "atlasIndex": 52
            },
            {
              "clip": "groundbait",
              "index": 2,
              "atlasIndex": 53
            },
            {
              "clip": "groundbait",
              "index": 3,
              "atlasIndex": 54
            },
            {
              "clip": "groundbait",
              "index": 4,
              "atlasIndex": 55
            },
            {
              "clip": "groundbait",
              "index": 5,
              "atlasIndex": 56
            },
            {
              "clip": "groundbait",
              "index": 6,
              "atlasIndex": 57
            },
            {
              "clip": "groundbait",
              "index": 7,
              "atlasIndex": 58
            }
          ],
          "nextClip": "ready"
        },
        "leave": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "leave",
              "index": 0,
              "atlasIndex": 59
            },
            {
              "clip": "leave",
              "index": 1,
              "atlasIndex": 60
            },
            {
              "clip": "leave",
              "index": 2,
              "atlasIndex": 61
            },
            {
              "clip": "leave",
              "index": 3,
              "atlasIndex": 62
            }
          ],
          "nextClip": "idle"
        }
      },
      "sourceToRuntime": {
        "wait": "fishing",
        "retrieve": "reel",
        "retrieve_empty": "reel_empty",
        "hooked": "bite + effort (loopStart=4)"
      },
      "flows": {
        "success": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "hooked",
          "reel",
          "catch",
          "reset_catch",
          "ready"
        ],
        "empty": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "reel_empty",
          "reset_empty",
          "ready"
        ]
      },
      "skinId": "black",
      "skinName": "黑猫",
      "skinKind": "painted-pose-assets-v4",
      "artworkManifestSha256": "9920481664879b6779b3396f33a346362de70c27aae984ead935444da2b91a83",
      "rodOcclusionModel": "head-layer-order-v1",
      "rodSourceManifestSha256": "a0b5789d160385569fd981174082ece49f4ec1b8e3cbdef5b67b1d509c8858ec"
    },
    "blue": {
      "version": 2,
      "sourceVersion": 19,
      "canvas": [
        640,
        512
      ],
      "layers": [
        "body",
        "head",
        "far_hand",
        "rod",
        "line",
        "fish",
        "hands",
        "bobber",
        "bait"
      ],
      "bodyLayers": [
        "body",
        "head",
        "far_hand",
        "hands"
      ],
      "place": {
        "foot": [
          264,
          450
        ],
        "bodyHeight": 257,
        "reference": 512,
        "referenceFoot": [
          138.5,
          404
        ],
        "referenceBody": 279
      },
      "maskModel": "pose-contours-v1",
      "sourceFrameCount": 63,
      "sourceManifestSha256": "083866a4e7c2a1128f5aa52fb00b93b03fa610bce88546711ea68e0bea6b4190",
      "clips": {
        "idle": {
          "playback": "hold",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            }
          ]
        },
        "sit": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            },
            {
              "clip": "sit",
              "index": 1,
              "atlasIndex": 1
            },
            {
              "clip": "sit",
              "index": 2,
              "atlasIndex": 2
            },
            {
              "clip": "sit",
              "index": 3,
              "atlasIndex": 3
            }
          ],
          "nextClip": "ready"
        },
        "ready": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            180
          ],
          "sourceFrames": [
            {
              "clip": "ready",
              "index": 0,
              "atlasIndex": 4
            }
          ]
        },
        "bait": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            220,
            220,
            260,
            300
          ],
          "sourceFrames": [
            {
              "clip": "bait",
              "index": 0,
              "atlasIndex": 5
            },
            {
              "clip": "bait",
              "index": 1,
              "atlasIndex": 6
            },
            {
              "clip": "bait",
              "index": 2,
              "atlasIndex": 7
            },
            {
              "clip": "bait",
              "index": 3,
              "atlasIndex": 8
            }
          ]
        },
        "cast": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "cast",
              "index": 0,
              "atlasIndex": 9
            },
            {
              "clip": "cast",
              "index": 1,
              "atlasIndex": 10
            },
            {
              "clip": "cast",
              "index": 2,
              "atlasIndex": 11
            },
            {
              "clip": "cast",
              "index": 3,
              "atlasIndex": 12
            }
          ]
        },
        "fishing": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            500
          ],
          "sourceFrames": [
            {
              "clip": "wait",
              "index": 0,
              "atlasIndex": 13
            }
          ]
        },
        "bite": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            }
          ],
          "nextClip": "effort"
        },
        "effort": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 0
        },
        "hooked": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160,
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            },
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 4
        },
        "reel": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve",
              "index": 0,
              "atlasIndex": 24
            },
            {
              "clip": "retrieve",
              "index": 1,
              "atlasIndex": 25
            },
            {
              "clip": "retrieve",
              "index": 2,
              "atlasIndex": 26
            },
            {
              "clip": "retrieve",
              "index": 3,
              "atlasIndex": 27
            },
            {
              "clip": "retrieve",
              "index": 4,
              "atlasIndex": 28
            },
            {
              "clip": "retrieve",
              "index": 5,
              "atlasIndex": 29
            }
          ],
          "nextClip": "catch"
        },
        "reel_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve_empty",
              "index": 0,
              "atlasIndex": 30
            },
            {
              "clip": "retrieve_empty",
              "index": 1,
              "atlasIndex": 31
            },
            {
              "clip": "retrieve_empty",
              "index": 2,
              "atlasIndex": 32
            },
            {
              "clip": "retrieve_empty",
              "index": 3,
              "atlasIndex": 33
            },
            {
              "clip": "retrieve_empty",
              "index": 4,
              "atlasIndex": 34
            },
            {
              "clip": "retrieve_empty",
              "index": 5,
              "atlasIndex": 35
            }
          ],
          "nextClip": "reset_empty"
        },
        "catch": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            160,
            160,
            180,
            360
          ],
          "sourceFrames": [
            {
              "clip": "catch",
              "index": 0,
              "atlasIndex": 36
            },
            {
              "clip": "catch",
              "index": 1,
              "atlasIndex": 37
            },
            {
              "clip": "catch",
              "index": 2,
              "atlasIndex": 38
            },
            {
              "clip": "catch",
              "index": 3,
              "atlasIndex": 39
            },
            {
              "clip": "catch",
              "index": 4,
              "atlasIndex": 40
            },
            {
              "clip": "catch",
              "index": 5,
              "atlasIndex": 41
            },
            {
              "clip": "catch",
              "index": 6,
              "atlasIndex": 42
            }
          ],
          "nextClip": "reset_catch"
        },
        "reset_catch": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_catch",
              "index": 0,
              "atlasIndex": 43
            },
            {
              "clip": "reset_catch",
              "index": 1,
              "atlasIndex": 44
            },
            {
              "clip": "reset_catch",
              "index": 2,
              "atlasIndex": 45
            },
            {
              "clip": "reset_catch",
              "index": 3,
              "atlasIndex": 46
            }
          ],
          "nextClip": "ready"
        },
        "reset_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_empty",
              "index": 0,
              "atlasIndex": 47
            },
            {
              "clip": "reset_empty",
              "index": 1,
              "atlasIndex": 48
            },
            {
              "clip": "reset_empty",
              "index": 2,
              "atlasIndex": 49
            },
            {
              "clip": "reset_empty",
              "index": 3,
              "atlasIndex": 50
            }
          ],
          "nextClip": "ready"
        },
        "groundbait": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            120,
            120,
            120,
            120,
            160
          ],
          "sourceFrames": [
            {
              "clip": "groundbait",
              "index": 0,
              "atlasIndex": 51
            },
            {
              "clip": "groundbait",
              "index": 1,
              "atlasIndex": 52
            },
            {
              "clip": "groundbait",
              "index": 2,
              "atlasIndex": 53
            },
            {
              "clip": "groundbait",
              "index": 3,
              "atlasIndex": 54
            },
            {
              "clip": "groundbait",
              "index": 4,
              "atlasIndex": 55
            },
            {
              "clip": "groundbait",
              "index": 5,
              "atlasIndex": 56
            },
            {
              "clip": "groundbait",
              "index": 6,
              "atlasIndex": 57
            },
            {
              "clip": "groundbait",
              "index": 7,
              "atlasIndex": 58
            }
          ],
          "nextClip": "ready"
        },
        "leave": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "leave",
              "index": 0,
              "atlasIndex": 59
            },
            {
              "clip": "leave",
              "index": 1,
              "atlasIndex": 60
            },
            {
              "clip": "leave",
              "index": 2,
              "atlasIndex": 61
            },
            {
              "clip": "leave",
              "index": 3,
              "atlasIndex": 62
            }
          ],
          "nextClip": "idle"
        }
      },
      "sourceToRuntime": {
        "wait": "fishing",
        "retrieve": "reel",
        "retrieve_empty": "reel_empty",
        "hooked": "bite + effort (loopStart=4)"
      },
      "flows": {
        "success": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "hooked",
          "reel",
          "catch",
          "reset_catch",
          "ready"
        ],
        "empty": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "reel_empty",
          "reset_empty",
          "ready"
        ]
      },
      "skinId": "blue",
      "skinName": "英短蓝猫",
      "skinKind": "painted-pose-assets-v4",
      "artworkManifestSha256": "b10860a29c292710eed729babfa97903233cfd4056f77cc3811f2222aa49c402",
      "rodOcclusionModel": "head-layer-order-v1",
      "rodSourceManifestSha256": "a0b5789d160385569fd981174082ece49f4ec1b8e3cbdef5b67b1d509c8858ec"
    },
    "brown": {
      "version": 2,
      "sourceVersion": 19,
      "canvas": [
        640,
        512
      ],
      "layers": [
        "body",
        "head",
        "far_hand",
        "rod",
        "line",
        "fish",
        "hands",
        "bobber",
        "bait"
      ],
      "bodyLayers": [
        "body",
        "head",
        "far_hand",
        "hands"
      ],
      "place": {
        "foot": [
          264,
          450
        ],
        "bodyHeight": 257,
        "reference": 512,
        "referenceFoot": [
          138.5,
          404
        ],
        "referenceBody": 279
      },
      "maskModel": "pose-contours-v1",
      "sourceFrameCount": 63,
      "sourceManifestSha256": "083866a4e7c2a1128f5aa52fb00b93b03fa610bce88546711ea68e0bea6b4190",
      "clips": {
        "idle": {
          "playback": "hold",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            }
          ]
        },
        "sit": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            },
            {
              "clip": "sit",
              "index": 1,
              "atlasIndex": 1
            },
            {
              "clip": "sit",
              "index": 2,
              "atlasIndex": 2
            },
            {
              "clip": "sit",
              "index": 3,
              "atlasIndex": 3
            }
          ],
          "nextClip": "ready"
        },
        "ready": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            180
          ],
          "sourceFrames": [
            {
              "clip": "ready",
              "index": 0,
              "atlasIndex": 4
            }
          ]
        },
        "bait": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            220,
            220,
            260,
            300
          ],
          "sourceFrames": [
            {
              "clip": "bait",
              "index": 0,
              "atlasIndex": 5
            },
            {
              "clip": "bait",
              "index": 1,
              "atlasIndex": 6
            },
            {
              "clip": "bait",
              "index": 2,
              "atlasIndex": 7
            },
            {
              "clip": "bait",
              "index": 3,
              "atlasIndex": 8
            }
          ]
        },
        "cast": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "cast",
              "index": 0,
              "atlasIndex": 9
            },
            {
              "clip": "cast",
              "index": 1,
              "atlasIndex": 10
            },
            {
              "clip": "cast",
              "index": 2,
              "atlasIndex": 11
            },
            {
              "clip": "cast",
              "index": 3,
              "atlasIndex": 12
            }
          ]
        },
        "fishing": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            500
          ],
          "sourceFrames": [
            {
              "clip": "wait",
              "index": 0,
              "atlasIndex": 13
            }
          ]
        },
        "bite": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            }
          ],
          "nextClip": "effort"
        },
        "effort": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 0
        },
        "hooked": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160,
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            },
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 4
        },
        "reel": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve",
              "index": 0,
              "atlasIndex": 24
            },
            {
              "clip": "retrieve",
              "index": 1,
              "atlasIndex": 25
            },
            {
              "clip": "retrieve",
              "index": 2,
              "atlasIndex": 26
            },
            {
              "clip": "retrieve",
              "index": 3,
              "atlasIndex": 27
            },
            {
              "clip": "retrieve",
              "index": 4,
              "atlasIndex": 28
            },
            {
              "clip": "retrieve",
              "index": 5,
              "atlasIndex": 29
            }
          ],
          "nextClip": "catch"
        },
        "reel_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve_empty",
              "index": 0,
              "atlasIndex": 30
            },
            {
              "clip": "retrieve_empty",
              "index": 1,
              "atlasIndex": 31
            },
            {
              "clip": "retrieve_empty",
              "index": 2,
              "atlasIndex": 32
            },
            {
              "clip": "retrieve_empty",
              "index": 3,
              "atlasIndex": 33
            },
            {
              "clip": "retrieve_empty",
              "index": 4,
              "atlasIndex": 34
            },
            {
              "clip": "retrieve_empty",
              "index": 5,
              "atlasIndex": 35
            }
          ],
          "nextClip": "reset_empty"
        },
        "catch": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            160,
            160,
            180,
            360
          ],
          "sourceFrames": [
            {
              "clip": "catch",
              "index": 0,
              "atlasIndex": 36
            },
            {
              "clip": "catch",
              "index": 1,
              "atlasIndex": 37
            },
            {
              "clip": "catch",
              "index": 2,
              "atlasIndex": 38
            },
            {
              "clip": "catch",
              "index": 3,
              "atlasIndex": 39
            },
            {
              "clip": "catch",
              "index": 4,
              "atlasIndex": 40
            },
            {
              "clip": "catch",
              "index": 5,
              "atlasIndex": 41
            },
            {
              "clip": "catch",
              "index": 6,
              "atlasIndex": 42
            }
          ],
          "nextClip": "reset_catch"
        },
        "reset_catch": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_catch",
              "index": 0,
              "atlasIndex": 43
            },
            {
              "clip": "reset_catch",
              "index": 1,
              "atlasIndex": 44
            },
            {
              "clip": "reset_catch",
              "index": 2,
              "atlasIndex": 45
            },
            {
              "clip": "reset_catch",
              "index": 3,
              "atlasIndex": 46
            }
          ],
          "nextClip": "ready"
        },
        "reset_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_empty",
              "index": 0,
              "atlasIndex": 47
            },
            {
              "clip": "reset_empty",
              "index": 1,
              "atlasIndex": 48
            },
            {
              "clip": "reset_empty",
              "index": 2,
              "atlasIndex": 49
            },
            {
              "clip": "reset_empty",
              "index": 3,
              "atlasIndex": 50
            }
          ],
          "nextClip": "ready"
        },
        "groundbait": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            120,
            120,
            120,
            120,
            160
          ],
          "sourceFrames": [
            {
              "clip": "groundbait",
              "index": 0,
              "atlasIndex": 51
            },
            {
              "clip": "groundbait",
              "index": 1,
              "atlasIndex": 52
            },
            {
              "clip": "groundbait",
              "index": 2,
              "atlasIndex": 53
            },
            {
              "clip": "groundbait",
              "index": 3,
              "atlasIndex": 54
            },
            {
              "clip": "groundbait",
              "index": 4,
              "atlasIndex": 55
            },
            {
              "clip": "groundbait",
              "index": 5,
              "atlasIndex": 56
            },
            {
              "clip": "groundbait",
              "index": 6,
              "atlasIndex": 57
            },
            {
              "clip": "groundbait",
              "index": 7,
              "atlasIndex": 58
            }
          ],
          "nextClip": "ready"
        },
        "leave": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "leave",
              "index": 0,
              "atlasIndex": 59
            },
            {
              "clip": "leave",
              "index": 1,
              "atlasIndex": 60
            },
            {
              "clip": "leave",
              "index": 2,
              "atlasIndex": 61
            },
            {
              "clip": "leave",
              "index": 3,
              "atlasIndex": 62
            }
          ],
          "nextClip": "idle"
        }
      },
      "sourceToRuntime": {
        "wait": "fishing",
        "retrieve": "reel",
        "retrieve_empty": "reel_empty",
        "hooked": "bite + effort (loopStart=4)"
      },
      "flows": {
        "success": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "hooked",
          "reel",
          "catch",
          "reset_catch",
          "ready"
        ],
        "empty": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "reel_empty",
          "reset_empty",
          "ready"
        ]
      },
      "skinId": "brown",
      "skinName": "棕虎斑",
      "skinKind": "painted-pose-assets-v4",
      "artworkManifestSha256": "5ff1bb2aaf6218a5130537c0a039a4eb0b898c35070059492cca27f245f68117",
      "rodOcclusionModel": "head-layer-order-v1",
      "rodSourceManifestSha256": "a0b5789d160385569fd981174082ece49f4ec1b8e3cbdef5b67b1d509c8858ec"
    },
    "calico": {
      "version": 2,
      "sourceVersion": 19,
      "canvas": [
        640,
        512
      ],
      "layers": [
        "body",
        "head",
        "far_hand",
        "rod",
        "line",
        "fish",
        "hands",
        "bobber",
        "bait"
      ],
      "bodyLayers": [
        "body",
        "head",
        "far_hand",
        "hands"
      ],
      "place": {
        "foot": [
          264,
          450
        ],
        "bodyHeight": 257,
        "reference": 512,
        "referenceFoot": [
          138.5,
          404
        ],
        "referenceBody": 279
      },
      "maskModel": "pose-contours-v1",
      "sourceFrameCount": 63,
      "sourceManifestSha256": "083866a4e7c2a1128f5aa52fb00b93b03fa610bce88546711ea68e0bea6b4190",
      "clips": {
        "idle": {
          "playback": "hold",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            }
          ]
        },
        "sit": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            },
            {
              "clip": "sit",
              "index": 1,
              "atlasIndex": 1
            },
            {
              "clip": "sit",
              "index": 2,
              "atlasIndex": 2
            },
            {
              "clip": "sit",
              "index": 3,
              "atlasIndex": 3
            }
          ],
          "nextClip": "ready"
        },
        "ready": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            180
          ],
          "sourceFrames": [
            {
              "clip": "ready",
              "index": 0,
              "atlasIndex": 4
            }
          ]
        },
        "bait": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            220,
            220,
            260,
            300
          ],
          "sourceFrames": [
            {
              "clip": "bait",
              "index": 0,
              "atlasIndex": 5
            },
            {
              "clip": "bait",
              "index": 1,
              "atlasIndex": 6
            },
            {
              "clip": "bait",
              "index": 2,
              "atlasIndex": 7
            },
            {
              "clip": "bait",
              "index": 3,
              "atlasIndex": 8
            }
          ]
        },
        "cast": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "cast",
              "index": 0,
              "atlasIndex": 9
            },
            {
              "clip": "cast",
              "index": 1,
              "atlasIndex": 10
            },
            {
              "clip": "cast",
              "index": 2,
              "atlasIndex": 11
            },
            {
              "clip": "cast",
              "index": 3,
              "atlasIndex": 12
            }
          ]
        },
        "fishing": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            500
          ],
          "sourceFrames": [
            {
              "clip": "wait",
              "index": 0,
              "atlasIndex": 13
            }
          ]
        },
        "bite": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            }
          ],
          "nextClip": "effort"
        },
        "effort": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 0
        },
        "hooked": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160,
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            },
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 4
        },
        "reel": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve",
              "index": 0,
              "atlasIndex": 24
            },
            {
              "clip": "retrieve",
              "index": 1,
              "atlasIndex": 25
            },
            {
              "clip": "retrieve",
              "index": 2,
              "atlasIndex": 26
            },
            {
              "clip": "retrieve",
              "index": 3,
              "atlasIndex": 27
            },
            {
              "clip": "retrieve",
              "index": 4,
              "atlasIndex": 28
            },
            {
              "clip": "retrieve",
              "index": 5,
              "atlasIndex": 29
            }
          ],
          "nextClip": "catch"
        },
        "reel_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve_empty",
              "index": 0,
              "atlasIndex": 30
            },
            {
              "clip": "retrieve_empty",
              "index": 1,
              "atlasIndex": 31
            },
            {
              "clip": "retrieve_empty",
              "index": 2,
              "atlasIndex": 32
            },
            {
              "clip": "retrieve_empty",
              "index": 3,
              "atlasIndex": 33
            },
            {
              "clip": "retrieve_empty",
              "index": 4,
              "atlasIndex": 34
            },
            {
              "clip": "retrieve_empty",
              "index": 5,
              "atlasIndex": 35
            }
          ],
          "nextClip": "reset_empty"
        },
        "catch": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            160,
            160,
            180,
            360
          ],
          "sourceFrames": [
            {
              "clip": "catch",
              "index": 0,
              "atlasIndex": 36
            },
            {
              "clip": "catch",
              "index": 1,
              "atlasIndex": 37
            },
            {
              "clip": "catch",
              "index": 2,
              "atlasIndex": 38
            },
            {
              "clip": "catch",
              "index": 3,
              "atlasIndex": 39
            },
            {
              "clip": "catch",
              "index": 4,
              "atlasIndex": 40
            },
            {
              "clip": "catch",
              "index": 5,
              "atlasIndex": 41
            },
            {
              "clip": "catch",
              "index": 6,
              "atlasIndex": 42
            }
          ],
          "nextClip": "reset_catch"
        },
        "reset_catch": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_catch",
              "index": 0,
              "atlasIndex": 43
            },
            {
              "clip": "reset_catch",
              "index": 1,
              "atlasIndex": 44
            },
            {
              "clip": "reset_catch",
              "index": 2,
              "atlasIndex": 45
            },
            {
              "clip": "reset_catch",
              "index": 3,
              "atlasIndex": 46
            }
          ],
          "nextClip": "ready"
        },
        "reset_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_empty",
              "index": 0,
              "atlasIndex": 47
            },
            {
              "clip": "reset_empty",
              "index": 1,
              "atlasIndex": 48
            },
            {
              "clip": "reset_empty",
              "index": 2,
              "atlasIndex": 49
            },
            {
              "clip": "reset_empty",
              "index": 3,
              "atlasIndex": 50
            }
          ],
          "nextClip": "ready"
        },
        "groundbait": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            120,
            120,
            120,
            120,
            160
          ],
          "sourceFrames": [
            {
              "clip": "groundbait",
              "index": 0,
              "atlasIndex": 51
            },
            {
              "clip": "groundbait",
              "index": 1,
              "atlasIndex": 52
            },
            {
              "clip": "groundbait",
              "index": 2,
              "atlasIndex": 53
            },
            {
              "clip": "groundbait",
              "index": 3,
              "atlasIndex": 54
            },
            {
              "clip": "groundbait",
              "index": 4,
              "atlasIndex": 55
            },
            {
              "clip": "groundbait",
              "index": 5,
              "atlasIndex": 56
            },
            {
              "clip": "groundbait",
              "index": 6,
              "atlasIndex": 57
            },
            {
              "clip": "groundbait",
              "index": 7,
              "atlasIndex": 58
            }
          ],
          "nextClip": "ready"
        },
        "leave": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "leave",
              "index": 0,
              "atlasIndex": 59
            },
            {
              "clip": "leave",
              "index": 1,
              "atlasIndex": 60
            },
            {
              "clip": "leave",
              "index": 2,
              "atlasIndex": 61
            },
            {
              "clip": "leave",
              "index": 3,
              "atlasIndex": 62
            }
          ],
          "nextClip": "idle"
        }
      },
      "sourceToRuntime": {
        "wait": "fishing",
        "retrieve": "reel",
        "retrieve_empty": "reel_empty",
        "hooked": "bite + effort (loopStart=4)"
      },
      "flows": {
        "success": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "hooked",
          "reel",
          "catch",
          "reset_catch",
          "ready"
        ],
        "empty": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "reel_empty",
          "reset_empty",
          "ready"
        ]
      },
      "skinId": "calico",
      "skinName": "三花猫",
      "skinKind": "painted-pose-assets-v3",
      "artworkManifestSha256": "eea22815249230f93e3e6155ec26ace6e1ea5638e77cf504acfbd5db40e40edb",
      "rodOcclusionModel": "head-layer-order-v1",
      "rodSourceManifestSha256": "a0b5789d160385569fd981174082ece49f4ec1b8e3cbdef5b67b1d509c8858ec"
    },
    "cow": {
      "version": 2,
      "sourceVersion": 19,
      "canvas": [
        640,
        512
      ],
      "layers": [
        "body",
        "head",
        "far_hand",
        "rod",
        "line",
        "fish",
        "hands",
        "bobber",
        "bait"
      ],
      "bodyLayers": [
        "body",
        "head",
        "far_hand",
        "hands"
      ],
      "place": {
        "foot": [
          264,
          450
        ],
        "bodyHeight": 257,
        "reference": 512,
        "referenceFoot": [
          138.5,
          404
        ],
        "referenceBody": 279
      },
      "maskModel": "pose-contours-v1",
      "sourceFrameCount": 63,
      "sourceManifestSha256": "083866a4e7c2a1128f5aa52fb00b93b03fa610bce88546711ea68e0bea6b4190",
      "clips": {
        "idle": {
          "playback": "hold",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            }
          ]
        },
        "sit": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            },
            {
              "clip": "sit",
              "index": 1,
              "atlasIndex": 1
            },
            {
              "clip": "sit",
              "index": 2,
              "atlasIndex": 2
            },
            {
              "clip": "sit",
              "index": 3,
              "atlasIndex": 3
            }
          ],
          "nextClip": "ready"
        },
        "ready": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            180
          ],
          "sourceFrames": [
            {
              "clip": "ready",
              "index": 0,
              "atlasIndex": 4
            }
          ]
        },
        "bait": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            220,
            220,
            260,
            300
          ],
          "sourceFrames": [
            {
              "clip": "bait",
              "index": 0,
              "atlasIndex": 5
            },
            {
              "clip": "bait",
              "index": 1,
              "atlasIndex": 6
            },
            {
              "clip": "bait",
              "index": 2,
              "atlasIndex": 7
            },
            {
              "clip": "bait",
              "index": 3,
              "atlasIndex": 8
            }
          ]
        },
        "cast": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "cast",
              "index": 0,
              "atlasIndex": 9
            },
            {
              "clip": "cast",
              "index": 1,
              "atlasIndex": 10
            },
            {
              "clip": "cast",
              "index": 2,
              "atlasIndex": 11
            },
            {
              "clip": "cast",
              "index": 3,
              "atlasIndex": 12
            }
          ]
        },
        "fishing": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            500
          ],
          "sourceFrames": [
            {
              "clip": "wait",
              "index": 0,
              "atlasIndex": 13
            }
          ]
        },
        "bite": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            }
          ],
          "nextClip": "effort"
        },
        "effort": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 0
        },
        "hooked": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160,
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            },
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 4
        },
        "reel": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve",
              "index": 0,
              "atlasIndex": 24
            },
            {
              "clip": "retrieve",
              "index": 1,
              "atlasIndex": 25
            },
            {
              "clip": "retrieve",
              "index": 2,
              "atlasIndex": 26
            },
            {
              "clip": "retrieve",
              "index": 3,
              "atlasIndex": 27
            },
            {
              "clip": "retrieve",
              "index": 4,
              "atlasIndex": 28
            },
            {
              "clip": "retrieve",
              "index": 5,
              "atlasIndex": 29
            }
          ],
          "nextClip": "catch"
        },
        "reel_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve_empty",
              "index": 0,
              "atlasIndex": 30
            },
            {
              "clip": "retrieve_empty",
              "index": 1,
              "atlasIndex": 31
            },
            {
              "clip": "retrieve_empty",
              "index": 2,
              "atlasIndex": 32
            },
            {
              "clip": "retrieve_empty",
              "index": 3,
              "atlasIndex": 33
            },
            {
              "clip": "retrieve_empty",
              "index": 4,
              "atlasIndex": 34
            },
            {
              "clip": "retrieve_empty",
              "index": 5,
              "atlasIndex": 35
            }
          ],
          "nextClip": "reset_empty"
        },
        "catch": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            160,
            160,
            180,
            360
          ],
          "sourceFrames": [
            {
              "clip": "catch",
              "index": 0,
              "atlasIndex": 36
            },
            {
              "clip": "catch",
              "index": 1,
              "atlasIndex": 37
            },
            {
              "clip": "catch",
              "index": 2,
              "atlasIndex": 38
            },
            {
              "clip": "catch",
              "index": 3,
              "atlasIndex": 39
            },
            {
              "clip": "catch",
              "index": 4,
              "atlasIndex": 40
            },
            {
              "clip": "catch",
              "index": 5,
              "atlasIndex": 41
            },
            {
              "clip": "catch",
              "index": 6,
              "atlasIndex": 42
            }
          ],
          "nextClip": "reset_catch"
        },
        "reset_catch": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_catch",
              "index": 0,
              "atlasIndex": 43
            },
            {
              "clip": "reset_catch",
              "index": 1,
              "atlasIndex": 44
            },
            {
              "clip": "reset_catch",
              "index": 2,
              "atlasIndex": 45
            },
            {
              "clip": "reset_catch",
              "index": 3,
              "atlasIndex": 46
            }
          ],
          "nextClip": "ready"
        },
        "reset_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_empty",
              "index": 0,
              "atlasIndex": 47
            },
            {
              "clip": "reset_empty",
              "index": 1,
              "atlasIndex": 48
            },
            {
              "clip": "reset_empty",
              "index": 2,
              "atlasIndex": 49
            },
            {
              "clip": "reset_empty",
              "index": 3,
              "atlasIndex": 50
            }
          ],
          "nextClip": "ready"
        },
        "groundbait": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            120,
            120,
            120,
            120,
            160
          ],
          "sourceFrames": [
            {
              "clip": "groundbait",
              "index": 0,
              "atlasIndex": 51
            },
            {
              "clip": "groundbait",
              "index": 1,
              "atlasIndex": 52
            },
            {
              "clip": "groundbait",
              "index": 2,
              "atlasIndex": 53
            },
            {
              "clip": "groundbait",
              "index": 3,
              "atlasIndex": 54
            },
            {
              "clip": "groundbait",
              "index": 4,
              "atlasIndex": 55
            },
            {
              "clip": "groundbait",
              "index": 5,
              "atlasIndex": 56
            },
            {
              "clip": "groundbait",
              "index": 6,
              "atlasIndex": 57
            },
            {
              "clip": "groundbait",
              "index": 7,
              "atlasIndex": 58
            }
          ],
          "nextClip": "ready"
        },
        "leave": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "leave",
              "index": 0,
              "atlasIndex": 59
            },
            {
              "clip": "leave",
              "index": 1,
              "atlasIndex": 60
            },
            {
              "clip": "leave",
              "index": 2,
              "atlasIndex": 61
            },
            {
              "clip": "leave",
              "index": 3,
              "atlasIndex": 62
            }
          ],
          "nextClip": "idle"
        }
      },
      "sourceToRuntime": {
        "wait": "fishing",
        "retrieve": "reel",
        "retrieve_empty": "reel_empty",
        "hooked": "bite + effort (loopStart=4)"
      },
      "flows": {
        "success": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "hooked",
          "reel",
          "catch",
          "reset_catch",
          "ready"
        ],
        "empty": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "reel_empty",
          "reset_empty",
          "ready"
        ]
      },
      "skinId": "cow",
      "skinName": "奶牛猫",
      "skinKind": "painted-pose-assets-v4",
      "artworkManifestSha256": "dfefb1c772f55de9c16781ff0be497936db668a2266ae29753ac551f2953256e",
      "rodOcclusionModel": "head-layer-order-v1",
      "rodSourceManifestSha256": "a0b5789d160385569fd981174082ece49f4ec1b8e3cbdef5b67b1d509c8858ec"
    },
    "cream": {
      "version": 2,
      "sourceVersion": 19,
      "canvas": [
        640,
        512
      ],
      "layers": [
        "body",
        "head",
        "far_hand",
        "rod",
        "line",
        "fish",
        "hands",
        "bobber",
        "bait"
      ],
      "bodyLayers": [
        "body",
        "head",
        "far_hand",
        "hands"
      ],
      "place": {
        "foot": [
          264,
          450
        ],
        "bodyHeight": 257,
        "reference": 512,
        "referenceFoot": [
          138.5,
          404
        ],
        "referenceBody": 279
      },
      "maskModel": "pose-contours-v1",
      "sourceFrameCount": 63,
      "sourceManifestSha256": "083866a4e7c2a1128f5aa52fb00b93b03fa610bce88546711ea68e0bea6b4190",
      "clips": {
        "idle": {
          "playback": "hold",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            }
          ]
        },
        "sit": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            },
            {
              "clip": "sit",
              "index": 1,
              "atlasIndex": 1
            },
            {
              "clip": "sit",
              "index": 2,
              "atlasIndex": 2
            },
            {
              "clip": "sit",
              "index": 3,
              "atlasIndex": 3
            }
          ],
          "nextClip": "ready"
        },
        "ready": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            180
          ],
          "sourceFrames": [
            {
              "clip": "ready",
              "index": 0,
              "atlasIndex": 4
            }
          ]
        },
        "bait": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            220,
            220,
            260,
            300
          ],
          "sourceFrames": [
            {
              "clip": "bait",
              "index": 0,
              "atlasIndex": 5
            },
            {
              "clip": "bait",
              "index": 1,
              "atlasIndex": 6
            },
            {
              "clip": "bait",
              "index": 2,
              "atlasIndex": 7
            },
            {
              "clip": "bait",
              "index": 3,
              "atlasIndex": 8
            }
          ]
        },
        "cast": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "cast",
              "index": 0,
              "atlasIndex": 9
            },
            {
              "clip": "cast",
              "index": 1,
              "atlasIndex": 10
            },
            {
              "clip": "cast",
              "index": 2,
              "atlasIndex": 11
            },
            {
              "clip": "cast",
              "index": 3,
              "atlasIndex": 12
            }
          ]
        },
        "fishing": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            500
          ],
          "sourceFrames": [
            {
              "clip": "wait",
              "index": 0,
              "atlasIndex": 13
            }
          ]
        },
        "bite": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            }
          ],
          "nextClip": "effort"
        },
        "effort": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 0
        },
        "hooked": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160,
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            },
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 4
        },
        "reel": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve",
              "index": 0,
              "atlasIndex": 24
            },
            {
              "clip": "retrieve",
              "index": 1,
              "atlasIndex": 25
            },
            {
              "clip": "retrieve",
              "index": 2,
              "atlasIndex": 26
            },
            {
              "clip": "retrieve",
              "index": 3,
              "atlasIndex": 27
            },
            {
              "clip": "retrieve",
              "index": 4,
              "atlasIndex": 28
            },
            {
              "clip": "retrieve",
              "index": 5,
              "atlasIndex": 29
            }
          ],
          "nextClip": "catch"
        },
        "reel_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve_empty",
              "index": 0,
              "atlasIndex": 30
            },
            {
              "clip": "retrieve_empty",
              "index": 1,
              "atlasIndex": 31
            },
            {
              "clip": "retrieve_empty",
              "index": 2,
              "atlasIndex": 32
            },
            {
              "clip": "retrieve_empty",
              "index": 3,
              "atlasIndex": 33
            },
            {
              "clip": "retrieve_empty",
              "index": 4,
              "atlasIndex": 34
            },
            {
              "clip": "retrieve_empty",
              "index": 5,
              "atlasIndex": 35
            }
          ],
          "nextClip": "reset_empty"
        },
        "catch": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            160,
            160,
            180,
            360
          ],
          "sourceFrames": [
            {
              "clip": "catch",
              "index": 0,
              "atlasIndex": 36
            },
            {
              "clip": "catch",
              "index": 1,
              "atlasIndex": 37
            },
            {
              "clip": "catch",
              "index": 2,
              "atlasIndex": 38
            },
            {
              "clip": "catch",
              "index": 3,
              "atlasIndex": 39
            },
            {
              "clip": "catch",
              "index": 4,
              "atlasIndex": 40
            },
            {
              "clip": "catch",
              "index": 5,
              "atlasIndex": 41
            },
            {
              "clip": "catch",
              "index": 6,
              "atlasIndex": 42
            }
          ],
          "nextClip": "reset_catch"
        },
        "reset_catch": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_catch",
              "index": 0,
              "atlasIndex": 43
            },
            {
              "clip": "reset_catch",
              "index": 1,
              "atlasIndex": 44
            },
            {
              "clip": "reset_catch",
              "index": 2,
              "atlasIndex": 45
            },
            {
              "clip": "reset_catch",
              "index": 3,
              "atlasIndex": 46
            }
          ],
          "nextClip": "ready"
        },
        "reset_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_empty",
              "index": 0,
              "atlasIndex": 47
            },
            {
              "clip": "reset_empty",
              "index": 1,
              "atlasIndex": 48
            },
            {
              "clip": "reset_empty",
              "index": 2,
              "atlasIndex": 49
            },
            {
              "clip": "reset_empty",
              "index": 3,
              "atlasIndex": 50
            }
          ],
          "nextClip": "ready"
        },
        "groundbait": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            120,
            120,
            120,
            120,
            160
          ],
          "sourceFrames": [
            {
              "clip": "groundbait",
              "index": 0,
              "atlasIndex": 51
            },
            {
              "clip": "groundbait",
              "index": 1,
              "atlasIndex": 52
            },
            {
              "clip": "groundbait",
              "index": 2,
              "atlasIndex": 53
            },
            {
              "clip": "groundbait",
              "index": 3,
              "atlasIndex": 54
            },
            {
              "clip": "groundbait",
              "index": 4,
              "atlasIndex": 55
            },
            {
              "clip": "groundbait",
              "index": 5,
              "atlasIndex": 56
            },
            {
              "clip": "groundbait",
              "index": 6,
              "atlasIndex": 57
            },
            {
              "clip": "groundbait",
              "index": 7,
              "atlasIndex": 58
            }
          ],
          "nextClip": "ready"
        },
        "leave": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "leave",
              "index": 0,
              "atlasIndex": 59
            },
            {
              "clip": "leave",
              "index": 1,
              "atlasIndex": 60
            },
            {
              "clip": "leave",
              "index": 2,
              "atlasIndex": 61
            },
            {
              "clip": "leave",
              "index": 3,
              "atlasIndex": 62
            }
          ],
          "nextClip": "idle"
        }
      },
      "sourceToRuntime": {
        "wait": "fishing",
        "retrieve": "reel",
        "retrieve_empty": "reel_empty",
        "hooked": "bite + effort (loopStart=4)"
      },
      "flows": {
        "success": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "hooked",
          "reel",
          "catch",
          "reset_catch",
          "ready"
        ],
        "empty": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "reel_empty",
          "reset_empty",
          "ready"
        ]
      },
      "skinId": "cream",
      "skinName": "奶油猫",
      "skinKind": "painted-pose-assets-v4",
      "artworkManifestSha256": "ff0addddc1d22cdb2a4d5540377897c18131f6326e0637f2a61e6ac76a6dba62",
      "rodOcclusionModel": "head-layer-order-v1",
      "rodSourceManifestSha256": "a0b5789d160385569fd981174082ece49f4ec1b8e3cbdef5b67b1d509c8858ec"
    },
    "dafeiyu": {
      "version": 2,
      "sourceVersion": 19,
      "canvas": [
        640,
        512
      ],
      "layers": [
        "body",
        "head",
        "far_hand",
        "rod",
        "line",
        "fish",
        "hands",
        "bobber",
        "bait"
      ],
      "bodyLayers": [
        "body",
        "head",
        "far_hand",
        "hands"
      ],
      "place": {
        "foot": [
          264,
          450
        ],
        "bodyHeight": 257,
        "reference": 512,
        "referenceFoot": [
          138.5,
          404
        ],
        "referenceBody": 279
      },
      "maskModel": "dafeiyu-pose-regions-hd-v2",
      "sourceFrameCount": 63,
      "sourceManifestSha256": "083866a4e7c2a1128f5aa52fb00b93b03fa610bce88546711ea68e0bea6b4190",
      "clips": {
        "idle": {
          "playback": "hold",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            }
          ],
          "seatVisible": [
            false
          ]
        },
        "sit": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            },
            {
              "clip": "sit",
              "index": 1,
              "atlasIndex": 1
            },
            {
              "clip": "sit",
              "index": 2,
              "atlasIndex": 2
            },
            {
              "clip": "sit",
              "index": 3,
              "atlasIndex": 3
            }
          ],
          "nextClip": "ready",
          "seatVisible": [
            false,
            false,
            true,
            true
          ]
        },
        "ready": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            180
          ],
          "sourceFrames": [
            {
              "clip": "ready",
              "index": 0,
              "atlasIndex": 4
            }
          ],
          "seatVisible": [
            true
          ]
        },
        "bait": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            220,
            220,
            260,
            300
          ],
          "sourceFrames": [
            {
              "clip": "bait",
              "index": 0,
              "atlasIndex": 5
            },
            {
              "clip": "bait",
              "index": 1,
              "atlasIndex": 6
            },
            {
              "clip": "bait",
              "index": 2,
              "atlasIndex": 7
            },
            {
              "clip": "bait",
              "index": 3,
              "atlasIndex": 8
            }
          ],
          "seatVisible": [
            true,
            true,
            true,
            true
          ]
        },
        "cast": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "cast",
              "index": 0,
              "atlasIndex": 9
            },
            {
              "clip": "cast",
              "index": 1,
              "atlasIndex": 10
            },
            {
              "clip": "cast",
              "index": 2,
              "atlasIndex": 11
            },
            {
              "clip": "cast",
              "index": 3,
              "atlasIndex": 12
            }
          ],
          "seatVisible": [
            true,
            true,
            true,
            true
          ]
        },
        "fishing": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            500
          ],
          "sourceFrames": [
            {
              "clip": "wait",
              "index": 0,
              "atlasIndex": 13
            }
          ],
          "seatVisible": [
            true
          ]
        },
        "bite": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            }
          ],
          "nextClip": "effort",
          "seatVisible": [
            true,
            true,
            true,
            true
          ]
        },
        "effort": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 0,
          "seatVisible": [
            true,
            true,
            true,
            true,
            true,
            true
          ]
        },
        "hooked": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160,
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            },
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 4,
          "seatVisible": [
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true
          ]
        },
        "reel": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve",
              "index": 0,
              "atlasIndex": 24
            },
            {
              "clip": "retrieve",
              "index": 1,
              "atlasIndex": 25
            },
            {
              "clip": "retrieve",
              "index": 2,
              "atlasIndex": 26
            },
            {
              "clip": "retrieve",
              "index": 3,
              "atlasIndex": 27
            },
            {
              "clip": "retrieve",
              "index": 4,
              "atlasIndex": 28
            },
            {
              "clip": "retrieve",
              "index": 5,
              "atlasIndex": 29
            }
          ],
          "nextClip": "catch",
          "seatVisible": [
            true,
            true,
            true,
            true,
            true,
            true
          ]
        },
        "reel_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve_empty",
              "index": 0,
              "atlasIndex": 30
            },
            {
              "clip": "retrieve_empty",
              "index": 1,
              "atlasIndex": 31
            },
            {
              "clip": "retrieve_empty",
              "index": 2,
              "atlasIndex": 32
            },
            {
              "clip": "retrieve_empty",
              "index": 3,
              "atlasIndex": 33
            },
            {
              "clip": "retrieve_empty",
              "index": 4,
              "atlasIndex": 34
            },
            {
              "clip": "retrieve_empty",
              "index": 5,
              "atlasIndex": 35
            }
          ],
          "nextClip": "reset_empty",
          "seatVisible": [
            true,
            true,
            true,
            true,
            true,
            true
          ]
        },
        "catch": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            160,
            160,
            180,
            360
          ],
          "sourceFrames": [
            {
              "clip": "catch",
              "index": 0,
              "atlasIndex": 36
            },
            {
              "clip": "catch",
              "index": 1,
              "atlasIndex": 37
            },
            {
              "clip": "catch",
              "index": 2,
              "atlasIndex": 38
            },
            {
              "clip": "catch",
              "index": 3,
              "atlasIndex": 39
            },
            {
              "clip": "catch",
              "index": 4,
              "atlasIndex": 40
            },
            {
              "clip": "catch",
              "index": 5,
              "atlasIndex": 41
            },
            {
              "clip": "catch",
              "index": 6,
              "atlasIndex": 42
            }
          ],
          "nextClip": "reset_catch",
          "seatVisible": [
            true,
            true,
            true,
            true,
            true,
            true,
            true
          ]
        },
        "reset_catch": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_catch",
              "index": 0,
              "atlasIndex": 43
            },
            {
              "clip": "reset_catch",
              "index": 1,
              "atlasIndex": 44
            },
            {
              "clip": "reset_catch",
              "index": 2,
              "atlasIndex": 45
            },
            {
              "clip": "reset_catch",
              "index": 3,
              "atlasIndex": 46
            }
          ],
          "nextClip": "ready",
          "seatVisible": [
            true,
            true,
            true,
            true
          ]
        },
        "reset_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_empty",
              "index": 0,
              "atlasIndex": 47
            },
            {
              "clip": "reset_empty",
              "index": 1,
              "atlasIndex": 48
            },
            {
              "clip": "reset_empty",
              "index": 2,
              "atlasIndex": 49
            },
            {
              "clip": "reset_empty",
              "index": 3,
              "atlasIndex": 50
            }
          ],
          "nextClip": "ready",
          "seatVisible": [
            true,
            true,
            true,
            true
          ]
        },
        "groundbait": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            120,
            120,
            120,
            120,
            160
          ],
          "sourceFrames": [
            {
              "clip": "groundbait",
              "index": 0,
              "atlasIndex": 51
            },
            {
              "clip": "groundbait",
              "index": 1,
              "atlasIndex": 52
            },
            {
              "clip": "groundbait",
              "index": 2,
              "atlasIndex": 53
            },
            {
              "clip": "groundbait",
              "index": 3,
              "atlasIndex": 54
            },
            {
              "clip": "groundbait",
              "index": 4,
              "atlasIndex": 55
            },
            {
              "clip": "groundbait",
              "index": 5,
              "atlasIndex": 56
            },
            {
              "clip": "groundbait",
              "index": 6,
              "atlasIndex": 57
            },
            {
              "clip": "groundbait",
              "index": 7,
              "atlasIndex": 58
            }
          ],
          "nextClip": "ready",
          "seatVisible": [
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true
          ]
        },
        "leave": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "leave",
              "index": 0,
              "atlasIndex": 59
            },
            {
              "clip": "leave",
              "index": 1,
              "atlasIndex": 60
            },
            {
              "clip": "leave",
              "index": 2,
              "atlasIndex": 61
            },
            {
              "clip": "leave",
              "index": 3,
              "atlasIndex": 62
            }
          ],
          "nextClip": "idle",
          "seatVisible": [
            true,
            true,
            false,
            false
          ]
        }
      },
      "sourceToRuntime": {
        "wait": "fishing",
        "retrieve": "reel",
        "retrieve_empty": "reel_empty",
        "hooked": "bite + effort (loopStart=4)"
      },
      "flows": {
        "success": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "hooked",
          "reel",
          "catch",
          "reset_catch",
          "ready"
        ],
        "empty": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "reel_empty",
          "reset_empty",
          "ready"
        ]
      },
      "skinId": "dafeiyu",
      "skinName": "大肥鱼",
      "skinKind": "painted-humanoid-character",
      "rodOcclusionModel": "head-layer-order-v1",
      "rodSourceManifestSha256": "a0b5789d160385569fd981174082ece49f4ec1b8e3cbdef5b67b1d509c8858ec",
      "bodyTextureScale": 2,
      "reviewStatus": "art-review",
      "characterArtworkManifestSha256": "bac169ad91197207af5fb98113ba8682b1e0eaa3d62b9534bda34fc00caca67c"
    },
    "orange": {
      "version": 2,
      "sourceVersion": 19,
      "canvas": [
        640,
        512
      ],
      "layers": [
        "body",
        "head",
        "far_hand",
        "rod",
        "line",
        "fish",
        "hands",
        "bobber",
        "bait"
      ],
      "bodyLayers": [
        "body",
        "head",
        "far_hand",
        "hands"
      ],
      "place": {
        "foot": [
          264,
          450
        ],
        "bodyHeight": 257,
        "reference": 512,
        "referenceFoot": [
          138.5,
          404
        ],
        "referenceBody": 279
      },
      "maskModel": "pose-contours-v1",
      "sourceFrameCount": 63,
      "sourceManifestSha256": "083866a4e7c2a1128f5aa52fb00b93b03fa610bce88546711ea68e0bea6b4190",
      "clips": {
        "idle": {
          "playback": "hold",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            }
          ]
        },
        "sit": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            },
            {
              "clip": "sit",
              "index": 1,
              "atlasIndex": 1
            },
            {
              "clip": "sit",
              "index": 2,
              "atlasIndex": 2
            },
            {
              "clip": "sit",
              "index": 3,
              "atlasIndex": 3
            }
          ],
          "nextClip": "ready"
        },
        "ready": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            180
          ],
          "sourceFrames": [
            {
              "clip": "ready",
              "index": 0,
              "atlasIndex": 4
            }
          ]
        },
        "bait": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            220,
            220,
            260,
            300
          ],
          "sourceFrames": [
            {
              "clip": "bait",
              "index": 0,
              "atlasIndex": 5
            },
            {
              "clip": "bait",
              "index": 1,
              "atlasIndex": 6
            },
            {
              "clip": "bait",
              "index": 2,
              "atlasIndex": 7
            },
            {
              "clip": "bait",
              "index": 3,
              "atlasIndex": 8
            }
          ]
        },
        "cast": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "cast",
              "index": 0,
              "atlasIndex": 9
            },
            {
              "clip": "cast",
              "index": 1,
              "atlasIndex": 10
            },
            {
              "clip": "cast",
              "index": 2,
              "atlasIndex": 11
            },
            {
              "clip": "cast",
              "index": 3,
              "atlasIndex": 12
            }
          ]
        },
        "fishing": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            500
          ],
          "sourceFrames": [
            {
              "clip": "wait",
              "index": 0,
              "atlasIndex": 13
            }
          ]
        },
        "bite": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            }
          ],
          "nextClip": "effort"
        },
        "effort": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 0
        },
        "hooked": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160,
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            },
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 4
        },
        "reel": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve",
              "index": 0,
              "atlasIndex": 24
            },
            {
              "clip": "retrieve",
              "index": 1,
              "atlasIndex": 25
            },
            {
              "clip": "retrieve",
              "index": 2,
              "atlasIndex": 26
            },
            {
              "clip": "retrieve",
              "index": 3,
              "atlasIndex": 27
            },
            {
              "clip": "retrieve",
              "index": 4,
              "atlasIndex": 28
            },
            {
              "clip": "retrieve",
              "index": 5,
              "atlasIndex": 29
            }
          ],
          "nextClip": "catch"
        },
        "reel_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve_empty",
              "index": 0,
              "atlasIndex": 30
            },
            {
              "clip": "retrieve_empty",
              "index": 1,
              "atlasIndex": 31
            },
            {
              "clip": "retrieve_empty",
              "index": 2,
              "atlasIndex": 32
            },
            {
              "clip": "retrieve_empty",
              "index": 3,
              "atlasIndex": 33
            },
            {
              "clip": "retrieve_empty",
              "index": 4,
              "atlasIndex": 34
            },
            {
              "clip": "retrieve_empty",
              "index": 5,
              "atlasIndex": 35
            }
          ],
          "nextClip": "reset_empty"
        },
        "catch": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            160,
            160,
            180,
            360
          ],
          "sourceFrames": [
            {
              "clip": "catch",
              "index": 0,
              "atlasIndex": 36
            },
            {
              "clip": "catch",
              "index": 1,
              "atlasIndex": 37
            },
            {
              "clip": "catch",
              "index": 2,
              "atlasIndex": 38
            },
            {
              "clip": "catch",
              "index": 3,
              "atlasIndex": 39
            },
            {
              "clip": "catch",
              "index": 4,
              "atlasIndex": 40
            },
            {
              "clip": "catch",
              "index": 5,
              "atlasIndex": 41
            },
            {
              "clip": "catch",
              "index": 6,
              "atlasIndex": 42
            }
          ],
          "nextClip": "reset_catch"
        },
        "reset_catch": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_catch",
              "index": 0,
              "atlasIndex": 43
            },
            {
              "clip": "reset_catch",
              "index": 1,
              "atlasIndex": 44
            },
            {
              "clip": "reset_catch",
              "index": 2,
              "atlasIndex": 45
            },
            {
              "clip": "reset_catch",
              "index": 3,
              "atlasIndex": 46
            }
          ],
          "nextClip": "ready"
        },
        "reset_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_empty",
              "index": 0,
              "atlasIndex": 47
            },
            {
              "clip": "reset_empty",
              "index": 1,
              "atlasIndex": 48
            },
            {
              "clip": "reset_empty",
              "index": 2,
              "atlasIndex": 49
            },
            {
              "clip": "reset_empty",
              "index": 3,
              "atlasIndex": 50
            }
          ],
          "nextClip": "ready"
        },
        "groundbait": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            120,
            120,
            120,
            120,
            160
          ],
          "sourceFrames": [
            {
              "clip": "groundbait",
              "index": 0,
              "atlasIndex": 51
            },
            {
              "clip": "groundbait",
              "index": 1,
              "atlasIndex": 52
            },
            {
              "clip": "groundbait",
              "index": 2,
              "atlasIndex": 53
            },
            {
              "clip": "groundbait",
              "index": 3,
              "atlasIndex": 54
            },
            {
              "clip": "groundbait",
              "index": 4,
              "atlasIndex": 55
            },
            {
              "clip": "groundbait",
              "index": 5,
              "atlasIndex": 56
            },
            {
              "clip": "groundbait",
              "index": 6,
              "atlasIndex": 57
            },
            {
              "clip": "groundbait",
              "index": 7,
              "atlasIndex": 58
            }
          ],
          "nextClip": "ready"
        },
        "leave": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "leave",
              "index": 0,
              "atlasIndex": 59
            },
            {
              "clip": "leave",
              "index": 1,
              "atlasIndex": 60
            },
            {
              "clip": "leave",
              "index": 2,
              "atlasIndex": 61
            },
            {
              "clip": "leave",
              "index": 3,
              "atlasIndex": 62
            }
          ],
          "nextClip": "idle"
        }
      },
      "sourceToRuntime": {
        "wait": "fishing",
        "retrieve": "reel",
        "retrieve_empty": "reel_empty",
        "hooked": "bite + effort (loopStart=4)"
      },
      "flows": {
        "success": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "hooked",
          "reel",
          "catch",
          "reset_catch",
          "ready"
        ],
        "empty": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "reel_empty",
          "reset_empty",
          "ready"
        ]
      },
      "skinId": "orange",
      "skinName": "原版橘猫",
      "skinKind": "approved-tint-variant-not-new-breed",
      "rodOcclusionModel": "head-layer-order-v1",
      "rodSourceManifestSha256": "a0b5789d160385569fd981174082ece49f4ec1b8e3cbdef5b67b1d509c8858ec"
    },
    "sangbiao": {
      "version": 2,
      "sourceVersion": 19,
      "canvas": [
        640,
        512
      ],
      "layers": [
        "body",
        "head",
        "far_hand",
        "rod",
        "line",
        "fish",
        "hands",
        "bobber",
        "bait"
      ],
      "bodyLayers": [
        "body",
        "head",
        "far_hand",
        "hands"
      ],
      "place": {
        "foot": [
          264,
          450
        ],
        "bodyHeight": 257,
        "reference": 512,
        "referenceFoot": [
          138.5,
          404
        ],
        "referenceBody": 279
      },
      "maskModel": "pose-contours-v1",
      "sourceFrameCount": 63,
      "sourceManifestSha256": "083866a4e7c2a1128f5aa52fb00b93b03fa610bce88546711ea68e0bea6b4190",
      "clips": {
        "idle": {
          "playback": "hold",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            }
          ]
        },
        "sit": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            },
            {
              "clip": "sit",
              "index": 1,
              "atlasIndex": 1
            },
            {
              "clip": "sit",
              "index": 2,
              "atlasIndex": 2
            },
            {
              "clip": "sit",
              "index": 3,
              "atlasIndex": 3
            }
          ],
          "nextClip": "ready"
        },
        "ready": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            180
          ],
          "sourceFrames": [
            {
              "clip": "ready",
              "index": 0,
              "atlasIndex": 4
            }
          ]
        },
        "bait": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            220,
            220,
            260,
            300
          ],
          "sourceFrames": [
            {
              "clip": "bait",
              "index": 0,
              "atlasIndex": 5
            },
            {
              "clip": "bait",
              "index": 1,
              "atlasIndex": 6
            },
            {
              "clip": "bait",
              "index": 2,
              "atlasIndex": 7
            },
            {
              "clip": "bait",
              "index": 3,
              "atlasIndex": 8
            }
          ]
        },
        "cast": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "cast",
              "index": 0,
              "atlasIndex": 9
            },
            {
              "clip": "cast",
              "index": 1,
              "atlasIndex": 10
            },
            {
              "clip": "cast",
              "index": 2,
              "atlasIndex": 11
            },
            {
              "clip": "cast",
              "index": 3,
              "atlasIndex": 12
            }
          ]
        },
        "fishing": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            500
          ],
          "sourceFrames": [
            {
              "clip": "wait",
              "index": 0,
              "atlasIndex": 13
            }
          ]
        },
        "bite": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            }
          ],
          "nextClip": "effort"
        },
        "effort": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 0
        },
        "hooked": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160,
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            },
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 4
        },
        "reel": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve",
              "index": 0,
              "atlasIndex": 24
            },
            {
              "clip": "retrieve",
              "index": 1,
              "atlasIndex": 25
            },
            {
              "clip": "retrieve",
              "index": 2,
              "atlasIndex": 26
            },
            {
              "clip": "retrieve",
              "index": 3,
              "atlasIndex": 27
            },
            {
              "clip": "retrieve",
              "index": 4,
              "atlasIndex": 28
            },
            {
              "clip": "retrieve",
              "index": 5,
              "atlasIndex": 29
            }
          ],
          "nextClip": "catch"
        },
        "reel_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve_empty",
              "index": 0,
              "atlasIndex": 30
            },
            {
              "clip": "retrieve_empty",
              "index": 1,
              "atlasIndex": 31
            },
            {
              "clip": "retrieve_empty",
              "index": 2,
              "atlasIndex": 32
            },
            {
              "clip": "retrieve_empty",
              "index": 3,
              "atlasIndex": 33
            },
            {
              "clip": "retrieve_empty",
              "index": 4,
              "atlasIndex": 34
            },
            {
              "clip": "retrieve_empty",
              "index": 5,
              "atlasIndex": 35
            }
          ],
          "nextClip": "reset_empty"
        },
        "catch": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            160,
            160,
            180,
            360
          ],
          "sourceFrames": [
            {
              "clip": "catch",
              "index": 0,
              "atlasIndex": 36
            },
            {
              "clip": "catch",
              "index": 1,
              "atlasIndex": 37
            },
            {
              "clip": "catch",
              "index": 2,
              "atlasIndex": 38
            },
            {
              "clip": "catch",
              "index": 3,
              "atlasIndex": 39
            },
            {
              "clip": "catch",
              "index": 4,
              "atlasIndex": 40
            },
            {
              "clip": "catch",
              "index": 5,
              "atlasIndex": 41
            },
            {
              "clip": "catch",
              "index": 6,
              "atlasIndex": 42
            }
          ],
          "nextClip": "reset_catch"
        },
        "reset_catch": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_catch",
              "index": 0,
              "atlasIndex": 43
            },
            {
              "clip": "reset_catch",
              "index": 1,
              "atlasIndex": 44
            },
            {
              "clip": "reset_catch",
              "index": 2,
              "atlasIndex": 45
            },
            {
              "clip": "reset_catch",
              "index": 3,
              "atlasIndex": 46
            }
          ],
          "nextClip": "ready"
        },
        "reset_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_empty",
              "index": 0,
              "atlasIndex": 47
            },
            {
              "clip": "reset_empty",
              "index": 1,
              "atlasIndex": 48
            },
            {
              "clip": "reset_empty",
              "index": 2,
              "atlasIndex": 49
            },
            {
              "clip": "reset_empty",
              "index": 3,
              "atlasIndex": 50
            }
          ],
          "nextClip": "ready"
        },
        "groundbait": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            120,
            120,
            120,
            120,
            160
          ],
          "sourceFrames": [
            {
              "clip": "groundbait",
              "index": 0,
              "atlasIndex": 51
            },
            {
              "clip": "groundbait",
              "index": 1,
              "atlasIndex": 52
            },
            {
              "clip": "groundbait",
              "index": 2,
              "atlasIndex": 53
            },
            {
              "clip": "groundbait",
              "index": 3,
              "atlasIndex": 54
            },
            {
              "clip": "groundbait",
              "index": 4,
              "atlasIndex": 55
            },
            {
              "clip": "groundbait",
              "index": 5,
              "atlasIndex": 56
            },
            {
              "clip": "groundbait",
              "index": 6,
              "atlasIndex": 57
            },
            {
              "clip": "groundbait",
              "index": 7,
              "atlasIndex": 58
            }
          ],
          "nextClip": "ready"
        },
        "leave": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "leave",
              "index": 0,
              "atlasIndex": 59
            },
            {
              "clip": "leave",
              "index": 1,
              "atlasIndex": 60
            },
            {
              "clip": "leave",
              "index": 2,
              "atlasIndex": 61
            },
            {
              "clip": "leave",
              "index": 3,
              "atlasIndex": 62
            }
          ],
          "nextClip": "idle"
        }
      },
      "sourceToRuntime": {
        "wait": "fishing",
        "retrieve": "reel",
        "retrieve_empty": "reel_empty",
        "hooked": "bite + effort (loopStart=4)"
      },
      "flows": {
        "success": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "hooked",
          "reel",
          "catch",
          "reset_catch",
          "ready"
        ],
        "empty": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "reel_empty",
          "reset_empty",
          "ready"
        ]
      },
      "skinId": "sangbiao",
      "skinName": "狸花猫「丧彪」",
      "skinKind": "painted-pose-assets-v4",
      "rodOcclusionModel": "head-layer-order-v1",
      "rodSourceManifestSha256": "a0b5789d160385569fd981174082ece49f4ec1b8e3cbdef5b67b1d509c8858ec",
      "artworkManifestSha256": "44128712b7eeeed6833a34d69df27e0edf4d11f65f8ed59baa8775a43b60f36c",
      "bodyTextureScale": 1
    },
    "silver": {
      "version": 2,
      "sourceVersion": 19,
      "canvas": [
        640,
        512
      ],
      "layers": [
        "body",
        "head",
        "far_hand",
        "rod",
        "line",
        "fish",
        "hands",
        "bobber",
        "bait"
      ],
      "bodyLayers": [
        "body",
        "head",
        "far_hand",
        "hands"
      ],
      "place": {
        "foot": [
          264,
          450
        ],
        "bodyHeight": 257,
        "reference": 512,
        "referenceFoot": [
          138.5,
          404
        ],
        "referenceBody": 279
      },
      "maskModel": "pose-contours-v1",
      "sourceFrameCount": 63,
      "sourceManifestSha256": "083866a4e7c2a1128f5aa52fb00b93b03fa610bce88546711ea68e0bea6b4190",
      "clips": {
        "idle": {
          "playback": "hold",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            }
          ]
        },
        "sit": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            },
            {
              "clip": "sit",
              "index": 1,
              "atlasIndex": 1
            },
            {
              "clip": "sit",
              "index": 2,
              "atlasIndex": 2
            },
            {
              "clip": "sit",
              "index": 3,
              "atlasIndex": 3
            }
          ],
          "nextClip": "ready"
        },
        "ready": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            180
          ],
          "sourceFrames": [
            {
              "clip": "ready",
              "index": 0,
              "atlasIndex": 4
            }
          ]
        },
        "bait": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            220,
            220,
            260,
            300
          ],
          "sourceFrames": [
            {
              "clip": "bait",
              "index": 0,
              "atlasIndex": 5
            },
            {
              "clip": "bait",
              "index": 1,
              "atlasIndex": 6
            },
            {
              "clip": "bait",
              "index": 2,
              "atlasIndex": 7
            },
            {
              "clip": "bait",
              "index": 3,
              "atlasIndex": 8
            }
          ]
        },
        "cast": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "cast",
              "index": 0,
              "atlasIndex": 9
            },
            {
              "clip": "cast",
              "index": 1,
              "atlasIndex": 10
            },
            {
              "clip": "cast",
              "index": 2,
              "atlasIndex": 11
            },
            {
              "clip": "cast",
              "index": 3,
              "atlasIndex": 12
            }
          ]
        },
        "fishing": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            500
          ],
          "sourceFrames": [
            {
              "clip": "wait",
              "index": 0,
              "atlasIndex": 13
            }
          ]
        },
        "bite": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            }
          ],
          "nextClip": "effort"
        },
        "effort": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 0
        },
        "hooked": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160,
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            },
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 4
        },
        "reel": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve",
              "index": 0,
              "atlasIndex": 24
            },
            {
              "clip": "retrieve",
              "index": 1,
              "atlasIndex": 25
            },
            {
              "clip": "retrieve",
              "index": 2,
              "atlasIndex": 26
            },
            {
              "clip": "retrieve",
              "index": 3,
              "atlasIndex": 27
            },
            {
              "clip": "retrieve",
              "index": 4,
              "atlasIndex": 28
            },
            {
              "clip": "retrieve",
              "index": 5,
              "atlasIndex": 29
            }
          ],
          "nextClip": "catch"
        },
        "reel_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve_empty",
              "index": 0,
              "atlasIndex": 30
            },
            {
              "clip": "retrieve_empty",
              "index": 1,
              "atlasIndex": 31
            },
            {
              "clip": "retrieve_empty",
              "index": 2,
              "atlasIndex": 32
            },
            {
              "clip": "retrieve_empty",
              "index": 3,
              "atlasIndex": 33
            },
            {
              "clip": "retrieve_empty",
              "index": 4,
              "atlasIndex": 34
            },
            {
              "clip": "retrieve_empty",
              "index": 5,
              "atlasIndex": 35
            }
          ],
          "nextClip": "reset_empty"
        },
        "catch": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            160,
            160,
            180,
            360
          ],
          "sourceFrames": [
            {
              "clip": "catch",
              "index": 0,
              "atlasIndex": 36
            },
            {
              "clip": "catch",
              "index": 1,
              "atlasIndex": 37
            },
            {
              "clip": "catch",
              "index": 2,
              "atlasIndex": 38
            },
            {
              "clip": "catch",
              "index": 3,
              "atlasIndex": 39
            },
            {
              "clip": "catch",
              "index": 4,
              "atlasIndex": 40
            },
            {
              "clip": "catch",
              "index": 5,
              "atlasIndex": 41
            },
            {
              "clip": "catch",
              "index": 6,
              "atlasIndex": 42
            }
          ],
          "nextClip": "reset_catch"
        },
        "reset_catch": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_catch",
              "index": 0,
              "atlasIndex": 43
            },
            {
              "clip": "reset_catch",
              "index": 1,
              "atlasIndex": 44
            },
            {
              "clip": "reset_catch",
              "index": 2,
              "atlasIndex": 45
            },
            {
              "clip": "reset_catch",
              "index": 3,
              "atlasIndex": 46
            }
          ],
          "nextClip": "ready"
        },
        "reset_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_empty",
              "index": 0,
              "atlasIndex": 47
            },
            {
              "clip": "reset_empty",
              "index": 1,
              "atlasIndex": 48
            },
            {
              "clip": "reset_empty",
              "index": 2,
              "atlasIndex": 49
            },
            {
              "clip": "reset_empty",
              "index": 3,
              "atlasIndex": 50
            }
          ],
          "nextClip": "ready"
        },
        "groundbait": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            120,
            120,
            120,
            120,
            160
          ],
          "sourceFrames": [
            {
              "clip": "groundbait",
              "index": 0,
              "atlasIndex": 51
            },
            {
              "clip": "groundbait",
              "index": 1,
              "atlasIndex": 52
            },
            {
              "clip": "groundbait",
              "index": 2,
              "atlasIndex": 53
            },
            {
              "clip": "groundbait",
              "index": 3,
              "atlasIndex": 54
            },
            {
              "clip": "groundbait",
              "index": 4,
              "atlasIndex": 55
            },
            {
              "clip": "groundbait",
              "index": 5,
              "atlasIndex": 56
            },
            {
              "clip": "groundbait",
              "index": 6,
              "atlasIndex": 57
            },
            {
              "clip": "groundbait",
              "index": 7,
              "atlasIndex": 58
            }
          ],
          "nextClip": "ready"
        },
        "leave": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "leave",
              "index": 0,
              "atlasIndex": 59
            },
            {
              "clip": "leave",
              "index": 1,
              "atlasIndex": 60
            },
            {
              "clip": "leave",
              "index": 2,
              "atlasIndex": 61
            },
            {
              "clip": "leave",
              "index": 3,
              "atlasIndex": 62
            }
          ],
          "nextClip": "idle"
        }
      },
      "sourceToRuntime": {
        "wait": "fishing",
        "retrieve": "reel",
        "retrieve_empty": "reel_empty",
        "hooked": "bite + effort (loopStart=4)"
      },
      "flows": {
        "success": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "hooked",
          "reel",
          "catch",
          "reset_catch",
          "ready"
        ],
        "empty": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "reel_empty",
          "reset_empty",
          "ready"
        ]
      },
      "skinId": "silver",
      "skinName": "银渐层",
      "skinKind": "painted-pose-assets-v4",
      "artworkManifestSha256": "83689122eaa2c7e296cab58ea6a593798277fc32ba843de3296daa17abde96a4",
      "rodOcclusionModel": "head-layer-order-v1",
      "rodSourceManifestSha256": "a0b5789d160385569fd981174082ece49f4ec1b8e3cbdef5b67b1d509c8858ec"
    },
    "white": {
      "version": 2,
      "sourceVersion": 19,
      "canvas": [
        640,
        512
      ],
      "layers": [
        "body",
        "head",
        "far_hand",
        "rod",
        "line",
        "fish",
        "hands",
        "bobber",
        "bait"
      ],
      "bodyLayers": [
        "body",
        "head",
        "far_hand",
        "hands"
      ],
      "place": {
        "foot": [
          264,
          450
        ],
        "bodyHeight": 257,
        "reference": 512,
        "referenceFoot": [
          138.5,
          404
        ],
        "referenceBody": 279
      },
      "maskModel": "pose-contours-v1",
      "sourceFrameCount": 63,
      "sourceManifestSha256": "083866a4e7c2a1128f5aa52fb00b93b03fa610bce88546711ea68e0bea6b4190",
      "clips": {
        "idle": {
          "playback": "hold",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            }
          ]
        },
        "sit": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "sit",
              "index": 0,
              "atlasIndex": 0
            },
            {
              "clip": "sit",
              "index": 1,
              "atlasIndex": 1
            },
            {
              "clip": "sit",
              "index": 2,
              "atlasIndex": 2
            },
            {
              "clip": "sit",
              "index": 3,
              "atlasIndex": 3
            }
          ],
          "nextClip": "ready"
        },
        "ready": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            180
          ],
          "sourceFrames": [
            {
              "clip": "ready",
              "index": 0,
              "atlasIndex": 4
            }
          ]
        },
        "bait": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            220,
            220,
            260,
            300
          ],
          "sourceFrames": [
            {
              "clip": "bait",
              "index": 0,
              "atlasIndex": 5
            },
            {
              "clip": "bait",
              "index": 1,
              "atlasIndex": 6
            },
            {
              "clip": "bait",
              "index": 2,
              "atlasIndex": 7
            },
            {
              "clip": "bait",
              "index": 3,
              "atlasIndex": 8
            }
          ]
        },
        "cast": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "cast",
              "index": 0,
              "atlasIndex": 9
            },
            {
              "clip": "cast",
              "index": 1,
              "atlasIndex": 10
            },
            {
              "clip": "cast",
              "index": 2,
              "atlasIndex": 11
            },
            {
              "clip": "cast",
              "index": 3,
              "atlasIndex": 12
            }
          ]
        },
        "fishing": {
          "playback": "hold",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            500
          ],
          "sourceFrames": [
            {
              "clip": "wait",
              "index": 0,
              "atlasIndex": 13
            }
          ]
        },
        "bite": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            }
          ],
          "nextClip": "effort"
        },
        "effort": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 0
        },
        "hooked": {
          "playback": "intro-loop",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            160,
            140,
            140,
            160,
            140,
            140,
            160
          ],
          "sourceFrames": [
            {
              "clip": "bite",
              "index": 0,
              "atlasIndex": 14
            },
            {
              "clip": "bite",
              "index": 1,
              "atlasIndex": 15
            },
            {
              "clip": "bite",
              "index": 2,
              "atlasIndex": 16
            },
            {
              "clip": "bite",
              "index": 3,
              "atlasIndex": 17
            },
            {
              "clip": "effort",
              "index": 0,
              "atlasIndex": 18
            },
            {
              "clip": "effort",
              "index": 1,
              "atlasIndex": 19
            },
            {
              "clip": "effort",
              "index": 2,
              "atlasIndex": 20
            },
            {
              "clip": "effort",
              "index": 3,
              "atlasIndex": 21
            },
            {
              "clip": "effort",
              "index": 4,
              "atlasIndex": 22
            },
            {
              "clip": "effort",
              "index": 5,
              "atlasIndex": 23
            }
          ],
          "loopStart": 4
        },
        "reel": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve",
              "index": 0,
              "atlasIndex": 24
            },
            {
              "clip": "retrieve",
              "index": 1,
              "atlasIndex": 25
            },
            {
              "clip": "retrieve",
              "index": 2,
              "atlasIndex": 26
            },
            {
              "clip": "retrieve",
              "index": 3,
              "atlasIndex": 27
            },
            {
              "clip": "retrieve",
              "index": 4,
              "atlasIndex": 28
            },
            {
              "clip": "retrieve",
              "index": 5,
              "atlasIndex": 29
            }
          ],
          "nextClip": "catch"
        },
        "reel_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            100,
            100,
            100,
            100,
            100
          ],
          "sourceFrames": [
            {
              "clip": "retrieve_empty",
              "index": 0,
              "atlasIndex": 30
            },
            {
              "clip": "retrieve_empty",
              "index": 1,
              "atlasIndex": 31
            },
            {
              "clip": "retrieve_empty",
              "index": 2,
              "atlasIndex": 32
            },
            {
              "clip": "retrieve_empty",
              "index": 3,
              "atlasIndex": 33
            },
            {
              "clip": "retrieve_empty",
              "index": 4,
              "atlasIndex": 34
            },
            {
              "clip": "retrieve_empty",
              "index": 5,
              "atlasIndex": 35
            }
          ],
          "nextClip": "reset_empty"
        },
        "catch": {
          "playback": "once",
          "frames": [
            [
              "line",
              "fish",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            160,
            160,
            180,
            360
          ],
          "sourceFrames": [
            {
              "clip": "catch",
              "index": 0,
              "atlasIndex": 36
            },
            {
              "clip": "catch",
              "index": 1,
              "atlasIndex": 37
            },
            {
              "clip": "catch",
              "index": 2,
              "atlasIndex": 38
            },
            {
              "clip": "catch",
              "index": 3,
              "atlasIndex": 39
            },
            {
              "clip": "catch",
              "index": 4,
              "atlasIndex": 40
            },
            {
              "clip": "catch",
              "index": 5,
              "atlasIndex": 41
            },
            {
              "clip": "catch",
              "index": 6,
              "atlasIndex": 42
            }
          ],
          "nextClip": "reset_catch"
        },
        "reset_catch": {
          "playback": "once",
          "frames": [
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_catch",
              "index": 0,
              "atlasIndex": 43
            },
            {
              "clip": "reset_catch",
              "index": 1,
              "atlasIndex": 44
            },
            {
              "clip": "reset_catch",
              "index": 2,
              "atlasIndex": 45
            },
            {
              "clip": "reset_catch",
              "index": 3,
              "atlasIndex": 46
            }
          ],
          "nextClip": "ready"
        },
        "reset_empty": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            120,
            120,
            120,
            120
          ],
          "sourceFrames": [
            {
              "clip": "reset_empty",
              "index": 0,
              "atlasIndex": 47
            },
            {
              "clip": "reset_empty",
              "index": 1,
              "atlasIndex": 48
            },
            {
              "clip": "reset_empty",
              "index": 2,
              "atlasIndex": 49
            },
            {
              "clip": "reset_empty",
              "index": 3,
              "atlasIndex": 50
            }
          ],
          "nextClip": "ready"
        },
        "groundbait": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ]
          ],
          "durationsMs": [
            100,
            120,
            120,
            120,
            120,
            120,
            120,
            160
          ],
          "sourceFrames": [
            {
              "clip": "groundbait",
              "index": 0,
              "atlasIndex": 51
            },
            {
              "clip": "groundbait",
              "index": 1,
              "atlasIndex": 52
            },
            {
              "clip": "groundbait",
              "index": 2,
              "atlasIndex": 53
            },
            {
              "clip": "groundbait",
              "index": 3,
              "atlasIndex": 54
            },
            {
              "clip": "groundbait",
              "index": 4,
              "atlasIndex": 55
            },
            {
              "clip": "groundbait",
              "index": 5,
              "atlasIndex": 56
            },
            {
              "clip": "groundbait",
              "index": 6,
              "atlasIndex": 57
            },
            {
              "clip": "groundbait",
              "index": 7,
              "atlasIndex": 58
            }
          ],
          "nextClip": "ready"
        },
        "leave": {
          "playback": "once",
          "frames": [
            [
              "line",
              "bobber",
              "bait",
              "body",
              "far_hand",
              "rod",
              "head",
              "hands",
              "fish"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ],
            [
              "rod",
              "line",
              "body",
              "head",
              "fish",
              "far_hand",
              "hands",
              "bobber",
              "bait"
            ]
          ],
          "durationsMs": [
            120,
            140,
            140,
            180
          ],
          "sourceFrames": [
            {
              "clip": "leave",
              "index": 0,
              "atlasIndex": 59
            },
            {
              "clip": "leave",
              "index": 1,
              "atlasIndex": 60
            },
            {
              "clip": "leave",
              "index": 2,
              "atlasIndex": 61
            },
            {
              "clip": "leave",
              "index": 3,
              "atlasIndex": 62
            }
          ],
          "nextClip": "idle"
        }
      },
      "sourceToRuntime": {
        "wait": "fishing",
        "retrieve": "reel",
        "retrieve_empty": "reel_empty",
        "hooked": "bite + effort (loopStart=4)"
      },
      "flows": {
        "success": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "hooked",
          "reel",
          "catch",
          "reset_catch",
          "ready"
        ],
        "empty": [
          "sit",
          "ready",
          "bait",
          "cast",
          "fishing",
          "reel_empty",
          "reset_empty",
          "ready"
        ]
      },
      "skinId": "white",
      "skinName": "白猫",
      "skinKind": "painted-pose-assets-v4",
      "artworkManifestSha256": "a76d89f7e6f4e4a3f7e0a918d08e2d7fce8e6f954a077422b805a13c972d059c",
      "rodOcclusionModel": "head-layer-order-v1",
      "rodSourceManifestSha256": "a0b5789d160385569fd981174082ece49f4ec1b8e3cbdef5b67b1d509c8858ec"
    }
  }
}
;
