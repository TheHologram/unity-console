/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Apache License, Version 2.0, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

/* ****************************************************************************
 *  Notes:  This code was copied and altered from thread.cs in IronPython.Modules
 *    to support coroutines in Unity
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;
using SpecialName = System.Runtime.CompilerServices.SpecialNameAttribute;

[assembly: PythonModule("coroutine", typeof(Unity.Python.Modules.PythonCoroutine))]
namespace Unity.Python.Modules
{
    [ComVisible(true)]
    public delegate object CoroutineStart();

    internal class Coroutine
    {
        private readonly CoroutineStart start;

        public Coroutine(CoroutineStart start, int size)
        {
            this.start = start;
        }

        public Coroutine(CoroutineStart start)
        {
            this.start = start;
        }

        public object Run()
        {
            return CoroutineBehavior.Run(this.start);
        }

        public object Result { get; set; }
        public static Coroutine CurrentCoroutine { get; set; }
    }

    public static class PythonCoroutine
    {
        public const string __doc__ = "Provides low level primitives for coroutine execution.";

        private static readonly object _stackSizeKey = new object();
        private static object _coroutineCountKey = new object();

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict)
        {
            context.SetModuleState(_stackSizeKey, 0);
            context.EnsureModuleException("coroutineerror", dict, "error", "coroutine");
        }

        #region Public API Surface

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly PythonType LockType = DynamicHelpers.GetPythonTypeFromType(typeof(@lock));

        [Documentation("start_new_coroutine(function, [args, [kwDict]]) -> coroutine id\nCreates a new coroutine running the given function")]
        public static object start_new_coroutine(CodeContext/*!*/ context, object function, object args, object kwDict)
        {
            PythonTuple tupArgs = args as PythonTuple;
            if (tupArgs == null) throw PythonOps.TypeError("2nd arg must be a tuple");

            Coroutine t = CreateCoroutine(context, new CoroutineObj(context, function, tupArgs, kwDict).Start);
            return t.Run();
        }

        [Documentation("start_new_coroutine(function, args, [kwDict]) -> coroutine id\nCreates a new coroutine running the given function")]
        public static object start_new_coroutine(CodeContext/*!*/ context, object function, object args)
        {
            PythonTuple tupArgs = args as PythonTuple;
            if (tupArgs == null) throw PythonOps.TypeError("2nd arg must be a tuple");

            Coroutine t = CreateCoroutine(context, new CoroutineObj(context, function, tupArgs, null).Start);
            return t.Run();
        }

        public static void exit()
        {
            PythonOps.SystemExit();
        }

        [Documentation("allocate_lock() -> lock object\nAllocates a new lock object that can be used for synchronization")]
        public static object allocate_lock()
        {
            return new @lock();
        }

        public static object get_ident()
        {
            return Coroutine.CurrentCoroutine.Result;
        }

        public static int stack_size(CodeContext/*!*/ context)
        {
            return GetStackSize(context);
        }

        public static int stack_size(CodeContext/*!*/ context, int size)
        {
            if (size < 32 * 1024 && size != 0)
            {
                throw PythonOps.ValueError("size too small: {0}", size);
            }

            int oldSize = GetStackSize(context);

            SetStackSize(context, size);

            return oldSize;
        }

        // deprecated synonyms, wrappers over preferred names...
        [Documentation("start_new(function, [args, [kwDict]]) -> coroutine id\nCreates a new coroutine running the given function")]
        public static object start_new(CodeContext context, object function, object args)
        {
            return start_new_coroutine(context, function, args);
        }

        public static void exit_coroutine()
        {
            exit();
        }

        public static object allocate()
        {
            return allocate_lock();
        }

        public static int _count(CodeContext context)
        {
            return (int)context.LanguageContext.GetOrCreateModuleState<object>(_coroutineCountKey, () => 0);
        }

        #endregion

        [PythonType, PythonHidden]
        public class @lock
        {
            private AutoResetEvent blockEvent;
            private Coroutine curHolder;

            public object __enter__()
            {
                acquire();
                return this;
            }

            public void __exit__(CodeContext/*!*/ context, params object[] args)
            {
                release(context);
            }

            public object acquire()
            {
                return (acquire(ScriptingRuntimeHelpers.True));
            }

            public object acquire(object waitflag)
            {
                bool fWait = PythonOps.IsTrue(waitflag);
                for (;;)
                {
                    if (Interlocked.CompareExchange<Coroutine>(ref curHolder, Coroutine.CurrentCoroutine, null) == null)
                    {
                        return ScriptingRuntimeHelpers.True;
                    }
                    if (!fWait)
                    {
                        return ScriptingRuntimeHelpers.False;
                    }
                    if (blockEvent == null)
                    {
                        // try again in case someone released us, checked the block
                        // event and discovered it was null so they didn't set it.
                        CreateBlockEvent();
                        continue;
                    }
                    blockEvent.WaitOne();
                    GC.KeepAlive(this);
                }
            }

            public void release(CodeContext/*!*/ context, params object[] param)
            {
                release(context);
            }

            public void release(CodeContext/*!*/ context)
            {
                if (Interlocked.Exchange<Coroutine>(ref curHolder, null) == null)
                {
                    throw PythonExceptions.CreateThrowable((PythonType)PythonContext.GetContext(context).GetModuleState("coroutineerror"), "lock isn't held", null);
                }
                if (blockEvent != null)
                {
                    // if this isn't set yet we race, it's handled in Acquire()
                    blockEvent.Set();
                    GC.KeepAlive(this);
                }
            }

            public bool locked()
            {
                return curHolder != null;
            }

            private void CreateBlockEvent()
            {
                AutoResetEvent are = new AutoResetEvent(false);
                if (Interlocked.CompareExchange<AutoResetEvent>(ref blockEvent, are, null) != null)
                {
                    ((IDisposable)are).Dispose();
                }
            }
        }

        #region Internal Implementation details

        private static Coroutine CreateCoroutine(CodeContext/*!*/ context, CoroutineStart start)
        {
            int size = GetStackSize(context);
            return (size != 0) ? new Coroutine(start, size) : new Coroutine(start);
        }

        private class CoroutineObj
        {
            private readonly object _func, _kwargs;
            private readonly PythonTuple _args;
            private readonly CodeContext _context;

            public CoroutineObj(CodeContext context, object function, PythonTuple args, object kwargs)
            {
                Debug.Assert(args != null);
                _func = function;
                _kwargs = kwargs;
                _args = args;
                _context = context;
            }

            public object Start()
            {
                object result = null;
                lock (_coroutineCountKey)
                {
                    int startCount = (int)_context.LanguageContext.GetOrCreateModuleState<object>(_coroutineCountKey, () => 0);
                    _context.LanguageContext.SetModuleState(_coroutineCountKey, startCount + 1);
                }
                try
                {
#pragma warning disable 618 // TODO: obsolete
                    if (_kwargs != null)
                    {
                        result = PythonOps.CallWithArgsTupleAndKeywordDictAndContext(_context, _func, ArrayUtils.EmptyObjects, ArrayUtils.EmptyStrings, _args, _kwargs);
                    }
                    else
                    {
                        result = PythonOps.CallWithArgsTuple(_func, ArrayUtils.EmptyObjects, _args);
                    }
#pragma warning restore 618
                }
                catch (SystemExitException)
                {
                    // ignore and quit
                }
                catch (Exception e)
                {
                    PythonOps.PrintWithDest(_context, PythonContext.GetContext(_context).SystemStandardError, "Unhandled exception on coroutine");
                    string exstr = _context.LanguageContext.FormatException(e);
                    PythonOps.PrintWithDest(_context, PythonContext.GetContext(_context).SystemStandardError, exstr);
                }
                finally
                {
                    lock (_coroutineCountKey)
                    {
                        int curCount = (int)_context.LanguageContext.GetModuleState(_coroutineCountKey);
                        _context.LanguageContext.SetModuleState(_coroutineCountKey, curCount - 1);
                    }
                }
                return result;
            }
        }

        #endregion

        private static int GetStackSize(CodeContext/*!*/ context)
        {
            return (int)PythonContext.GetContext(context).GetModuleState(_stackSizeKey);
        }

        private static void SetStackSize(CodeContext/*!*/ context, int stackSize)
        {
            PythonContext.GetContext(context).SetModuleState(_stackSizeKey, stackSize);
        }

        [PythonType]
        public class _local
        {
            private readonly PythonDictionary/*!*/ _dict = new PythonDictionary(new CoroutineLocalDictionaryStorage());

            #region Custom Attribute Access

            [SpecialName]
            public object GetCustomMember(string name)
            {
                return _dict.get(name, OperationFailed.Value);
            }

            [SpecialName]
            public void SetMemberAfter(string name, object value)
            {
                _dict[name] = value;
            }

            [SpecialName]
            public void DeleteMember(string name)
            {
                _dict.__delitem__(name);
            }

            #endregion

            public PythonDictionary/*!*/ __dict__
            {
                get
                {
                    return _dict;
                }
            }

            #region Dictionary Storage

            /// <summary>
            /// Provides a dictionary storage implementation whose storage is local to
            /// the coroutine.
            /// </summary>
            private class CoroutineLocalDictionaryStorage : IronPython.Runtime.DictionaryStorage
            {
                private readonly Microsoft.Scripting.Utils.ThreadLocal<IronPython.Runtime.CommonDictionaryStorage> _storage = new Microsoft.Scripting.Utils.ThreadLocal<IronPython.Runtime.CommonDictionaryStorage>();

                public override void Add(ref IronPython.Runtime.DictionaryStorage storage, object key, object value)
                {
                    GetStorage().Add(key, value);
                }

                public override bool Contains(object key)
                {
                    return GetStorage().Contains(key);
                }

                public override bool Remove(ref IronPython.Runtime.DictionaryStorage storage, object key)
                {
                    return GetStorage().Remove(ref storage, key);
                }

                public override bool TryGetValue(object key, out object value)
                {
                    return GetStorage().TryGetValue(key, out value);
                }

                public override int Count
                {
                    get { return GetStorage().Count; }
                }

                public override void Clear(ref IronPython.Runtime.DictionaryStorage storage)
                {
                    GetStorage().Clear(ref storage);
                }

                public override List<KeyValuePair<object, object>>/*!*/ GetItems()
                {
                    return GetStorage().GetItems();
                }

                private IronPython.Runtime.CommonDictionaryStorage/*!*/ GetStorage()
                {
                    return _storage.GetOrCreate(() => new IronPython.Runtime.CommonDictionaryStorage());
                }
            }

            #endregion
        }
    }
}
