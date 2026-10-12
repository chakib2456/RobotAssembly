
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Coordinates the robot programming interface: it builds the instruction list,
/// validates edits, executes the program step by step, and restores a level state
/// when a player reverts a move.
/// </summary>
public class InstructionManager : MonoBehaviour
{
    /// <summary>
    /// Reference to the robot that the program will control while it executes.
    /// </summary>
    public RobotMovement robot;

    [Header("UI")]
    public GameObject instructionPrefab;
    public Transform instructionList;
    public TMP_Text slotCounterText;
    public TMP_InputField repeatCountInput;

    /// <summary>
    /// Tracks whether the programming panel is currently visible to the player.
    /// </summary>
    public bool isProgramPanelVisible = true;
    public GameObject programPanel;

    [Header("Execution Buttons")]
    public Button runButton;
    public Button stepButton;
    public Button resetButton;
    public Button revertButton;

    public GameObject guidePanel;

    /// <summary>
    /// Static storage used when the scene reloads so the program survives a reset.
    /// </summary>
    public static List<ProgramInstruction> savedProgram;

    [Tooltip("Add, delete, clear, and End FOR buttons go here.")]
    public List<Button> editingButtons = new List<Button>();

    [Header("Program Settings")]
    public int maxInstructions = 8;
    public float stepDelay = 0.5f;

    /// <summary>
    /// Root list of instructions the player has assembled for the level.
    /// </summary>
    public List<ProgramInstruction> program =
    new List<ProgramInstruction>();

    /// <summary>
    /// Represents the currently open loop being edited.
    /// </summary>
    private ProgramInstruction activeLoop;
    private Coroutine runningProgram;

    /// <summary>
    /// Flattened list of all actions that will be executed, including repeated loop bodies.
    /// </summary>
    private List<InstructionType> executionSteps =
    new List<InstructionType>();

    /// <summary>
    /// Tracks which original instruction owns each execution step, used for revert logic.
    /// </summary>
    private List<ProgramInstruction> executionOwners =
    new List<ProgramInstruction>();

    private int currentStepIndex;
    private bool executionPrepared;
    private bool isExecuting;
    private bool isStepping;
    private bool editingLocked;
    private bool hasRunThisLevel;

    /// <summary>
    /// Lightweight snapshot of a box used to restore state after a revert.
    /// </summary>
    private class BoxSnapshot
    {
    public BoxObject box;
    public Transform originalParent;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 localScale;
    }

    /// <summary>
    /// Snapshot of robot, box, and door state before a command is executed.
    /// </summary>
    private class StepSnapshot
    {
    public Vector3 robotPosition;
    public Quaternion robotRotation;
    public Vector2Int robotGridPosition;

    public GameObject carriedBox;
    public bool isCarryingBox;

    public List<BoxSnapshot> boxes =
        new List<BoxSnapshot>();

    public List<bool> doorStates =
        new List<bool>();
    }

    /// <summary>
    /// Stack-like history of moves that can be undone one step at a time.
    /// </summary>
    private List<StepSnapshot> stepHistory =
    new List<StepSnapshot>();

    /// <summary>
    /// Doors tracked so their active states can be restored as part of a revert.
    /// </summary>
    private List<GameObject> trackedDoors =
    new List<GameObject>();

    /// <summary>
    /// Initializes the program once the scene is ready and captures the initial level state.
    /// </summary>
    void Start()
    {
        guidePanel.SetActive(false);
        if (savedProgram != null)
        {
            program = savedProgram;
            savedProgram = null;
        }
        UpdateSlotCounter();
        RefreshProgramUI();
        UpdateControlStates();

        StartCoroutine(CaptureInitialState());
    }

