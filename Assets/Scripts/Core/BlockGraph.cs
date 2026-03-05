using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BlockSystem.Core
{
    // ══════════════════════════════════════════════════════════════════════
    //  BlockGraph  —  the saved data asset that holds your whole graph.
    //
    //  Think of it as the "project file" for a single behaviour graph.
    //  It stores two lists:
    //    • blocks      — every node you've placed in the editor.
    //    • connections — every wire you've drawn between ports.
    //
    //  Create one in the Project window:
    //    Right-click → Create → BlockSystem → Block Graph
    //  Then drag it onto the GameManager component to run it.
    //
    //  You rarely need to call these methods manually — the editor and
    //  GraphRunner do it for you.  They're documented here so you know
    //  what's available if you ever script against the graph directly.
    // ══════════════════════════════════════════════════════════════════════
    [CreateAssetMenu(menuName = "BlockSystem/Block Graph", fileName = "NewBlockGraph")]
    public class BlockGraph : ScriptableObject
    {
        // All blocks in the graph.  [SerializeReference] is required so Unity
        // can save polymorphic types (MoveBlock, DelayBlock, etc.) correctly.
        [SerializeReference] public List<Block> blocks = new();

        // All wires.  Each Connection records which port on which block
        // connects to which port on which other block.
        public List<Connection> connections = new();

        // ── Adding / removing blocks ──────────────────────────────────────

        // Creates a block of the given type and adds it to the graph.
        // Used by the graph editor when you pick a node from the search menu.
        public Block AddBlock(Type blockType)
        {
            if (!typeof(Block).IsAssignableFrom(blockType))
            {
                Debug.LogError($"{blockType.Name} is not a Block");
                return null;
            }

            var block = (Block)Activator.CreateInstance(blockType);
            blocks.Add(block);
            return block;
        }

        // Generic version — handy when the type is known at compile time.
        // Example:  graph.AddBlock<DelayBlock>();
        public T AddBlock<T>() where T : Block, new()
        {
            var block = new T();
            blocks.Add(block);
            return block;
        }

        // Removes a block and automatically cleans up all wires that touched it.
        // Always use this instead of removing from the list directly.
        public void RemoveBlock(string blockId)
        {
            blocks.RemoveAll(b => b.id == blockId);
            connections.RemoveAll(c => c.fromBlockId == blockId || c.toBlockId == blockId);
        }

        // Look up a block by its short 8-char id.
        public Block GetBlock(string id) => blocks.FirstOrDefault(b => b.id == id);

        // ── Connecting / disconnecting ports ──────────────────────────────

        // Draws a wire from an output port to an input port.
        // Returns false (and logs a warning) if the port types don't match
        // or the wire already exists.
        public bool Connect(string fromBlockId, string fromPort, string toBlockId, string toPort)
        {
            var from = GetBlock(fromBlockId);
            var to   = GetBlock(toBlockId);
            if (from == null || to == null) return false;

            var outPort = from.GetOutput(fromPort);
            var inPort  = to.GetInput(toPort);
            if (outPort == null || inPort == null) return false;

            // Flow ports must only connect to flow ports.
            // Data ports must have the same data type on both ends.
            if (outPort.type != inPort.type)
            {
                Debug.LogWarning($"Port type mismatch: {outPort.type} -> {inPort.type}");
                return false;
            }

            // Prevent duplicate wires between the same two ports.
            if (connections.Any(c => c.fromBlockId == fromBlockId && c.fromPortName == fromPort
                && c.toBlockId == toBlockId && c.toPortName == toPort))
                return false;

            connections.Add(new Connection(fromBlockId, fromPort, toBlockId, toPort));
            return true;
        }

        // Removes the specific wire between two ports.
        public void Disconnect(string fromBlockId, string fromPort, string toBlockId, string toPort)
        {
            connections.RemoveAll(c => c.fromBlockId == fromBlockId && c.fromPortName == fromPort
                && c.toBlockId == toBlockId && c.toPortName == toPort);
        }

        // ── Querying connections ──────────────────────────────────────────

        // Returns every wire that leaves a given output port.
        // A flow output on ParallelBlock can have many wires — that's why
        // this returns a list instead of a single connection.
        public List<Connection> GetOutputConnections(string blockId, string portName)
        {
            return connections.Where(c => c.fromBlockId == blockId && c.fromPortName == portName).ToList();
        }

        // Returns the single wire feeding into an input port.
        // Data input ports only accept one source (one wire in).
        // Returns null if nothing is connected.
        public Connection GetInputConnection(string blockId, string portName)
        {
            return connections.FirstOrDefault(c => c.toBlockId == blockId && c.toPortName == portName);
        }

        // Finds blocks where execution should begin.
        // An entry block is any block that has a flow input port but no
        // wire coming into that port — meaning nothing triggers it, so it
        // must be a starting point (like an OnStart or a Spawn node).
        public List<Block> GetEntryBlocks()
        {
            var blocksWithIncomingFlow = connections
                .Where(c =>
                {
                    var target = GetBlock(c.toBlockId);
                    var port   = target?.GetInput(c.toPortName);
                    return port != null && port.type == PortType.Flow;
                })
                .Select(c => c.toBlockId)
                .ToHashSet();

            return blocks.Where(b =>
                !blocksWithIncomingFlow.Contains(b.id)
                && b.inputs.Any(p => p.type == PortType.Flow))
                .ToList();
        }

        // Wipes all blocks and connections.  Used before loading a new graph
        // from JSON so we start from a clean state.
        public void Clear()
        {
            blocks.Clear();
            connections.Clear();
        }
    }
}
