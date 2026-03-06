# Digital Twin Factory - Node Graph Viewer

![Unity Version](https://img.shields.io/badge/Unity-2022.3+-blue)
![License](https://img.shields.io/badge/License-MIT-green)
![Status](https://img.shields.io/badge/Status-Active-success)

**Author:** Maher Guerfali  
**Created:** March 5, 2026  
**Project Type:** Node-Based Visual Scripting System for Digital Twin Factory Simulation

A powerful **node-based visual scripting system** built with Unity's GraphView. Design complex factory automation sequences, character animations, and environmental interactions without writing code. Perfect for digital twins, game logic, and interactive simulations.
<img width="1226" height="676" alt="Screenshot 2026-03-06 at 04 00 25" src="https://github.com/user-attachments/assets/6995c74d-3b3c-4846-8238-8bc996c3ad2d" />

---

## 🎯 Overview

This project provides a **visual node editor** with a complete execution engine for creating complex behaviors through node graphs. Instead of traditional scripting, connect blocks visually to create:

- **Factory automation sequences** — Control robotic arms, conveyor belts, and machinery
- **Character animations** — Chain multiple animation states with branching logic
- **Environmental interactions** — Spawn objects, trigger events, execute custom component methods
- **AI behaviors** *(beta)* — Node-based prompt system for AI-driven decision making

### Key Features

✨ **Node-Based Editor**
- Intuitive visual node graph interface in Unity Inspector/Editor
- Real-time graph execution with pause/resume support
- Full undo/redo, copy/paste, duplicate functionality

⚙️ **Rich Block Library** (18+ blocks)
- **Transform:** Move, Rotate, Scale (animated with DOTween)
- **Logic:** Branch (if/else), Compare, Parallel execution
- **Utility:** Delay, Sequence, ForEachTag, Repeat, DebugLog
- **Component:** Invoke any component method via reflection
- **Values:** Constant values, Vector3, Float, Bool, String
- **Spawning:** Instantiate GameObjects from prefabs
- **AI:** Prompt-based nodes for LLM integration *(beta)*

🔄 **Flow Control**
- Linear execution (Start → Block → Next)
- Conditional branching (if/then/else)
- Parallel execution (run multiple blocks simultaneously)
- Loop support (repeat until condition)
- Delay/timer integration

💾 **Save & Load**
- Export graphs to JSON
- Load and execute saved graphs at runtime
- Reflection-based serialization for flexibility

🎨 **Professional Animation System**
- DOTween integration for smooth transitions
- Linear, eased animations with customizable duration
- Automatic graph pause/resume during animations

---

## 🚀 Quick Start

### 1. Creating a Block Graph

```
1. Right-click in Project → Create → BlockGraph
2. Open the graph asset to launch the node editor
3. Drag nodes from the palette and connect them
4. Save your graph
```

### 2. Setting Up Runtime Execution

```csharp
// Attach GameManager to a GameObject
public BlockGraph graph;  // Assign your graph asset
// Calls RunGraph() on Start
```

### 3. Adding Nodes to Your Graph

**Example 1: Move object with animation**
```
MoveBlock (offset: 0,10,0, speed: 20) 
  → DelayBlock (duration: 1s)
    → MoveBlock (offset: 0,-10,0, speed: 20)
```

**Example 2: Conditional branching**
```
CompareBlock (distance < 5)
  → True: PlayAnimationArm (trigger: "Explode")
  → False: DebugLogBlock (message: "Too far")
```

---

## 📚 Architecture & Design

### Core Concepts

**Blocks** — Visual nodes representing actions or logic
- Every block inherits from `Block` class
- Declares input/output ports in `SetupPorts()`
- Implements logic in `Execute(GraphContext ctx)`

**Ports** — Typed data connections
- **Flow ports:** Control execution order (like wires)
- **Data ports:** Pass values between blocks (Vector3, Float, Bool, etc.)
- **Strongly typed:** Runtime safety with enum-based type system

**Graph Execution Pipeline**
```
1. GraphRunner.Run() starts execution
2. Execute first block (usually a flow input)
3. Block reads input ports via In<T>("PortName")
4. Block performs logic
5. Block writes output ports via Out("PortName", value)
6. GraphRunner follows connected output port to next block
7. Repeat until no more blocks or graph pauses
```

### Data Flow

| Step | What Happens |
|------|--------------|
| **Before Execute** | GraphRunner copies upstream output values into this block's input ports |
| **During Execute** | Block calls `In<T>("PortName")` to read data |
| **Block Logic** | Perform custom operations with the input data |
| **After Execute** | Block calls `Out("PortName", value)` to publish results |
| **Flow Following** | GraphRunner copies output values to downstream input ports, executes next block |

### Pause/Resume Pattern (For Animations & Delays)

```csharp
// In Execute() method:
ctx.IsPaused = true;  // Tell runner to stop executing

// Start animation/timer
animationTween.OnComplete(() =>
{
    ctx.IsPaused = false;  // Resume execution
    if (nextId != null)
        ctx.Executor.Resume(nextId);  // Execute next block
});
```

---

## 🛠️ Creating Custom Blocks

### Step 1: Create Your Block Class

```csharp
using UnityEngine;
using BlockSystem.Core;

namespace BlockSystem.Blocks
{
    /// <summary>
    /// Custom block that demonstrates a simple pattern.
    /// </summary>
    [System.Serializable]
    public class MyCustomBlock : Block
    {
        public float multiplier = 2f;

        protected override void SetupPorts()
        {
            AddInput("Start", PortType.Flow);
            AddInput("Value", PortType.Float);
            AddOutput("Next", PortType.Flow);
            AddOutput("Result", PortType.Float);
        }

        public override void Execute(GraphContext ctx)
        {
            // Read input
            float input = In<float>("Value");
            
            // Process
            float result = input * multiplier;
            
            // Write output
            Out("Result", result);
        }
    }
}
```

### Step 2: BlockRegistry Auto-Discovery

No registration needed! The system automatically discovers your block:
1. Place the script in `Assets/Scripts/Blocks/` folder
2. It inherits from `Block`
3. Restart Unity Editor
4. Your block appears in the node palette

### Step 3: Use in Graph

1. Open a BlockGraph
2. Find your block in the palette (listed under a category)
3. Drag it into the graph
4. Configure inspector fields
5. Connect ports to other blocks

---

## 🔧 Editing Existing Nodes

### In the Inspector

```
1. Click on a node in the graph editor
2. Modify fields in the Inspector
3. Changes apply immediately
4. Use Color Tint to visually identify node types
```

### Via Code

Each block exposes public fields that appear in the Inspector:

```csharp
// MoveBlock example
public Vector3 offset = new Vector3(0, 10, 0);  // Visible & editable
public float speed = 20f;                       // Can override via port
public string targetName;                       // Inspector fallback
```

### Ports vs Inspector Values

- **Port connected** → Use port value
- **Port empty** → Use inspector field value
- **Port + inspector** → Port takes priority

---

## 📖 Scripts & Flow Explanation

### Core Engine (`Assets/Scripts/Core/`)

**Block.cs**
- Abstract base class for all nodes
- Provides `In<T>()` and `Out()` helpers for port access
- Manages port registration and lifecycle

**GraphContext.cs**
- Runtime data passed to every block during execution
- Provides access to: graph structure, object registry, executor, flow control
- Used for pausing/resuming execution

**GraphRunner.cs**
- The execution engine
- Maintains execution loop, handles pause/resume
- Manages flow following and block invocation order

### Transform Blocks (`Assets/Scripts/Blocks/Transform/`)

**MoveBlock.cs** — Animate GameObject position
- **Inputs:** Target (GameObject), Offset (Vector3), Speed (Float)
- **Outputs:** Next (Flow)
- **Logic:** Uses DOTween.DOMove() for smooth animation
- **Pause Pattern:** Pauses graph during animation, resumes on completion

**RotateBlock.cs** — Animate GameObject rotation
- **Inputs:** Target (GameObject), Angles (Vector3), Speed (Float)
- **Outputs:** Next (Flow)
- **Logic:** Uses DOTween.DORotate() with FastBeyond360 mode
- **Note:** Speed is degrees-per-second

**ScaleBlock.cs** — Animate or set GameObject scale
- **Inputs:** Target (GameObject), Scale (Vector3), Duration (Float)
- **Outputs:** Next (Flow)
- **Logic:** DOTween.DOScale() for animation or instant if Duration ≤ 0

### Utility Blocks (`Assets/Scripts/Blocks/Utility/`)

**ComponentInvokeBlock.cs** — Call any component method
- Reflects on component type to find methods/properties/fields
- Converts string parameters to correct types
- Useful for triggering animations, playing sounds, calling custom logic

**DelayBlock.cs** — Wait before proceeding
- Supports GameTime, Realtime, or Frame counting
- Pauses graph execution until timer expires

**DebugLogBlock.cs** — Log messages to console
- Helpful for tracing graph execution
- Supports dynamic message input

### Logic Blocks (`Assets/Scripts/Blocks/Logic/`)

**BranchBlock.cs** — Conditional execution
- Reads boolean condition
- Routes execution to "True" or "False" branch
- Implements the if/then/else pattern

**CompareBlock.cs** — Compare two values
- Outputs: Equal, Greater, Less
- Three output ports for different comparison results

### Runtime Integration (`Assets/Runtime/`)

**GameManager.cs**
- Executes BlockGraph at runtime
- Loads and imports JSON graphs
- Demonstrates interface-based architecture

**PlayAnimationArm.cs**
- Example component for animator integration
- Called via ComponentInvokeBlock
- Shows how to expose component methods to graphs

---

## 📊 Execution Flow Diagram

```
START
  ↓
[Block 1] (Execute, read inputs, write outputs)
  ↓
[GraphRunner] (Copy outputs → next inputs)
  ↓
[Block 2] (Execute with received data)
  ↓
... continue until END or PAUSE
  ↓
**IF ANIMATED:**
  MoveBlock pauses: ctx.IsPaused = true
  ↓
  [DOTween Animation Running]
  ↓
  Animation completes → OnComplete() callback
  ↓
  ctx.IsPaused = false; Resume(nextBlockId)
  ↓
  [Continue to Block 3]
END
```

---

## 🎮 Example Use Cases

### Robotic Arm Animation Sequence

```
Start Flow
  ↓
MoveBlock (position: forward 5 units, speed: 10)
  ↓
RotateBlock (rotation: 90° on Y axis, speed: 45)
  ↓
ComponentInvokeBlock (call PlayAnimationArm.Trigger())
  ↓
DelayBlock (wait 1 second)
  ↓
MoveBlock (position: back to origin, speed: 10)
  ↓
End
```

### Conditional Factory Logic

```
Start Flow
  ↓
CompareBlock (check: objectCount > 10)
  ├─ True:
  │   ├→ SpawnBlock (spawn 5 defect items)
  │   └→ DebugLogBlock ("Factory overloaded!")
  │
  └─ False:
      ├→ MoveBlock (process item)
      └→ DebugLogBlock ("Processing normal")
```

---

## 🎨 Editor Features

### Node Palette
- Organized by category (Transform, Logic, Utility, etc.)
- Search by name
- Drag to create new nodes

### Graph Editor
- **Zoom & Pan:** Mouse wheel + right-click drag
- **Create Node:** Right-click → search or select
- **Connect Ports:** Click port → drag → click target port
- **Delete:** Select node + Delete key
- **Duplicate:** Ctrl+D
- **Undo/Redo:** Ctrl+Z / Ctrl+Y

### Serialization
- Automatic JSON export/import
- Preserves graph structure and block values
- Custom serializers for complex types

---

## 📦 Dependencies & Credits

### Core Dependencies

| Package | Version | Purpose | License |
|---------|---------|---------|---------|
| **DOTween** | 1.x | Smooth animations & tweening | Proprietary |
| **Free Outline Shader** | Latest | Highlight important factory elements | MIT |
| **UI Rounder** | Latest | Rounded corner UI elements | MIT |
| **Unity GraphView** | 2022.3+ | Node editor framework | Unity |

### Special Thanks

- **DOTween** — This project uses DOTween for all smooth animations (movement, rotation, scaling)
- **Free Outline Shader** — Used to highlight interactive and important elements in factory scenes
- **UI Rounder** — Provides beautiful rounded corners for UI components throughout the application

---

## 🔮 Features (Beta)

### AI Prompt Node *(Currently in Beta)*

The AI Prompt node allows integration with language models for intelligent decision-making:

```csharp
// Coming Soon:
// - Send graph state to LLM
// - Get branching decisions from AI
// - Multi-turn conversations with context
```

**Status:** Beta - Core infrastructure ready, awaiting LLM integration

---

## 📁 Project Structure

```
Assets/
├── Scripts/
│   ├── Core/                 # Engine foundation (Block, GraphContext, GraphRunner)
│   ├── Blocks/              # All block implementations
│   │   ├── Transform/       # Move, Rotate, Scale
│   │   ├── Logic/          # Branch, Compare
│   │   ├── Utility/        # Delay, Invoke, DebugLog
│   │   ├── Values/         # Constant values
│   │   ├── Spawn/          # Instantiation
│   │   └── AI/             # AI Prompt (beta)
│   ├── Editor/             # GraphView editor and UI
│   └── Serialization/      # JSON save/load
├── Runtime/                # Game integration & examples
├── Animations/             # .anim and .controller files
├── Models/                 # FBX assets
├── Textures/              # Images and sprites
├── Materials/             # Material assets
├── Prefabs/               # Reusable GameObject templates
└── SavedGraphs/           # BlockGraph assets
```

---

## 🚦 Getting Started

### Installation

1. Clone or download the project
2. Open in Unity 2022.3+
3. Wait for compilation
4. Create a BlockGraph asset: Right-click → Create → BlockGraph
5. Double-click to open the editor

### First Graph

```
1. Open a BlockGraph asset
2. Right-click → Create Block → Debug → DebugLogBlock
3. Right-click → Create Block → Utility → DelayBlock
4. Click DebugLogBlock output port → Drag to DelayBlock input
5. Attach GameManager to a GameObject in your scene
6. Assign the graph to GameManager.graph field
7. Hit Play!
```

### Creating a Custom Block

1. Create new C# script in `Assets/Scripts/Blocks/MyCategory/`
2. Inherit from `Block`
3. Override `SetupPorts()` and `Execute()`
4. No registration needed!
5. Block appears in editor automatically

---

## 🤝 Contributing

Found a bug? Have a feature request? Contributions are welcome!

1. Create an issue describing the problem
2. Fork and create a feature branch
3. Make your changes
4. Submit a pull request

---

## 📄 License

This project is licensed under the MIT License — see the LICENSE file for details.

---

## 👨‍💻 Author

**Maher Guerfali**  
Digital Twin Factory Visualization & Node-Based Scripting System  
Created: March 5, 2026

---

## 🔗 Resources

- [Unity Documentation](https://docs.unity.com)
- [DOTween Documentation](http://dotween.demigiant.com/)
- [GraphView Tutorial](https://docs.unity3d.com/Manual/GraphView.html)

---

## ⚠️ Known Limitations

- Graph execution is single-threaded (sequential blocks)
- Large graphs (1000+ blocks) may impact editor performance
- AI Prompt node is in beta and requires manual LLM setup
- Undo/redo has memory overhead for large graphs

---

**Happy node building! 🎉**

For support, documentation, or feature requests, please visit the project repository.
