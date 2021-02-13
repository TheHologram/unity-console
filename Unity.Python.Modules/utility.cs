using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using IronPython.Runtime;
using Microsoft.Scripting.Runtime;

[assembly: PythonModule("unity_util", typeof(Unity.Python.Modules.UtilityModule))]
namespace Unity.Python.Modules
{
    public static class UtilityModule
    {
        public const string __doc__ = "Provides low level primitives for unity behaviors";

        [SpecialName]
        public static void PerformModuleReload(PythonContext/*!*/ context, PythonDictionary/*!*/ dict)
        {
            //context.SetModuleState(_stackSizeKey, 0);
            //context.EnsureModuleException("coroutineerror", dict, "error", "coroutine");
        }

        #region GuiBehavior
        [Documentation("create_gui_behavior(class_or_function, args, kwargs) -> object\nAllocates a new GameObject and populates with gui behavior")]
        public static object create_gui_behavior(CodeContext/*!*/ context, object function, object args, object kwDict)
        {
            var tupArgs = args as PythonTuple;
            if (tupArgs == null) throw IronPython.Runtime.Operations.PythonOps.TypeError("2nd arg must be a tuple");

            return Behaviors.GuiBehavior.Create(context, function, tupArgs, kwDict);
        }

        [Documentation("create_gui_behavior(class_or_function, args) -> object\nAllocates a new GameObject and populates with gui behavior")]
        public static object create_gui_behavior(CodeContext/*!*/ context, object function, object args)
        {
            var tupArgs = args as PythonTuple;
            if (tupArgs == null) throw IronPython.Runtime.Operations.PythonOps.TypeError("2nd arg must be a tuple");

            return Behaviors.GuiBehavior.Create(context, function, tupArgs, null);
        }

        [Documentation("create_gui_behavior(class_or_function) -> object\nAllocates a new GameObject and populates with gui behavior")]
        public static object create_gui_behavior(CodeContext/*!*/ context, object function)
        {
            return Behaviors.GuiBehavior.Create(context, function, PythonTuple.EMPTY, null);
        }

        [Documentation("add_gui_behavior(class_or_function, args, kwargs) -> object\nAdd gui behavior to existing GameObject")]
        public static object add_gui_behavior(CodeContext/*!*/ context, UnityEngine.GameObject parent, object function, object args, object kwDict)
        {
            var tupArgs = args as PythonTuple;
            if (tupArgs == null) throw IronPython.Runtime.Operations.PythonOps.TypeError("2nd arg must be a tuple");

            return Behaviors.GuiBehavior.Add(context, parent, function, tupArgs, kwDict);
        }

        [Documentation("add_gui_behavior(class_or_function, args) -> object\nAdd gui behavior to existing GameObject")]
        public static object add_gui_behavior(CodeContext/*!*/ context, UnityEngine.GameObject parent, object function, object args)
        {
            var tupArgs = args as PythonTuple;
            if (tupArgs == null) throw IronPython.Runtime.Operations.PythonOps.TypeError("2nd arg must be a tuple");

            return Behaviors.GuiBehavior.Add(context, parent, function, tupArgs, null);
        }

        [Documentation("add_gui_behavior(class_or_function) -> object\nAdd gui behavior to existing GameObject")]
        public static object add_gui_behavior(CodeContext/*!*/ context, UnityEngine.GameObject parent, object function)
        {
            return Behaviors.GuiBehavior.Add(context, parent, function, PythonTuple.EMPTY, null);
        }
        #endregion

        #region PyBehavior
        [Documentation("create_behavior(class_or_function, args, kwargs) -> object\nAllocates a new GameObject and populates with gui behavior")]
        public static object create_behavior(CodeContext/*!*/ context, object function, object args, object kwDict)
        {
            var tupArgs = args as PythonTuple;
            if (tupArgs == null) throw IronPython.Runtime.Operations.PythonOps.TypeError("2nd arg must be a tuple");

            return Behaviors.PyBehavior.Create(context, function, tupArgs, kwDict);
        }

        [Documentation("create_behavior(class_or_function, args) -> object\nAllocates a new GameObject and populates with gui behavior")]
        public static object create_behavior(CodeContext/*!*/ context, object function, object args)
        {
            var tupArgs = args as PythonTuple;
            if (tupArgs == null) throw IronPython.Runtime.Operations.PythonOps.TypeError("2nd arg must be a tuple");

            return Behaviors.PyBehavior.Create(context, function, tupArgs, null);
        }

        [Documentation("create_behavior(class_or_function) -> object\nAllocates a new GameObject and populates with gui behavior")]
        public static object create_behavior(CodeContext/*!*/ context, object function)
        {
            return Behaviors.PyBehavior.Create(context, function, PythonTuple.EMPTY, null);
        }

        [Documentation("add_behavior(class_or_function, args, kwargs) -> object\nAdd gui behavior to existing GameObject")]
        public static object add_behavior(CodeContext/*!*/ context, UnityEngine.GameObject parent, object function, object args, object kwDict)
        {
            var tupArgs = args as PythonTuple;
            if (tupArgs == null) throw IronPython.Runtime.Operations.PythonOps.TypeError("2nd arg must be a tuple");

            return Behaviors.PyBehavior.Add(context, parent, function, tupArgs, kwDict);
        }

        [Documentation("add_behavior(class_or_function, args) -> object\nAdd gui behavior to existing GameObject")]
        public static object add_behavior(CodeContext/*!*/ context, UnityEngine.GameObject parent, object function, object args)
        {
            var tupArgs = args as PythonTuple;
            if (tupArgs == null) throw IronPython.Runtime.Operations.PythonOps.TypeError("2nd arg must be a tuple");

            return Behaviors.PyBehavior.Add(context, parent, function, tupArgs, null);
        }

        [Documentation("add_behavior(class_or_function) -> object\nAdd gui behavior to existing GameObject")]
        public static object add_behavior(CodeContext/*!*/ context, UnityEngine.GameObject parent, object function)
        {
            return Behaviors.PyBehavior.Add(context, parent, function, PythonTuple.EMPTY, null);
        }
        #endregion

        [Documentation("clean_behaviors() -> object\nClean gameobjects running behaviors")]
        public static void clean_behaviors(CodeContext /*!*/ context)
        {
            foreach (var behavior in UnityEngine.Object.FindObjectsOfType<Behaviors.GuiBehavior>())
            {
                if (behavior.gameObject != null)
                    UnityEngine.Object.Destroy(behavior.gameObject);
            }
            foreach (var behavior in UnityEngine.Object.FindObjectsOfType<Behaviors.PyBehavior>())
            {
                if (behavior.gameObject != null)
                    UnityEngine.Object.Destroy(behavior.gameObject);
            }
        }


        [Documentation("metakey_state() -> (ctrl, alt, shift)\nGet keydown state of ctrl, alt and shift keys")]
        public static PythonTuple metakey_state(CodeContext context)
        {
            bool ControlDown = GetAsyncKeyState(0xA2) != 0 || GetAsyncKeyState(0xA3) != 0;
            bool AltDown = GetAsyncKeyState(0xA4) != 0 || GetAsyncKeyState(0xA5) != 0;
            bool ShiftDown = GetAsyncKeyState(0xA0) != 0 || GetAsyncKeyState(0xA1) != 0;
            return PythonTuple.MakeTuple(ControlDown, AltDown, ShiftDown);
        }

        [Documentation("asynckey_state(vkey) -> state\nGet keydown state of specific key")]
        public static bool asynckey_state(CodeContext context, int keystate)
        {
            return GetAsyncKeyState(keystate) != 0;
        }

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

    }
}
