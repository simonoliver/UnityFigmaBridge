using System;
using UnityEngine;

namespace UnityFigmaBridge.Runtime.UI
{
    /// <summary>
    /// Control full flowScreen transitions that are driven by Animation timelines.
    /// Assumes a simple animator setup with two triggers - AnimateOut and AnimateIn, and callback functions
    /// </summary>
    public class TransitionEffectAnimationDriven : TransitionEffect
    {
        [SerializeField] private Animator m_TransitionEffectAnimator;
        
        private Action m_OnAnimateOutComplete;
        private Action m_OnAnimateInComplete;
        private static readonly int Out = Animator.StringToHash("TransitionOut");
        private static readonly int In = Animator.StringToHash("TransitionIn");
        private static readonly int Hidden = Animator.StringToHash("Hidden");
        
        public override void AnimateOut(Action completeDelegate)
        {
            gameObject.SetActive(true);
            m_OnAnimateOutComplete = completeDelegate;
            m_TransitionEffectAnimator.SetTrigger(Out);
        }

        public override void AnimateIn(Action completeDelegate)
        {
            gameObject.SetActive(true);
            m_OnAnimateInComplete = completeDelegate;
            m_TransitionEffectAnimator.SetTrigger(In);
        }

        public override void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Called by animation timeline (Animation Event)
        /// </summary>
        public void TransitionOutComplete()
        {
            m_OnAnimateOutComplete?.Invoke();
        }
        
        /// <summary>
        /// Called by animation timeline (Animation Event)
        /// </summary>
        public void TransitionInComplete()
        {
            gameObject.SetActive(false);
            m_OnAnimateInComplete?.Invoke();
        }

        public void SetHidden()
        {
            m_TransitionEffectAnimator.SetTrigger(Hidden);
        }
    }
}