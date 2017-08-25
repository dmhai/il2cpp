﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.Threading;

namespace il2cpp
{
	// 泛型签名替换器
	internal interface IGenericReplacer
	{
		TypeSig Replace(GenericVar genVarSig);
		TypeSig Replace(GenericMVar genMVarSig);
	}

	internal class GenericReplacer : IGenericReplacer
	{
		public readonly TypeX OwnerType;
		public readonly MethodX OwnerMethod;

		public GenericReplacer(TypeX ownerTyX, MethodX ownerMetX)
		{
			OwnerType = ownerTyX;
			OwnerMethod = ownerMetX;
		}

		public TypeSig Replace(GenericVar genVarSig)
		{
			if (genVarSig.OwnerType == OwnerType.Def)
				return OwnerType.GenArgs[(int)genVarSig.Number];
			return genVarSig;
		}

		public TypeSig Replace(GenericMVar genMVarSig)
		{
			if (genMVarSig.OwnerMethod == OwnerMethod.Def)
				return OwnerMethod.GenArgs[(int)genMVarSig.Number];
			return genMVarSig;
		}
	}

	internal class TypeDefGenReplacer : IGenericReplacer
	{
		public readonly TypeDef OwnerType;
		public readonly IList<TypeSig> TypeGenArgs;

		public TypeDefGenReplacer(TypeDef ownerTyDef, IList<TypeSig> tyGenArgs)
		{
			OwnerType = ownerTyDef;
			TypeGenArgs = tyGenArgs;
		}

		public TypeSig Replace(GenericVar genVarSig)
		{
			if (TypeEqualityComparer.Instance.Equals(genVarSig.OwnerType, OwnerType))
				return TypeGenArgs[(int)genVarSig.Number];
			return null;
		}

		public TypeSig Replace(GenericMVar genMVarSig)
		{
			return null;
		}
	}

	// 辅助扩展方法
	internal static class Helper
	{
		// 替换类型中的泛型签名
		public static TypeSig ReplaceGenericSig(TypeSig tySig, IGenericReplacer replacer)
		{
			if (replacer == null || !IsReplaceNeeded(tySig))
				return tySig;

			return ReplaceGenericSigImpl(tySig, replacer);
		}

		private static TypeSig ReplaceGenericSigImpl(TypeSig tySig, IGenericReplacer replacer)
		{
			if (tySig == null)
				return null;

			switch (tySig.ElementType)
			{
				case ElementType.Class:
				case ElementType.ValueType:
					return tySig;

				case ElementType.Ptr:
					return new PtrSig(ReplaceGenericSigImpl(tySig.Next, replacer));
				case ElementType.ByRef:
					return new ByRefSig(ReplaceGenericSigImpl(tySig.Next, replacer));
				case ElementType.Pinned:
					return new PinnedSig(ReplaceGenericSigImpl(tySig.Next, replacer));
				case ElementType.SZArray:
					return new SZArraySig(ReplaceGenericSigImpl(tySig.Next, replacer));

				case ElementType.Array:
					{
						ArraySig arySig = (ArraySig)tySig;
						return new ArraySig(ReplaceGenericSigImpl(arySig.Next, replacer),
							arySig.Rank,
							arySig.Sizes,
							arySig.LowerBounds);
					}
				case ElementType.CModReqd:
					{
						CModReqdSig modreqdSig = (CModReqdSig)tySig;
						return new CModReqdSig(modreqdSig.Modifier, ReplaceGenericSigImpl(modreqdSig.Next, replacer));
					}
				case ElementType.CModOpt:
					{
						CModOptSig modoptSig = (CModOptSig)tySig;
						return new CModOptSig(modoptSig.Modifier, ReplaceGenericSigImpl(modoptSig.Next, replacer));
					}
				case ElementType.GenericInst:
					{
						GenericInstSig genInstSig = (GenericInstSig)tySig;
						return new GenericInstSig(genInstSig.GenericType, ReplaceGenericSigListImpl(genInstSig.GenericArguments, replacer));
					}

				case ElementType.Var:
					{
						GenericVar genVarSig = (GenericVar)tySig;
						TypeSig result = replacer.Replace(genVarSig);
						if (result != null)
							return result;
						return new GenericVar(genVarSig.Number, genVarSig.OwnerType);
					}
				case ElementType.MVar:
					{
						GenericMVar genMVarSig = (GenericMVar)tySig;
						TypeSig result = replacer.Replace(genMVarSig);
						if (result != null)
							return result;
						return new GenericMVar(genMVarSig.Number, genMVarSig.OwnerMethod);
					}

				default:
					if (tySig is CorLibTypeSig)
						return tySig;

					throw new NotSupportedException();
			}
		}