    /// <summary>
    /// Waits until the robot reference exists and caches any doors that need state restoration.
    /// </summary>
    IEnumerator CaptureInitialState()
    {
        while (robot == null)
            yield return null;

        // Allow LevelManager to finish spawning objects.
        yield return null;

        trackedDoors.Clear();

        // Cache doors before any trigger can deactivate them.
        trackedDoors.AddRange(
            GameObject.FindGameObjectsWithTag("Door")
        );

        Debug.Log("Initial level state captured.");
    }

    /// <summary>
    /// Returns true while the program can still be edited safely.
    /// </summary>
    bool CanEditProgram()
    {
        if (editingLocked || isExecuting || isStepping)
        {
            Debug.Log("Finish execution before editing.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Ensures the player has not reached the maximum instruction count.
    /// </summary>
    bool CanAddInstruction()
    {
        if (program.Count >= maxInstructions)
        {
            Debug.Log("Instruction limit reached!");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Clears any prepared execution plan and resets step tracking after editing the program.
    /// </summary>
    void InvalidateExecution()
    {
        executionPrepared = false;
        executionSteps.Clear();
        currentStepIndex = 0;
        stepHistory.Clear();

        UpdateControlStates();
    }

    /// <summary>
    /// Adds a standard action to the current target instruction collection.
    /// </summary>
    void AddInstruction(InstructionType type)
    {
        if (!CanEditProgram())
            return;

        ProgramInstruction instruction =
            new ProgramInstruction();

        instruction.type = type;

        if (activeLoop != null)
        {
            // Allow only one command inside each FOR loop.
            if (activeLoop.body.Count >= 1)
            {
                Debug.Log("A FOR loop can contain only one instruction!");
                return;
            }

            activeLoop.body.Add(instruction);
        }
        else
        {
            if (!CanAddInstruction())
                return;

            program.Add(instruction);
        }

        InvalidateExecution();
        RefreshProgramUI();
    }

    /// <summary>
    /// Adds a move instruction to the active program list or loop body.
    /// </summary>
    public void AddMove()
    {
        AddInstruction(InstructionType.Move);
    }

    /// <summary>
    /// Adds a left turn instruction.
    /// </summary>
    public void AddTurnLeft()
    {
        AddInstruction(InstructionType.TurnLeft);
    }

    /// <summary>
    /// Adds a right turn instruction.
    /// </summary>
    public void AddTurnRight()
    {
        AddInstruction(InstructionType.TurnRight);
    }

    /// <summary>
    /// Adds a box pickup instruction.
    /// </summary>
    public void AddPickUp()
    {
        AddInstruction(InstructionType.PickUp);
    }

    /// <summary>
    /// Adds a box drop instruction.
    /// </summary>
    public void AddDrop()
    {
        AddInstruction(InstructionType.Drop);
    }

    /// <summary>
    /// Opens a new FOR loop using the value entered in the repeat input field.
    /// </summary>
    public void AddForLoop()
    {
        if (!CanEditProgram())
            return;

        if (activeLoop != null)
        {
            Debug.Log("Nested loops are not supported yet.");
            return;
        }

        if (!CanAddInstruction())
            return;

        int repeatCount = 1;

        if (repeatCountInput != null &&
            !string.IsNullOrWhiteSpace(repeatCountInput.text))
        {
            if (!int.TryParse(
                repeatCountInput.text,
                out repeatCount))
            {
                repeatCount = 1;
            }
        }

        repeatCount = Mathf.Clamp(repeatCount, 1, 99);

        ProgramInstruction loop =
            new ProgramInstruction();

        loop.type = InstructionType.ForLoop;
        loop.repeatCount = repeatCount;

        program.Add(loop);
        activeLoop = loop;

        InvalidateExecution();
        RefreshProgramUI();
    }

    /// <summary>
    /// Closes the currently open FOR loop and re-renders the instruction list.
    /// </summary>
    public void EndForLoop()
    {
        if (!CanEditProgram())
            return;

        if (activeLoop == null)
        {
            Debug.Log("No open FOR loop.");
            return;
        }

        activeLoop = null;

        InvalidateExecution();
        RefreshProgramUI();

        Debug.Log("FOR loop closed.");
    }

    /// <summary>
    /// Removes the last instruction from the active instruction group or the main program.
    /// </summary>
    public void DeleteLastInstruction()
    {
        if (!CanEditProgram())
            return;

        if (activeLoop != null)
        {
            if (activeLoop.body.Count > 0)
            {
                activeLoop.body.RemoveAt(
                    activeLoop.body.Count - 1
                );
            }
            else
            {
                program.Remove(activeLoop);
                activeLoop = null;
            }
        }
        else if (program.Count > 0)
        {
            program.RemoveAt(program.Count - 1);
        }
        else
        {
            Debug.Log("No instructions to delete.");
            return;
        }

        InvalidateExecution();
        RefreshProgramUI();
    }

    /// <summary>
    /// Removes every instruction from the current program and resets the loop state.
    /// </summary>
    public void ClearProgram()
    {
        if (!CanEditProgram())
            return;

        program.Clear();
        activeLoop = null;

        InvalidateExecution();
        RefreshProgramUI();

        Debug.Log("Program cleared.");
    }

    /// <summary>
    /// Updates the visible counter showing how many instructions are currently in the program.
    /// </summary>
    public void UpdateSlotCounter()
    {
        if (slotCounterText != null)
        {
            slotCounterText.text =
                "Instructions: " + program.Count +
                " / " + maxInstructions;
        }
    }

    /// <summary>
    /// Rebuilds the list of instruction rows shown in the visual programming panel.
    /// </summary>
    void RefreshProgramUI()
    {
        if (instructionList == null || instructionPrefab == null)
        {
            UpdateSlotCounter();
            return;
        }

        foreach (Transform child in instructionList)
            Destroy(child.gameObject);

        for (int i = 0; i < program.Count; i++)
        {
            ProgramInstruction instruction = program[i];

            if (instruction.type == InstructionType.ForLoop)
            {
                DisplayLine(
                    (i + 1) + "  FOR " +
                    instruction.repeatCount + " TIMES"
                );

                foreach (ProgramInstruction bodyInstruction
                         in instruction.body)
                {
                    DisplayLine(
                        "    ↳ " +
                        GetInstructionName(bodyInstruction.type)
                    );
                }

                if (instruction != activeLoop)
                    DisplayLine("    END Loop");
            }
            else
            {
                DisplayLine(
                    (i + 1) + "  " +
                    GetInstructionName(instruction.type)
                );
            }
        }

        UpdateSlotCounter();
    }

    /// <summary>
    /// Instantiates a single text row in the instruction list UI.
    /// </summary>
    void DisplayLine(string line)
    {
        GameObject newInstruction =
            Instantiate(instructionPrefab, instructionList);

        TMP_Text text =
            newInstruction.GetComponent<TMP_Text>();

        if (text != null)
            text.text = line;
    }

    /// <summary>
    /// Converts an instruction enum into a readable label for the UI.
    /// </summary>
    string GetInstructionName(InstructionType type)
    {
        switch (type)
        {
            case InstructionType.Move:
                return "> MOVE";

            case InstructionType.TurnLeft:
                return "> TURN LEFT";

            case InstructionType.TurnRight:
                return "> TURN RIGHT";

            case InstructionType.PickUp:
                return "> PICK UP";

            case InstructionType.Drop:
                return "> DROP";

            default:
                return type.ToString().ToUpper();
        }
    }

    /// <summary>
    /// Expands all nested FOR loops into a flat execution sequence before the program runs.
    /// </summary>
    void PrepareExecution()
    {
        executionSteps.Clear();
        executionOwners.Clear();

        foreach (ProgramInstruction instruction in program)
        {
            AddExpandedInstructions(instruction, instruction);
        }

        currentStepIndex = 0;
        executionPrepared = true;
    }

    /// <summary>
    /// Recursively flattens a looped instruction tree into individual runtime steps.
    /// </summary>
    void AddExpandedInstructions(
        ProgramInstruction instruction,
        ProgramInstruction owner)
    {
        if (instruction.type == InstructionType.ForLoop)
        {
            for (int i = 0; i < instruction.repeatCount; i++)
            {
                foreach (ProgramInstruction bodyInstruction
                        in instruction.body)
                {
                    AddExpandedInstructions(bodyInstruction, owner);
                }
            }
        }
        else
        {
            executionSteps.Add(instruction.type);
            executionOwners.Add(owner);
        }
    }

    // --------------------------------------------------
    // SNAPSHOT SYSTEM
    // --------------------------------------------------

    /// <summary>
    /// Saves the current level state before a command is executed so it can be restored later.
    /// </summary>
    StepSnapshot CaptureStepSnapshot()
    {
        StepSnapshot snapshot = new StepSnapshot();

        snapshot.robotPosition = robot.transform.position;
        snapshot.robotRotation = robot.transform.rotation;
        snapshot.robotGridPosition = robot.gridPosition;

        snapshot.carriedBox = robot.carriedBox;
        snapshot.isCarryingBox = robot.isCarryingBox;

        BoxObject[] boxes =
            FindObjectsByType<BoxObject>(
                FindObjectsSortMode.None
            );

        foreach (BoxObject box in boxes)
        {
            if (box == null)
                continue;

            BoxSnapshot boxSnapshot = new BoxSnapshot();

            boxSnapshot.box = box;
            boxSnapshot.originalParent = box.transform.parent;
            boxSnapshot.position = box.transform.position;
            boxSnapshot.rotation = box.transform.rotation;
            boxSnapshot.localScale = box.transform.localScale;

            snapshot.boxes.Add(boxSnapshot);
        }

        foreach (GameObject door in trackedDoors)
        {
            snapshot.doorStates.Add(
                door != null && door.activeSelf
            );
        }

        return snapshot;
    }

    /// <summary>
    /// Restores a previously captured state to undo the last executed program step.
    /// </summary>
    void RestoreStepSnapshot(StepSnapshot snapshot)
    {
        // Restore robot transform and grid position.
        robot.transform.position = snapshot.robotPosition;
        robot.transform.rotation = snapshot.robotRotation;
        robot.gridPosition = snapshot.robotGridPosition;

        robot.carriedBox = snapshot.carriedBox;
        robot.isCarryingBox = snapshot.isCarryingBox;

        if (robot.animator != null)
        {
            robot.animator.SetBool(
                "PickUp",
                robot.isCarryingBox
            );
        }

        // Restore box positions and parenting.
        foreach (BoxSnapshot boxSnapshot in snapshot.boxes)
        {
            if (boxSnapshot.box == null)
                continue;

            Transform boxTransform =
                boxSnapshot.box.transform;

            boxTransform.SetParent(
                boxSnapshot.originalParent
            );

            boxTransform.localScale = boxSnapshot.localScale;
            boxTransform.rotation = boxSnapshot.rotation;
            boxTransform.position = boxSnapshot.position;
        }

        // Restore door active states.
        for (int i = 0;
             i < trackedDoors.Count &&
             i < snapshot.doorStates.Count;
             i++)
        {
            if (trackedDoors[i] != null)
            {
                trackedDoors[i].SetActive(
                    snapshot.doorStates[i]
                );
            }
        }
    }

    // --------------------------------------------------
    // RUN
    // --------------------------------------------------

    /// <summary>
    /// Starts the current program and executes all remaining queued steps automatically.
    /// </summary>
    public void RunProgram()
    {
        if (hasRunThisLevel)
        {
            Debug.Log(
                "Program has already run this level. " +
                "Revert a step or Reset to try again."
            );
            return;
        }

        if (robot == null)
        {
            Debug.LogError("No robot assigned!");
            return;
        }

        if (activeLoop != null)
        {
            Debug.Log("Press END FOR before running.");
            return;
        }

        if (isExecuting || isStepping)
            return;

        if (!executionPrepared)
            PrepareExecution();

        if (currentStepIndex >= executionSteps.Count)
        {
            Debug.Log("There are no more commands to run.");
            return;
        }

        hasRunThisLevel = true;
        editingLocked = true;
        isExecuting = true;

        UpdateControlStates();

        runningProgram =
            StartCoroutine(ExecuteRemainingSteps());
    }

    /// <summary>
    /// Executes the remainder of the flattened instruction sequence at a timed pace.
    /// </summary>
    IEnumerator ExecuteRemainingSteps()
    {
        while (currentStepIndex < executionSteps.Count)
        {
            // Save state BEFORE executing this command.
            stepHistory.Add(CaptureStepSnapshot());

            UpdateControlStates();

            ExecuteSingleInstruction(
                executionSteps[currentStepIndex]
            );

            while (robot.IsMoving)
                yield return null;

            currentStepIndex++;

            UpdateControlStates();

            yield return new WaitForSeconds(stepDelay);
        }

        runningProgram = null;
        isExecuting = false;
        editingLocked = false;

        UpdateControlStates();

        Debug.Log("Program execution finished.");
    }

    // --------------------------------------------------
    // STEP
    // --------------------------------------------------

    /// <summary>
    /// Executes a single instruction immediately and then waits for the robot animation to finish.
    /// </summary>
    public void StepProgram()
    {
        if (robot == null)
        {
            Debug.LogError("No robot assigned!");
            return;
        }

        if (activeLoop != null)
        {
            Debug.Log("Press END FOR before stepping.");
            return;
        }

        if (isExecuting || isStepping)
            return;

        if (!executionPrepared)
            PrepareExecution();

        if (currentStepIndex >= executionSteps.Count)
        {
            Debug.Log("No more steps. Press Revert or Reset.");
            return;
        }

        isStepping = true;
        editingLocked = true;
        ExecuteSingleInstruction(
        executionSteps[currentStepIndex]
        );

        currentStepIndex++;


        UpdateControlStates();

        StartCoroutine(ExecuteOneStep());
    }

    /// <summary>
    /// Waits for the robot's current movement to complete before enabling editing again.
    /// </summary>
    IEnumerator ExecuteOneStep()
    {
        // Save state BEFORE executing this command.
        stepHistory.Add(CaptureStepSnapshot());

        ExecuteSingleInstruction(
            executionSteps[currentStepIndex]
        );

        // Wait for smooth movement, if applicable.
        while (robot.IsMoving)
            yield return null;

        currentStepIndex++;

        isStepping = false;
        editingLocked = false;

        UpdateControlStates();

        if (currentStepIndex >= executionSteps.Count)
            Debug.Log("Program execution finished.");
    }

    // --------------------------------------------------
    // REVERT
    // --------------------------------------------------

    /// <summary>
    /// Restores the level to the state before the most recent executed instruction.
    /// </summary>
    public void RevertStep()
    {
        if (isExecuting || isStepping)
            return;

        if (stepHistory.Count == 0)
        {
            Debug.Log("Nothing to revert!");
            return;
        }

        // Identify the instruction that was just reverted.
        int executedIndex = currentStepIndex - 1;

        ProgramInstruction revertedOwner = null;

        if (executedIndex >= 0 &&
            executedIndex < executionOwners.Count)
        {
            revertedOwner = executionOwners[executedIndex];
        }

        // Restore the previous game state.
        int lastIndex = stepHistory.Count - 1;
        StepSnapshot snapshot = stepHistory[lastIndex];

        stepHistory.RemoveAt(lastIndex);
        RestoreStepSnapshot(snapshot);

        // Move execution back one step.
        currentStepIndex = Mathf.Max(0, currentStepIndex - 1);

        // Find the first execution belonging to this instruction.
        int firstOwnerIndex = -1;

        if (revertedOwner != null)
        {
            firstOwnerIndex = executionOwners.IndexOf(revertedOwner);
        }

        // Delete the instruction only after all its executions
        // have been reverted.
        if (revertedOwner != null &&
            firstOwnerIndex >= 0 &&
            currentStepIndex <= firstOwnerIndex)
        {
            program.Remove(revertedOwner);

            // Rebuild execution lists without resetting the index.
            executionSteps.Clear();
            executionOwners.Clear();

            foreach (ProgramInstruction instruction in program)
            {
                AddExpandedInstructions(instruction, instruction);
            }

            currentStepIndex = Mathf.Min(
                currentStepIndex,
                executionSteps.Count
            );

            executionPrepared = true;

            Debug.Log("Instruction fully reverted and deleted.");
        }
        else
        {
            Debug.Log("Step reverted. Instruction remains in the program.");
        }

        RefreshProgramUI();

        hasRunThisLevel = false;
        editingLocked = false;

        UpdateControlStates();
    }

    // --------------------------------------------------
    // COMMAND EXECUTION
    // --------------------------------------------------

    /// <summary>
    /// Calls the matching robot method for the given instruction type.
    /// </summary>
    void ExecuteSingleInstruction(InstructionType type)
    {
        switch (type)
        {
            case InstructionType.Move:
                robot.MoveRobot();
                break;

            case InstructionType.TurnLeft:
                robot.TurnLeft();
                break;

            case InstructionType.TurnRight:
                robot.TurnRight();
                break;

            case InstructionType.PickUp:
                robot.PickUpBox();
                break;

            case InstructionType.Drop:
                robot.DropBox();
                break;
        }
    }

    /// <summary>
    /// Saves the current program and reloads the active scene so the level can be replayed.
    /// </summary>
    public void ResetProgram()
    {
        // Save the current instructions before reloading.
        savedProgram = new List<ProgramInstruction>();

        foreach (ProgramInstruction instruction in program)
        {
            savedProgram.Add(CloneInstruction(instruction));
        }

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    /// <summary>
    /// Deep clones a program node so it can be restored after a scene reset.
    /// </summary>
    ProgramInstruction CloneInstruction(ProgramInstruction original)
    {
        ProgramInstruction copy = new ProgramInstruction();

        copy.type = original.type;
        copy.repeatCount = original.repeatCount;

        foreach (ProgramInstruction instruction in original.body)
        {
            copy.body.Add(CloneInstruction(instruction));
        }

        return copy;
    }

    // --------------------------------------------------
    // UI CONTROL STATES
    // --------------------------------------------------

    /// <summary>
    /// Enables or disables editing and execution buttons based on the current runtime state.
    /// </summary>
    void UpdateControlStates()
    {
        bool canEdit =
            !editingLocked &&
            !isExecuting &&
            !isStepping;

        foreach (Button button in editingButtons)
        {
            if (button != null)
                button.interactable = canEdit;
        }

        if (repeatCountInput != null)
            repeatCountInput.interactable = canEdit;

        if (runButton != null)
            runButton.interactable =
                !isExecuting && !isStepping;

        if (stepButton != null)
            stepButton.interactable =
                !isExecuting && !isStepping;

        if (revertButton != null)
        {
            revertButton.interactable =
                stepHistory.Count > 0 &&
                !isExecuting &&
                !isStepping;
        }

        // Reset stays available during execution.
        if (resetButton != null)
            resetButton.interactable = true;
    }

    /// <summary>
    /// Toggles the visibility of the programming panel in the level UI.
    /// </summary>
    public void TerminalLog()
    {
        isProgramPanelVisible = !isProgramPanelVisible;

        if (programPanel != null)
            programPanel.SetActive(isProgramPanelVisible);
    }

    public void GuidePanel()
    {
        if (guidePanel != null)
            guidePanel.SetActive(true);
    }
    public void CloseGuidePanel()
    {
        if (guidePanel != null)
            guidePanel.SetActive(false);
    }
}
