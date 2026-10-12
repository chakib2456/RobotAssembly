using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents one node in the robot's command tree, including loop bodies.
/// </summary>
[Serializable]
public class ProgramInstruction
{
    /// <summary>
    /// The kind of command stored in this instruction node.
    /// </summary>
    public InstructionType type;

    /// <summary>
    /// Number of times a FOR loop should repeat when expanded for execution.
    /// </summary>
    public int repeatCount = 1;

    /// <summary>
    /// Nested instructions contained inside a loop, if this node is a FOR block.
    /// </summary>
    [SerializeReference]
    public List<ProgramInstruction> body =
        new List<ProgramInstruction>();
}