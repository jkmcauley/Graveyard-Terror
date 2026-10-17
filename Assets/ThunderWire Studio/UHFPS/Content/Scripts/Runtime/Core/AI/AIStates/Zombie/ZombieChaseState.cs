using UnityEngine;
using UHFPS.Scriptable;
using UHFPS.Tools;

namespace UHFPS.Runtime.States
{
    public class ZombieChaseState : AIStateAsset
    {
        public float RunSpeed = 3f;
        public float ChaseStoppingDistance = 1.5f;

        [Header("Chase")]
        public float LostPlayerPatrolTime = 5f;
        public float LostPlayerPredictTime = 1f;
        public float VeryClosePlayerDetection = 1.5f;

        [Header("Attack")]
        public float AttackFOV = 30f;
        public float AttackDistance = 1.35f;

        public override FSMAIState InitState(NPCStateMachine machine, AIStatesGroup group)
        {
            return new ChaseState(machine, group, this);
        }

        public override string StateKey => "Chase";
        public override string Name => "Zombie/Chase";

        public class ChaseState : FSMAIState
        {
            private readonly ZombieStateGroup Group;
            private readonly ZombieChaseState State;

            private bool isChaseStarted;
            private bool isPatrolPending;
            private bool resetParameters;

            private float waitTime;
            private float predictTime;
            private bool playerDied;

            public ChaseState(NPCStateMachine machine, AIStatesGroup group, AIStateAsset state) : base(machine)
            {
                Group = (ZombieStateGroup)group;
                State = (ZombieChaseState)state;

                machine.CatchMessage("Attack", () => AttackPlayer());
            }

            public override Transition[] OnGetTransitions()
            {
                return new Transition[]
                {
                    Transition.To<ZombiePatrolState>(() => waitTime > State.LostPlayerPatrolTime || playerDied),
                    Transition.To<ZombiePlayerHideState>(() => playerMachine.IsCurrent(PlayerStateMachine.HIDING_STATE))
                };
            }

            public override void OnStateEnter()
            {
                Group.ResetAnimatorPrameters(animator);
                agent.speed = State.RunSpeed;
                agent.stoppingDistance = State.ChaseStoppingDistance;
                machine.RotateAgentManually = true;
                isChaseStarted = true;
            }

            public override void OnStateExit()
            {
                machine.RotateAgentManually = false;
                machine.FacePlayerWhileManual = false;
                isChaseStarted = false;
                isPatrolPending = false;
                resetParameters = false;
                waitTime = 0f;
                predictTime = 0f;
            }

            public override void OnPlayerDeath()
            {
                animator.ResetTrigger(Group.AttackTrigger);
                playerDied = true;
            }

            public override void OnStateUpdate()
            {
                if (PlayerInSights())
                {
                    if (!resetParameters)
                    {
                        Group.ResetAnimatorPrameters(animator);
                        animator.SetBool(Group.RunParameter, true);
                        resetParameters = true;
                    }

                    SetDestination(PlayerPosition);
                    predictTime = State.LostPlayerPredictTime;

                    if (WithinAttackReach())
                    {
                        agent.isStopped = true;
                        agent.velocity = Vector3.zero;
                        machine.FacePlayerWhileManual = true;
                        animator.SetBool(Group.RunParameter, false);
                        animator.SetBool(Group.IdleParameter, true);

                        bool isAttacking = IsAnimation(1, Group.AttackState);
                        if (FacingPlayer() && !isAttacking && !playerHealth.IsDead)
                            animator.SetTrigger(Group.AttackTrigger);
                    }
                    else
                    {
                        machine.FacePlayerWhileManual = false;
                        agent.isStopped = false;
                        animator.SetBool(Group.RunParameter, true);
                        animator.SetBool(Group.IdleParameter, false);
                        animator.ResetTrigger(Group.AttackTrigger);
                    }

                    isPatrolPending = false;
                    isChaseStarted = true;
                    waitTime = 0f;
                }
                else if(predictTime > 0f)
                {
                    machine.FacePlayerWhileManual = false;
                    SetDestination(PlayerPosition);
                    predictTime -= Time.deltaTime;
                }
                else
                {
                    machine.FacePlayerWhileManual = false;
                    if (!PathCompleted())
                        return;

                    if (!isPatrolPending)
                    {
                        Group.ResetAnimatorPrameters(animator);
                        animator.SetBool(Group.PatrolParameter, true);
                        agent.velocity = Vector3.zero;
                        agent.isStopped = true;

                        resetParameters = false;
                        isPatrolPending = true;
                        isChaseStarted = false;
                    }
                    else
                    {
                        waitTime += Time.deltaTime;
                    }
                }
            }

            private bool WithinAttackReach()
            {
                Vector3 self = machine.transform.position;
                Vector3 player = PlayerPosition;
                self.y = 0f;
                player.y = 0f;
                return Vector3.Distance(self, player) <= State.AttackDistance;
            }

            private bool FacingPlayer()
            {
                return IsObjectInSights(Mathf.Max(State.AttackFOV, 70f), PlayerPosition);
            }

            private bool PlayerInSights()
            {
                if (playerHealth.IsDead)
                    return false;

                if (!isChaseStarted || isPatrolPending)
                    return SeesPlayerOrClose(State.VeryClosePlayerDetection);

                return SeesObject(machine.SightsDistance, PlayerHead);
            }

            private void AttackPlayer()
            {
                if (!WithinAttackReach())
                    return;

                int damage = Group.DamageRange.Random();
                playerHealth.ApplyDamage(damage, machine.transform);
            }
        }
    }
}