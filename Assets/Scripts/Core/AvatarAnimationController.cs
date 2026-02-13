using UnityEngine;

namespace LanguageTutor.Core
{
    /// <summary>
    /// Controls avatar animations based on conversation pipeline stages.
    /// Manages transitions between Idle, Thinking, and Talking animations.
    /// </summary>
    public class AvatarAnimationController : MonoBehaviour
    {
        [Header("Animation Components")]
        [SerializeField] private Animator animator;

        [Header("Animation Trigger Names")]
        [SerializeField] private string idleTrigger = "Idle";
        [SerializeField] private string thinkingTrigger = "Thinking";
        [SerializeField] private string talkingTrigger = "Talking";
        [SerializeField] private string greetingTrigger = "Greeting";

        [Header("Animation State Names (Optional)")]
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string thinkingStateName = "Thinking";
        [SerializeField] private string talkingStateName = "Talking";

        [Header("Idle Scratch")]
        [SerializeField] private bool enableIdleScratch = true;
        [SerializeField] private string scratchTrigger = "Scratch";
        [SerializeField] private string scratchStateName = "IdleScratch";
        [SerializeField] private float scratchIntervalMinSeconds = 12f;
        [SerializeField] private float scratchIntervalMaxSeconds = 18f;

        private AnimationState _currentState = AnimationState.Idle;
        private bool _wasIdle;
        private float _nextScratchTime;
        private int _idleStateHash;
        private int _scratchStateHash;

        private void Awake()
        {
            // Try to get animator if not assigned
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (animator == null)
            {
                Debug.LogError("[AvatarAnimationController] No Animator component found! Please assign an Animator.");
            }
            else
            {
                // CRITICAL FIX: Disable Root Motion to prevent avatar from snapping to floor/origin
                // when forcing transitions. We want the avatar to stay in its placed position.
                animator.applyRootMotion = false;
            }

            _idleStateHash = Animator.StringToHash(idleStateName);
            _scratchStateHash = Animator.StringToHash(scratchStateName);
            ScheduleNextScratch();
        }

        private void Update()
        {
            if (!enableIdleScratch || animator == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(scratchTrigger))
            {
                return;
            }

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            bool isIdle = stateInfo.shortNameHash == _idleStateHash || stateInfo.IsName(idleStateName);
            bool isScratch = !string.IsNullOrWhiteSpace(scratchStateName)
                && (stateInfo.shortNameHash == _scratchStateHash || stateInfo.IsName(scratchStateName));

            if (!isIdle || isScratch || animator.IsInTransition(0))
            {
                _wasIdle = false;
                return;
            }

            if (!_wasIdle)
            {
                _wasIdle = true;
                ScheduleNextScratch();
                return;
            }

            if (Time.time >= _nextScratchTime)
            {
                animator.SetTrigger(scratchTrigger);
                ScheduleNextScratch();
            }
        }

        /// <summary>
        /// Set avatar to idle animation.
        /// </summary>
        public void SetIdle()
        {
            if (_currentState == AnimationState.Idle) return;

            if (animator != null)
            {
                // Force IMMEDIATE snap to Idle using Play() instead of CrossFade or SetTrigger.
                // This prevents "blending to floor" issues and ignores "Has Exit Time".
                animator.Play(idleStateName); 
                
                _currentState = AnimationState.Idle;
                ScheduleNextScratch();
                Debug.Log("[AvatarAnimationController] Animation force-set to: Idle");
            }
        }

        /// <summary>
        /// Set avatar to thinking animation.
        /// Used during STT, LLM processing, and TTS generation.
        /// </summary>
        public void SetThinking()
        {
            if (_currentState == AnimationState.Thinking) return;

            if (animator != null)
            {
                // WORKAROUND: The "Thinking" animation clip is a kneeling/sitting pose which looks buggy.
                // We use "Idle" (Standing) instead so the avatar stays upright while generating voice.
                animator.Play(idleStateName); 
                
                _currentState = AnimationState.Thinking;
                Debug.Log("[AvatarAnimationController] Animation force-set to: Thinking (Visual: Idle)");
            }
        }

        /// <summary>
        /// Set avatar to talking animation.
        /// Used when TTS audio is playing.
        /// </summary>
        public void SetTalking()
        {
            if (_currentState == AnimationState.Talking) return;

            if (animator != null)
            {
                // Force IMMEDIATE transition to Talking
                animator.Play(talkingStateName); 
                
                _currentState = AnimationState.Talking;
                Debug.Log("[AvatarAnimationController] Animation force-set to: Talking");
            }
        }

        /// <summary>
        /// Play greeting animation.
        /// Used when the character starts up or greets the user.
        /// </summary>
        public void PlayGreeting()
        {
            if (animator != null)
            {
                animator.SetTrigger(greetingTrigger);
                Debug.Log("[AvatarAnimationController] Playing greeting animation");
            }
        }

        /// <summary>
        /// Get the current animation state.
        /// </summary>
        public AnimationState GetCurrentState()
        {
            return _currentState;
        }

        /// <summary>
        /// Check if animator is in a specific state.
        /// </summary>
        public bool IsInState(string stateName)
        {
            if (animator == null) return false;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.IsName(stateName);
        }

        /// <summary>
        /// Force immediate transition to a state (useful for debugging).
        /// </summary>
        public void ForceState(AnimationState state)
        {
            switch (state)
            {
                case AnimationState.Idle:
                    SetIdle();
                    break;
                case AnimationState.Thinking:
                    SetThinking();
                    break;
                case AnimationState.Talking:
                    SetTalking();
                    break;
            }
        }

        /// <summary>
        /// Reset animation controller to idle state.
        /// </summary>
        public void Reset()
        {
            _currentState = AnimationState.Idle;
            if (animator != null)
            {
                animator.SetTrigger(idleTrigger);
                ScheduleNextScratch();
            }
        }

        private void ScheduleNextScratch()
        {
            float minInterval = Mathf.Max(0.1f, scratchIntervalMinSeconds);
            float maxInterval = Mathf.Max(minInterval, scratchIntervalMaxSeconds);
            _nextScratchTime = Time.time + Random.Range(minInterval, maxInterval);
        }

        #region Editor Helpers
#if UNITY_EDITOR
        [ContextMenu("Test Idle Animation")]
        private void TestIdle()
        {
            SetIdle();
        }

        [ContextMenu("Test Thinking Animation")]
        private void TestThinking()
        {
            SetThinking();
        }

        [ContextMenu("Test Talking Animation")]
        private void TestTalking()
        {
            SetTalking();
        }
#endif
        #endregion
    }

    /// <summary>
    /// Avatar animation states.
    /// </summary>
    public enum AnimationState
    {
        Idle,
        Thinking,
        Talking
    }
}
