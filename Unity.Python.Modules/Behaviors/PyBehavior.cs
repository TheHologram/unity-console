using System;
using System.Dynamic;
using System.Runtime.CompilerServices;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using Microsoft.Scripting.Utils;
using UnityEngine;
using ClassMemberCall =
    System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, object, object>>;

namespace Unity.Python.Modules.Behaviors
{
    internal class PyBehavior : MonoBehaviour
    {
        internal static object Create(CodeContext context, object function, PythonTuple args, object kwargs)
        {
            var obj = new GameObject(Guid.NewGuid().ToString());
            try
            {
                return Add(context, obj, function, args, kwargs);
            }
            catch
            {
                DestroyObject(obj);
            }
            return null;
        }

        internal static object Add(CodeContext context, GameObject parent, object function, PythonTuple args, object kwargs)
        {
            try
            {
#pragma warning disable 618 // TODO: obsolete
                object result = null;
                if (kwargs != null)
                    result = PythonOps.CallWithArgsTupleAndKeywordDictAndContext(context, function,
                        ArrayUtils.EmptyObjects, ArrayUtils.EmptyStrings, args, kwargs);
                else
                    result = PythonOps.CallWithArgsTuple(function, ArrayUtils.EmptyObjects, args);
#pragma warning restore 618
                if (result != null)
                {
                    var ex = parent.AddComponent<PyBehavior>();
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


        private ClassMemberCall awakeCB, onEnableCB, onDisableCB, startCB, updateCB, lateUpdateCB, onDestroyCB, onPostRenderCB, onMouseEnterCB, onMouseExitCB, onMouseOverCB;

        private void SetInner(CodeContext context, object inner)
        {
            //var parameter = Expression.Parameter(typeof(object), "");
            //var innerDMO = DynamicUtils.ObjectToMetaObject(inner, parameter);

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
                    case "lateupdate":
                        lateUpdateCB = ClassMemberCall.Create(
                            context.LanguageContext.CreateCallBinder(memberName, false, new CallInfo(0)));
                        break;

                    case "onpostrender":
                        onPostRenderCB = ClassMemberCall.Create(
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
                        onMouseOverCB = ClassMemberCall.Create(context.LanguageContext.CreateCallBinder(memberName, false, new CallInfo(0)));
                        break;

                    case "_gameobject":
                    case "gameobject":
                        
                        break;

                    case "_component":
                    case "component":
                        
                        break;

                }
            PythonOps.SetAttr(context, inner, "component", this.gameObject);
            PythonOps.SetAttr(context, inner, "gameObject", this);
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
        ///     LateUpdate is called once per frame, after Update has finished. Any calculations that are performed in Update will have completed when LateUpdate begins. A common use for LateUpdate would be a following third-person camera. If you make your character move and turn inside Update, you can perform all camera movement and rotation calculations in LateUpdate. This will ensure that the character has moved completely before the camera tracks its position.
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private void LateUpdate()
        {
            lateUpdateCB?.Target(lateUpdateCB, Inner);
        }
        

        /// <summary>
        ///     This function is called after all frame updates for the last frame of the object’s existence (the object might be destroyed in response to Object.Destroy or at the closure of a scene).
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private void OnDestroy()
        {
            onDestroyCB?.Target(onDestroyCB, Inner);
        }

        /// <summary>
        ///     Called after a camera finishes rendering the scene.
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private void OnPostRender()
        {
            onPostRenderCB?.Target(onPostRenderCB, Inner);
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

        public override string ToString()
        {
            return "<Gui Behavior>";
        }

    }
}