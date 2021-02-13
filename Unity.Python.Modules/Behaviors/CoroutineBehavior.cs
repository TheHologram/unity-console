using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace Unity.Python.Modules
{
    class CoroutineBehavior : MonoBehaviour
    {
        private static object _lock = new object();
        private readonly ManualResetEvent completeEvent = new ManualResetEvent(false);
        protected bool start;
        protected object result;
        protected CoroutineStart func;

        public static object Run(CoroutineStart func)
        {
            // If already in unity coroutine then just execute python
            if (!Monitor.TryEnter(_lock))
            {
                //Console.WriteLine("CoroutineBehavior [{0}]: Recursion Skip", Thread.CurrentThread.ManagedThreadId);
                return func();
            }

            try
            {
                //Console.WriteLine("CoroutineBehavior [{0}]: Run", Thread.CurrentThread.ManagedThreadId);
                var obj2 = new GameObject(Guid.NewGuid().ToString());
                UnityEngine.Object.DontDestroyOnLoad(obj2);
                var ex = obj2.AddComponent<CoroutineBehavior>();
                if (ex != null)
                {
                    ex.func = func;
                    ex.start = true;
                    ex.WaitFor();
                    return ex.result;
                }
                else
                {
                    return func();
                }
            }
            finally
            {
                Monitor.Exit(_lock);
                //Console.WriteLine("CoroutineBehavior [{0}]: Done", Thread.CurrentThread.ManagedThreadId);
            }
        }

        // ReSharper disable once UnusedMember.Local
        protected virtual void Update()
        {
            if (!start) return;
            start = false;
            StartCoroutine(StartCoroutineProc());
        }

        protected object UpdateStep(int step)
        {
            //Console.WriteLine("-> Coroutine [{0}]: Update Step", Thread.CurrentThread.ManagedThreadId);
            result = func();
            //Console.WriteLine("<- Coroutine [{0}]: Update Step", Thread.CurrentThread.ManagedThreadId);
            return null;
        }

        [DebuggerHidden]
        private IEnumerator StartCoroutineProc()
        {
            return new CoroutineProcIter(this);
        }

        protected virtual void Start()
        {
            completeEvent.Reset();
        }

        public void Complete()
        {
            completeEvent.Set();
            OnComplete();
            Destroy(this.gameObject);
        }

        public virtual void OnComplete() { }

        public bool WaitFor(TimeSpan time)
        {
            return completeEvent.WaitOne(time, false);
        }

        public bool WaitFor()
        {
            return completeEvent.WaitOne(-1, false);
        }

        internal class CoroutineProcIter : IEnumerator<object>
        {
            private readonly CoroutineBehavior owner;
            private object current;
            private int step;

            internal CoroutineProcIter(CoroutineBehavior owner)
            {
                this.owner = owner;
                step = 0;
                try
                {
                    owner.Start();
                }
                catch (Exception ex)
                {
                    step = -1;
                    System.Console.WriteLine("Exception: " + ex.Message);
                    Complete();
                }
            }

            void Complete()
            {
                try
                {
                    owner.Complete();
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine("Exception: " + ex.Message);
                }
            }

            [DebuggerHidden]
            public void Dispose()
            {
                step = -1;
            }

            public bool MoveNext()
            {
                var num = step;
                step = -1;
                if (num < 0)
                    return false;
                if (num == 0)
                {
                    //oldTime = Time.timeScale;
                    //Time.timeScale = 0;
                    current = new WaitForEndOfFrame();
                    step = num + 1;
                }
                else
                {
                    step = num + 1;
                    try
                    {
                        current = owner.UpdateStep(num);
                        if (current == null)
                            step = -1;
                    }
                    catch (Exception ex)
                    {
                        current = null;
                        step = -1;
                        System.Console.WriteLine("Exception: " + ex.Message);
                    }
                }
                if (step <= 0)
                {
                    //Time.timeScale = oldTime;
                    Complete();
                }
                return step >= 0;
            }

            [DebuggerHidden]
            public void Reset()
            {
                throw new NotSupportedException();
            }

            object IEnumerator.Current
            {
                [DebuggerHidden]
                get { return current; }
            }

            object IEnumerator<object>.Current
            {
                [DebuggerHidden]
                get { return current; }
            }
        }
    }
}
