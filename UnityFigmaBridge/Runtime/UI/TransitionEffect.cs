using System;
using UnityEngine;

namespace UnityFigmaBridge.Runtime.UI
{
    /// <summary>
    /// Interface for a transition effect with callback for end/show
    /// </summary>
    public abstract class TransitionEffect : MonoBehaviour
    {
        public abstract void AnimateOut(Action completeDelegate=null);
        public abstract void AnimateIn(Action completeDelegate=null);
        public abstract void Hide();
    }
}