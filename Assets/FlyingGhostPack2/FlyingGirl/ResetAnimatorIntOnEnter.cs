using UnityEngine;

namespace GraveyardTerror
{
    /// <summary>
    /// Clears an animator int when a state begins so a turn value does not fire again.
    /// </summary>
    public class ResetAnimatorIntOnEnter : StateMachineBehaviour
    {
        public string parameter = "Turn";

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.SetInteger(parameter, 0);
        }
    }
}
