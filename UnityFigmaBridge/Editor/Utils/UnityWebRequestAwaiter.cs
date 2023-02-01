using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityFigmaBridge.Editor.Utils
{
	
    /// <summary>
    /// Extension to UnityWebRequest to allow it to operate with async/await
    /// This makes it much easier to work with Editor scripts
    /// From here: https://gist.github.com/krzys-h/9062552e33dd7bd7fe4a6c12db109a1a
    /// </summary>
    public class UnityWebRequestAwaiter : INotifyCompletion
    {
        private UnityWebRequestAsyncOperation asyncOp;
        private Action continuation;

        public UnityWebRequestAwaiter(UnityWebRequestAsyncOperation asyncOp)
        {
            this.asyncOp = asyncOp;
            asyncOp.completed += OnRequestCompleted;
        }

        public bool IsCompleted
        {
            get { return asyncOp.isDone; }
        }

        public void GetResult()
        {
        }

        public void OnCompleted(Action continuation)
        {
            this.continuation = continuation;
        }

        private void OnRequestCompleted(AsyncOperation obj)
        {
            continuation();
        }
    }

    public static class ExtensionMethods
    {
        public static UnityWebRequestAwaiter GetAwaiter(this UnityWebRequestAsyncOperation asyncOp)
        {
            return new UnityWebRequestAwaiter(asyncOp);
        }
    }

}