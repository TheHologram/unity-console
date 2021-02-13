using System;
using System.Dynamic;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Utils;
using UnityEngine;
using ClassMemberCall =
    System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, object, object>>;

namespace Unity.Python.Modules.Behaviors
{
    internal class GuiBehavior : MonoBehaviour
    {
        internal static object Create(CodeContext context, object function, PythonTuple args, object kwargs)
        {
            var obj = new GameObject(Guid.NewGuid().ToString());
            UnityEngine.Object.DontDestroyOnLoad(obj);
            try
            {
                return Add(context, obj, function, args, kwargs);
            }
            catch
            {
                Destroy(obj);
            }
            return null;
        }

        internal static object Add(CodeContext context, GameObject parent, object function, PythonTuple args,
            object kwargs)
        {
            try
            {
#pragma warning disable 618 // TODO: obsolete
                object result;
                if (kwargs != null)
                    result = PythonOps.CallWithArgsTupleAndKeywordDictAndContext(context, function,
                        ArrayUtils.EmptyObjects, ArrayUtils.EmptyStrings, args, kwargs);
                else
                    result = PythonOps.CallWithArgsTuple(function, ArrayUtils.EmptyObjects, args);
#pragma warning restore 618
                if (result != null)
                {
                    var ex = parent.AddComponent<GuiBehavior>();
                    if (ex != null)
                    {
                        ex.SetInner(context, result);
                        return ex;
                        //return PythonTuple.MakeTuple(obj, ex);
                    }
                }
            }
            catch (SystemExitException)
            {
                // ignore and quit
            }
            catch (Exception e)
            {
                PythonOps.PrintWithDest(context, PythonContext.GetContext(context).SystemStandardError,
                    "Unhandled exception on coroutine");
                var exstr = context.LanguageContext.FormatException(e);
                PythonOps.PrintWithDest(context, PythonContext.GetContext(context).SystemStandardError, exstr);
            }
            return null;
        }


        // ReSharper disable InconsistentNaming
        private ClassMemberCall awakeCB, onEnableCB, onDisableCB, startCB, onGUICB, updateCB, onMouseEnterCB, onMouseExitCB, onMouseOverCB, onDestroyCB;

        private void SetInner(CodeContext context, object inner)
        {
            Inner = inner;
            foreach (var memberName in context.LanguageContext.GetMemberNames(inner))
                switch (memberName.ToLower())
                {
                    case "awake":
                        awakeCB = ClassMemberCall.Create(
                            context.LanguageContext.CreateCallBinder(memberName, false, new CallInfo(0)));
                        break;
                    case "onenable":
                        onEnableCB = ClassMemberCall.Create(
                            context.LanguageContext.CreateCallBinder(memberName, false, new CallInfo(0)));
                        break;
                    case "ondisable":
                        onDisableCB = ClassMemberCall.Create(
                            context.LanguageContext.CreateCallBinder(memberName, false, new CallInfo(0)));
                        break;
                    case "ondestroy":
                        onDestroyCB = ClassMemberCall.Create(
                            context.LanguageContext.CreateCallBinder(memberName, false, new CallInfo(0)));
                        break;
                    case "start":
                        startCB = ClassMemberCall.Create(
                            context.LanguageContext.CreateCallBinder(memberName, false, new CallInfo(0)));
                        break;
                    case "update":
                        updateCB = ClassMemberCall.Create(
                            context.LanguageContext.CreateCallBinder(memberName, false, new CallInfo(0)));
                        break;
                    case "ongui":
                        onGUICB = ClassMemberCall.Create(
                            context.LanguageContext.CreateCallBinder(memberName, false, new CallInfo(0)));
                        break;
                    case "onmouseenter":
                        onMouseEnterCB =
                            ClassMemberCall.Create(
                                context.LanguageContext.CreateCallBinder(memberName, false, new CallInfo(0)));
                        break;
                    case "onmouseexit":
                        onMouseExitCB =
                            ClassMemberCall.Create(
                                context.LanguageContext.CreateCallBinder(memberName, false, new CallInfo(0)));
                        break;
                    case "onmouseover":
                        onMouseOverCB =
                            ClassMemberCall.Create(
                                context.LanguageContext.CreateCallBinder(memberName, false, new CallInfo(0)));
                        break;

                    case "_gameobject":
                    case "gameobject":
                        PythonOps.SetAttr(context, inner, memberName, this.gameObject);
                        break;

                    case "_component":
                    case "component":
                        PythonOps.SetAttr(context, inner, memberName, this);
                        break;
                }
        }

        /// <summary>
        ///     The python callsite object class instance
        /// </summary>
        public object Inner { get; private set; }


        /// <summary>
        ///     This function is always called before any Start functions and also just after a prefab is instantiated.
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private void Awake()
        {
            awakeCB?.Target(awakeCB, Inner);
        }

        /// <summary>
        ///     This function is called just after the object is enabled. This happens when a MonoBehaviour instance is created,
        ///     such as when a level is loaded or a GameObject with the script component is instantiated.
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private void OnEnable()
        {
            onEnableCB?.Target(onEnableCB, Inner);
        }
        /// <summary>
        ///     This function is called just after the object is enabled. This happens when a MonoBehaviour instance is created,
        ///     such as when a level is loaded or a GameObject with the script component is instantiated.
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private void OnDisable()
        {
            onDisableCB?.Target(onDisableCB, Inner);
        }


        /// <summary>
        ///     Start is called before the first frame update only if the script instance is enabled
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private void Start()
        {
            startCB?.Target(startCB, Inner);
        }

        /// <summary>
        ///     Update is called once per frame. It is the main workhorse function for frame updates
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private void Update()
        {
            updateCB?.Target(updateCB, Inner);
        }


        /// <summary>
        ///     On Mouse Enter Element
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private void OnMouseEnter()
        {
            onMouseEnterCB?.Target(onMouseEnterCB, Inner);
        }

        /// <summary>
        ///     Update is called once per frame. It is the main workhorse function for frame updates
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private void OnMouseExit()
        {
            onMouseExitCB?.Target(onMouseExitCB, Inner);
        }

        /// <summary>
        ///     Update is called once per frame. It is the main workhorse function for frame updates
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private void OnMouseOver()
        {
            onMouseOverCB?.Target(onMouseOverCB, Inner);
        }

        /// <summary>
        ///     Called multiple times per frame in response to GUI events.
        ///     The Layout and Repaint events are processed first, followed by a Layout and keyboard/mouse event for each input
        ///     event.
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        // ReSharper disable once InconsistentNaming
        private void OnGUI()
        {
            onGUICB?.Target(onGUICB, Inner);
        }

        /// <summary>
        ///     This function is called after all frame updates for the last frame of the object’s existence (the object might be destroyed in response to Object.Destroy or at the closure of a scene).
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private void OnDestroy()
        {
            onDestroyCB?.Target(onDestroyCB, Inner);
        }

        public override string ToString()
        {
            return "<Gui Behavior>";
        }
    }
}