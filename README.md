# VNKit

A small Unity toolkit for branching visual novels: a node-graph story editor, a video-editor-style
timeline for background/music clips, and a runtime player — with story variables, conditional
branching, save/load, and multiple characters on screen at once.

## Requirements

- Unity 2021.3+ (uses `UIElements`/`GraphView` and `TwoPaneSplitView`)
- [TextMeshPro](https://docs.unity3d.com/Manual/com.unity.textmeshpro.html) (Package Manager)
- [DOTween](http://dotween.demigiant.com/) (Asset Store, free)

> **About the assembly definitions.** `VNKit.Runtime.asmdef` references an assembly named
> `DOTween`. Most DOTween installs compile straight into `Assembly-CSharp` and don't ship an
> asmdef of their own — if that's your case, Unity will report a missing assembly reference.
> Open **Tools > Demigiant > DOTween Utility Panel > Create ASMDEF** in DOTween's own menu to
> generate one (it's named `DOTween`), or remove the `"DOTween"` entry from
> `VNKit/Runtime/VNKit.Runtime.asmdef` and let the scripts compile into the default assembly
> instead.

## Installing

Drop the `VNKit` folder anywhere under your project's `Assets`.

## Quick start

1. **Create a story:** `Tools > Visual Novel > Story Editor` → **New Story**.
2. **Build the graph:** right-click the graph canvas → **Add Frame**. Select a frame to edit its
   background, characters, text and sound in the inspector on the right.
3. **Link frames:** drag a thread out of a frame's **Next** port and drop it on another frame (or
   on empty space to create one). Drag a *second* thread out of the same port to turn the frame
   into a branch with two choices.
4. **Add background/music:** use the timeline at the bottom of the window — it lays frames out in
   story order; right-click a track to add a clip, drag it to move or resize.
5. **Build the player:** `Tools > Visual Novel > Create Player In Scene`, then drag your story
   asset onto the created `VNPlayer`'s `Story` field (or set it from code before `Start()`).
6. Press Play.

## Concepts

### Frames

A frame (`VNNodeData`) is one beat of the story: a background + any number of characters (or a
single full-screen image), a line of dialogue with an optional speaker name and a typewriter
speed, an optional sound effect, and where to go next.

### Characters

A frame can show up to three characters at once, one per slot (**Left / Center / Right**). Each
character cue has its own sprite and enter animation. A slot a frame doesn't mention is hidden, so
each frame declares the full visible cast.

### Choices, conditions and effects

A frame with choices waits for the player to pick one. Each choice can:

- have **conditions** ("shown if") — a list of `variable OP value` checks; the choice is hidden
  unless *all* of them pass;
- have **effects** ("on pick") — variable changes applied the moment the choice is taken.

A frame also has **on-enter effects**, applied the instant it's shown (handy for incrementing a
counter, flipping a flag the player met a character, etc).

### Conditional branches

A frame with **no choices** can instead carry a list of conditional branches: the first one whose
conditions all pass is where the story goes; if none match, it falls back to the **Next** link.
This is how you write "if the player picked X earlier, skip ahead" without turning every fork into
a player-facing choice.

### Story variables

Declare variables once per story (**Story Variables** foldout in the inspector): a name, a type
(`Bool` / `Int` / `Float` / `String`) and a default value. At runtime they live in
`VNPlayer.Variables` (a `VNVariables` instance), which conditions read and effects write. You can
also read/write them directly from gameplay code:

```csharp
player.Variables.SetBool("met_hero", true);
int trust = player.Variables.GetInt("trust");
```

### Timeline clips

Background and music clips span a range of frames, measured in **story order** (a depth-first walk
from the start frame — the same order the player advances through at runtime) rather than wall
time, so they stay correct as you reorder or branch the story.

### Save / load

```csharp
// save
VNSaveSystem.Save(player, slot: 0);

// load (story is whatever VNStory asset the save was made from)
VNSaveSystem.Load(player, story, slot: 0);
```

Saves are plain JSON under `Application.persistentDataPath/VNSaves/`, holding the current frame id
and a snapshot of every story variable — not a reference to the story asset itself, so you supply
that the same way you would any other scene reference.

## Editor validation

The story editor's inspector shows a **Validation** panel: dangling choice/branch/next links,
frames the start frame can never reach, and a missing start frame. Click **Select frame** on an
issue to jump straight to it in the graph.

## Architecture

```
VNKit/
├── Runtime/
│   ├── VNStory.cs                  story data: frames, choices, branches, variables, clips
│   ├── VNVariables.cs               runtime variable store
│   ├── VNSaveData.cs / VNSaveSystem.cs   JSON save slots
│   ├── VNPlayer.cs                  orchestrator: flow, advance/choose, save/load entry points
│   ├── VNVisualLayerController.cs   background / character slots / single-image animation
│   ├── VNTextPresenter.cs           speaker/body text, style, typewriter
│   ├── VNChoicePresenter.cs         pooled choice buttons
│   └── VNAudioController.cs         music crossfade, sfx
└── Editor/
    ├── VNStoryEditorWindow.cs       main window: graph + inspector + timeline
    ├── VNGraphView.cs / VNNodeView.cs / VNPort.cs   the node graph
    ├── VNTimelinePanel.cs           clip timeline
    ├── VNGraphValidator.cs          dangling-link / unreachable-frame checks
    ├── VNStoryInspector.cs          custom inspector for the VNStory asset
    └── VNPlayerSetup.cs             builds the in-scene player UI
```

`VNPlayer` only orchestrates the flow; the actual work (animating layers, typing text, styling
choice buttons, crossfading music) lives in the four presenter/controller classes above, so each
piece can be tested or swapped independently of the others.

`VNStory` caches its id→frame index and story-order traversal instead of rebuilding them on every
`GetNode`/`GetOrderedNodes` call (which happens once per frame shown, and once per timeline
repaint). The cache is invalidated automatically on inspector edits (`OnValidate`) and explicitly
by the graph/editor code after programmatic edits (adding/removing a frame, relinking a thread,
undo/redo).

## Known limitations

- No localization: all story text lives directly on the `VNStory` asset.
- No dialogue log/backlog or auto-play/skip.
- The node graph has no visual port for conditional branches (task-level branches are edited from
  the inspector's **Conditional Branches** section instead of dragged like choices are).

## License

MIT — see [LICENSE](LICENSE).