		// 替换类型签名列表
		public static IList<TypeSig> ReplaceGenericSigList(IList<TypeSig> tySigList, IGenericReplacer replacer)
		{
			return tySigList?.Select(tySig => ReplaceGenericSig(tySig, replacer)).ToList();
		}

		private static IList<TypeSig> ReplaceGenericSigListImpl(IList<TypeSig> tySigList, IGenericReplacer replacer)
		{
			return tySigList?.Select(tySig => ReplaceGenericSigImpl(tySig, replacer)).ToList();
		}

		// 检查是否存在要替换的泛型签名
		private static bool IsReplaceNeeded(TypeSig tySig)
		{
			while (tySig != null)
			{
				switch (tySig.ElementType)
				{
					case ElementType.Var:
					case ElementType.MVar:
						return true;

					case ElementType.GenericInst:
						{
							GenericInstSig genInstSig = (GenericInstSig)tySig;
							foreach (var arg in genInstSig.GenericArguments)
							{
								if (IsReplaceNeeded(arg))
									return true;
							}
							break;
						}
				}

				tySig = tySig.Next;
			}
			return false;
		}

		private static bool IsInstantiatableTypeSigList(IList<TypeSig> tySigList)
		{
			return tySigList == null || tySigList.All(IsInstantiatableTypeSig);
		}

		// 检查类型签名是否可实例化
		private static bool IsInstantiatableTypeSig(TypeSig tySig)
		{
			while (tySig != null)
			{
				switch (tySig.ElementType)
				{
					case ElementType.Var:
						return false;
					case ElementType.MVar:
						throw new ArgumentOutOfRangeException();

					case ElementType.GenericInst:
						{
							GenericInstSig genInstSig = (GenericInstSig)tySig;
							foreach (var arg in genInstSig.GenericArguments)
							{
								if (!IsInstantiatableTypeSig(arg))
									return false;
							}
							break;
						}
				}

				tySig = tySig.Next;
			}
			return true;
		}

		public static void TypeNameKey(
			StringBuilder sb,
			string name,
			IList<TypeSig> genArgs)
		{
			sb.Append(EscapeName(name));
			if (genArgs != null && genArgs.Count > 0)
			{
				sb.Append('<');
				TypeSigListName(sb, genArgs, true);
				sb.Append('>');
			}
		}

		public static void MethodNameKey(
			StringBuilder sb,
			string name,
			int genCount,
			TypeSig retType,
			IList<TypeSig> paramTypes,
			CallingConvention callConv)
		{
			sb.Append(EscapeName(name));
			sb.Append('|');

			TypeSigName(sb, retType, false);

			if (genCount > 0)
			{
				sb.Append('<');
				sb.Append(genCount);
				sb.Append('>');
			}

			sb.Append('(');
			TypeSigListName(sb, paramTypes, false);
			sb.Append(')');
			sb.Append('|');

			sb.Append(((uint)callConv).ToString("X"));
		}

		public static void MethodNameKeyWithGen(
			StringBuilder sb,
			string name,
			IList<TypeSig> genArgs,
			TypeSig retType,
			IList<TypeSig> paramTypes,
			CallingConvention callConv)
		{
			sb.Append(EscapeName(name));
			sb.Append('|');

			TypeSigName(sb, retType, false);

			if (genArgs != null && genArgs.Count > 0)
			{
				sb.Append('<');
				TypeSigListName(sb, genArgs, false);
				sb.Append('>');
			}

			sb.Append('(');
			TypeSigListName(sb, paramTypes, false);
			sb.Append(')');
			sb.Append('|');

			sb.Append(((uint)callConv).ToString("X"));
		}

		public static void FieldNameKey(
			StringBuilder sb,
			string name,
			TypeSig fldType)
		{
			sb.Append(EscapeName(name));
			sb.Append('|');
			TypeSigName(sb, fldType, false);
		}

