﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace il2cpp
{
	// 指令
	internal class InstInfo
	{
		public OpCode OpCode;
		public object Operand;
		public int Offset;

		public bool IsBrTarget;
		public bool IsProcessed;
	}

	internal class MethodX : GenericArgs
	{
		// 所属类型
		public readonly TypeX DeclType;

		public readonly MethodDef Def;
		// 方法名
		public readonly string DefName;
		// 方法签名
		public readonly MethodSig DefSig;
		// 方法属性
		public readonly MethodAttributes DefAttr;
		// 方法异常处理器
		public IList<ExceptionHandler> DefHandlers;
		// 方法指令列表
		public IList<Instruction> DefInstList;

		// 唯一名称
		private string NameKey;

		// 返回值类型
		public TypeSig ReturnType;
		// 参数类型列表, 包含 this 类型
		public IList<TypeSig> ParamTypes;
		public IList<TypeSig> ParamAfterSentinel;
		// 局部变量类型列表
		public IList<TypeSig> LocalTypes;
		//! 异常处理器列表
		// 指令列表
		public InstInfo[] InstList;

		// 虚方法绑定的实现方法
		public HashSet<MethodX> OverrideImpls;
		public bool HasOverrideImpls => OverrideImpls != null && OverrideImpls.Count > 0;

		public bool HasThis => DefSig.HasThis;
		public bool IsVirtual => (DefAttr & MethodAttributes.Virtual) != 0;

		// 是否已处理过
		public bool IsProcessed;
		// 是否跳过处理
		public bool IsSkipProcessing;

		public MethodX(TypeX declType, MethodDef metDef)
		{
			Debug.Assert(declType != null);
			Debug.Assert(metDef.DeclaringType == declType.Def);
			DeclType = declType;
			Def = metDef;
			DefName = metDef.Name;
			DefSig = metDef.MethodSig;
			DefAttr = metDef.Attributes;

			Debug.Assert((HasThis && !metDef.IsStatic) || (!HasThis && metDef.IsStatic));

			if (metDef.HasBody)
			{
				if (metDef.Body.HasExceptionHandlers)
					DefHandlers = metDef.Body.ExceptionHandlers;

				if (metDef.Body.HasVariables)
				{
					LocalTypes = new List<TypeSig>();
					foreach (var loc in metDef.Body.Variables)
					{
						Debug.Assert(loc.Index == LocalTypes.Count);
						LocalTypes.Add(loc.Type);
					}
				}

				if (metDef.Body.HasInstructions)
					DefInstList = metDef.Body.Instructions;
			}
		}

		public override string ToString()
		{
			return DeclType + " -> " + NameKey;
		}

		public string GetNameKey()
		{
			if (NameKey == null)
			{
				// Name|RetType<GenArgs>(DefArgList)|CC|Attr
				StringBuilder sb = new StringBuilder();
				Helper.MethodNameKeyWithGen(sb, DefName, GenArgs, DefSig.RetType, DefSig.Params, DefSig.CallingConvention);
				sb.Append('|');
				sb.Append(((uint)DefAttr).ToString("X"));

				NameKey = sb.ToString();
			}
			return NameKey;
		}

		public string GetReplacedNameKey()
		{
			Debug.Assert(ReturnType != null);
			Debug.Assert(ParamTypes != null);

			StringBuilder sb = new StringBuilder();
			Helper.MethodNameKeyWithGen(sb, DefName, GenArgs, ReturnType, ParamTypes, DefSig.CallingConvention);

			return sb.ToString();
		}

		public void AddOverrideImpl(MethodX impl)
		{
			if (OverrideImpls == null)
				OverrideImpls = new HashSet<MethodX>();
			OverrideImpls.Add(impl);
		}
	}
}