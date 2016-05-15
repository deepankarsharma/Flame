﻿using Flame.Compiler;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flame.Cecil.Emit
{
    public class NewObjectBlock : ICecilBlock
    {
        public NewObjectBlock(
            ICodeGenerator CodeGenerator, IMethod Constructor, 
            IEnumerable<ICecilBlock> Arguments)
        {
            this.CodeGenerator = CodeGenerator;
            this.Constructor = Constructor;
            this.Arguments = Arguments;
        }

        public ICodeGenerator CodeGenerator { get; private set; }
        public IMethod Constructor { get; private set; }
        public IEnumerable<ICecilBlock> Arguments { get; private set; }

        public void Emit(IEmitContext Context)
        {
            ILCodeGenerator.EmitArguments(Arguments, Constructor, Context);
            Context.Emit(OpCodes.Newobj, Constructor);
            Context.Stack.Push(Constructor.DeclaringType);
        }

        public IType BlockType
        {
            get
            {
                return Constructor.DeclaringType;
            }
        }
    }

    public class InvocationBlock : ICecilBlock
    {
        public InvocationBlock(ICecilBlock Method, IEnumerable<ICecilBlock> Arguments)
        {
            this.Method = Method;
            this.Arguments = Arguments;
        }

        public ICecilBlock Method { get; private set; }
        public IEnumerable<ICecilBlock> Arguments { get; private set; }

        public void Emit(IEmitContext Context)
        {
            if (Method is MethodBlock)
            {
                var log = CodeGenerator.Method.GetLog();
                var mBlock = (MethodBlock)Method;
                var method = mBlock.Method;
                IType callerType = null;
                if (!method.IsStatic)
                {
                    if (mBlock.Caller == null)
                    {
                        System.Diagnostics.Debugger.Break();
                    }
                    MethodBlock.EmitCaller(mBlock.Caller, method, Context);
                    callerType = Context.Stack.Pop();
                    if (!ILCodeGenerator.IsExpectedCallingType(method, callerType))
                    {
                        log.LogError(new LogEntry("IL emit error", "invalid calling type on stack. Expected '" + ILCodeGenerator.GetExpectedCallingType(method).FullName + "', got '" + callerType.FullName + "'"));
                    }
                }
                ILCodeGenerator.EmitArguments(Arguments, method, Context);
                if ((method.DeclaringType.GetIsArray() || method.DeclaringType.GetIsVector()) && method is IAccessor 
                    && (((IAccessor)method).DeclaringProperty).Name.ToString() == "Length" 
                    && ((IAccessor)method).AccessorType.Equals(AccessorType.GetAccessor))
                {
                    Context.Emit(OpCodes.Ldlen);
                }
                else if (log.Options.UseInvariantCulture() && ILCodeGenerator.IsCultureSpecific(method))
                    // Fix culture-specific calls if necessary
                {
                    ILCodeGenerator.EmitCultureInvariantCall(Context, method, callerType, mBlock.IsVirtual, CodeGenerator.GetModule());
                }
                else
                {
                    ILCodeGenerator.EmitCall(Context, method, callerType, mBlock.IsVirtual);
                }

                Context.Stack.PushValue(method.ReturnType);
            }
            else
            {
                // Call delegate
                Method.Emit(Context);
                var type = CecilDelegateType.Create(Context.Stack.Pop(), CodeGenerator);
                var invokeMethod = CecilDelegateType.GetInvokeMethod(type);
                ILCodeGenerator.EmitArguments(Arguments, invokeMethod, Context);
                Context.Emit(OpCodes.Callvirt, invokeMethod);

                // This emits calli (for function pointers), but Flame.Cecil uses delegates
                /*
                var method = (IMethod)type;
                var cecilMethod = (ICecilMethod)CodeGenerator.Method;
                var methodRef = cecilMethod.GetMethodReference();
                var module = CodeGenerator.GetModule();
                var callSite = new CallSite(method.ReturnType.GetImportedReference(module, methodRef));
                foreach (var item in method.GetParameters())
                {
                    var paramDef = new ParameterDefinition(item.ParameterType.GetImportedReference(module, methodRef));
                    callSite.Parameters.Add(paramDef);
                }
                ILCodeGenerator.EmitArguments(Arguments, method, Context);
                Context.Emit(OpCodes.Calli, callSite); */

                Context.Stack.PushValue(invokeMethod.ReturnType);

            }
        }

        public ICodeGenerator CodeGenerator
        {
            get { return Method.CodeGenerator; }
        }

        public IType BlockType
        {
            get
            {
                if (Method is MethodBlock)
                {
                    var method = (MethodBlock)Method;
                    if (method.Caller == null && method.Method.IsConstructor)
                    {
                        return method.Method.DeclaringType;
                    }
                }
                return CecilDelegateType.GetDelegateMethod(Method.BlockType).ReturnType;
            }
        }
    }
}
