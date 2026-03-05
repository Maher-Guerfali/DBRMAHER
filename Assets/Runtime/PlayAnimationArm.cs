using UnityEngine;

/// <summary>
/// Simple bridge to trigger an Animator parameter from graph execution.
/// </summary>
public class PlayAnimationArm : MonoBehaviour
{
    public Animator animator;
    public string triggerName = "PlayAnimation";

    [ContextMenu("Trigger Once")]
    public void Trigger()
    {
        if (animator != null)
        {
            animator.SetTrigger(triggerName);
        }
        else
        {
            Debug.LogWarning("PlayAnimationArm: Animator is not assigned! Check the Inspector.");
        }
    }
}