		public static void TypeSigListName(StringBuilder sb, IList<TypeSig> tySigList, bool printGenOwner)
		{
			bool last = false;
			foreach (var tySig in tySigList)
			{
				if (last)
					sb.Append(',');
				last = true;
				TypeSigName(sb, tySig, printGenOwner);
			}
		}

		public static void TypeSigName(StringBuilder sb, TypeSig tySig, bool printGenOwner)
		{
			if (tySig == null)
				return;

			switch (tySig.ElementType)
			{
				case ElementType.Class:
				case ElementType.ValueType:
					sb.Append(ClassSigName(tySig));
					return;

				case ElementType.Ptr:
					TypeSigName(sb, tySig.Next, printGenOwner);
					sb.Append('*');
					return;

				case ElementType.ByRef:
					TypeSigName(sb, tySig.Next, printGenOwner);
					sb.Append('&');
					return;

				case ElementType.Pinned:
					TypeSigName(sb, tySig.Next, printGenOwner);
					return;

				case ElementType.SZArray:
					TypeSigName(sb, tySig.Next, printGenOwner);
					sb.Append("[]");
					return;

				case ElementType.Array:
					{
						TypeSigName(sb, tySig.Next, printGenOwner);
						ArraySig arySig = (ArraySig)tySig;
						sb.Append('[');
						uint rank = arySig.Rank;
						if (rank == 0)
							throw new NotSupportedException();
						else if (rank == 1)
							sb.Append('*');
						else
						{
							for (int i = 0; i < (int)rank; i++)
							{
								if (i != 0)
									sb.Append(',');

								const int NO_LOWER = int.MinValue;
								const uint NO_SIZE = uint.MaxValue;
								int lower = arySig.LowerBounds.Get(i, NO_LOWER);
								uint size = arySig.Sizes.Get(i, NO_SIZE);
								if (lower != NO_LOWER)
								{
									sb.Append(lower);
									sb.Append("..");
									if (size != NO_SIZE)
										sb.Append(lower + (int)size - 1);
									else
										sb.Append('.');
								}
							}
						}
						sb.Append(']');
						return;
					}

				case ElementType.CModReqd:
					TypeSigName(sb, tySig.Next, printGenOwner);
					sb.AppendFormat(" modreq({0})", ((CModReqdSig)tySig).Modifier.FullName);
					return;

				case ElementType.CModOpt:
					TypeSigName(sb, tySig.Next, printGenOwner);
					sb.AppendFormat(" modopt({0})", ((CModOptSig)tySig).Modifier.FullName);
					return;

				case ElementType.GenericInst:
					{
						GenericInstSig genInstSig = (GenericInstSig)tySig;
						TypeSigName(sb, genInstSig.GenericType, printGenOwner);
						sb.Append('<');
						TypeSigListName(sb, genInstSig.GenericArguments, printGenOwner);
						sb.Append('>');
						return;
					}

				case ElementType.Var:
				case ElementType.MVar:
					{
						var genSig = (GenericSig)tySig;
						if (genSig.IsMethodVar)
						{
							sb.Append("!!");
						}
						else
						{
							sb.Append('!');
							if (printGenOwner)
							{
								sb.Append('(');
								sb.Append(genSig.OwnerType.FullName);
								sb.Append(')');
							}
						}
						sb.Append(genSig.Number);
						return;
					}

				default:
					if (tySig is CorLibTypeSig)
					{
						sb.Append(ClassSigName(tySig));
						return;
					}

					throw new ArgumentOutOfRangeException();
			}
		}

		private static string ClassSigName(TypeSig tySig)
		{
			if (tySig.DefinitionAssembly.IsCorLib())
				return tySig.TypeName;
			return tySig.FullName;
		}

		private static string EscapeChar(char ch)
		{
			if (ch >= 'a' && ch <= 'z' ||
				ch >= 'A' && ch <= 'Z' ||
				ch >= '0' && ch <= '9' ||
				ch == '_' || ch == '`' || ch == '.' ||
				ch == ':' || ch == '/')
			{
				return null;
			}
			return @"\u" + ((uint)ch).ToString("X4");
		}

		// 转义特殊符号
		private static string EscapeName(string name)
		{
			string result = null;
			foreach (char ch in name)
			{
				string escape = EscapeChar(ch);
				if (escape == null)
					result += ch;
				else
					result += escape;
			}
			return result;
		}
	}
}