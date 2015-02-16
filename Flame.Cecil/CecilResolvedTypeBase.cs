﻿using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flame.Cecil
{
    public abstract class CecilResolvedTypeBase : CecilTypeBase
    {
        public CecilResolvedTypeBase()
        { }
        public CecilResolvedTypeBase(AncestryGraph AncestryGraph)
            : base(AncestryGraph)
        { }

        public virtual TypeDefinition GetResolvedType()
        {
            return GetTypeReference().Resolve();
        }

        protected IType GetEnumElementType()
        {
            foreach (var item in GetCecilFields())
            {
                if (item.Name == "value__" && item.IsRuntimeSpecialName && item.IsSpecialName)
                {
                    return new CecilField(this, item).FieldType;
                }
            }
            return null;
        }

        private IType[] cachedBaseTypes;
        protected virtual IType[] GetBaseTypesCore()
        {
            var type = GetResolvedType();
            var baseTypes = new List<IType>();
            if (type.BaseType != null)
            {
                if (type.BaseType.FullName == "System.Enum")
                {
                    baseTypes.Add(GetEnumElementType());
                }
                else
                {
                    baseTypes.Add(CecilTypeBase.ImportCecil(type.BaseType, this));
                }
            }
            else if (type.FullName == "System.Object")
            {
                baseTypes.Add(PrimitiveTypes.IEquatable);
                baseTypes.Add(PrimitiveTypes.IHashCodeProvider);
            }
            foreach (var item in type.Interfaces)
            {
                baseTypes.Add(CecilTypeBase.ImportCecil(item, this));
            }
            return baseTypes.ToArray();
        }
        public sealed override IType[] GetBaseTypes()
        {
            if (cachedBaseTypes == null)
            {
                cachedBaseTypes = GetBaseTypesCore();
            }
            return cachedBaseTypes;
        }

        public override ICecilType GetCecilGenericDeclaration()
        {
            var type = GetResolvedType();
            if (type.IsGenericInstance)
            {
                return CecilTypeBase.CreateCecil(type.GetElementType());
            }
            else
            {
                return CecilTypeBase.CreateCecil(type);
            }
        }

        protected const string StaticSingletonName = "Static_Singleton";

        protected string GetSingletonMemberName()
        {
            var resolvedType = GetResolvedType();
            foreach (var item in resolvedType.Properties)
            {
                var getMethod = item.GetMethod;
                if (getMethod != null && item.Name == "Instance" && getMethod.IsStatic && getMethod.ReturnType.Equals(resolvedType))
                {
                    return item.Name;
                }
            }
            return null;
        }

        protected bool IsSingletonProperty(IProperty Property)
        {
            return Property.Name == "Instance" && Property.IsStatic && Property.PropertyType.Equals(this);
        }

        protected IType GetAssociatedSingleton()
        {
            foreach (var item in GetTypes())
            {
                if (item.Name == StaticSingletonName && item.get_IsSingleton())
                {
                    return item;
                }
            }
            return null;
        }

        protected override IEnumerable<IAttribute> GetMemberAttributes()
        {
            List<IAttribute> attrs = new List<IAttribute>();
            var t = GetResolvedType();
            switch (t.Attributes & (TypeAttributes)0x7)
            {
                case TypeAttributes.NestedAssembly:
                    attrs.Add(new AccessAttribute(AccessModifier.Assembly));
                    break;
                case TypeAttributes.NestedFamANDAssem:
                    attrs.Add(new AccessAttribute(AccessModifier.ProtectedAndAssembly));
                    break;
                case TypeAttributes.NestedFamORAssem:
                    attrs.Add(new AccessAttribute(AccessModifier.ProtectedOrAssembly));
                    break;
                case TypeAttributes.NestedFamily:
                    attrs.Add(new AccessAttribute(AccessModifier.Protected));
                    break;
                case TypeAttributes.NestedPublic:
                case TypeAttributes.Public:
                    attrs.Add(new AccessAttribute(AccessModifier.Public));
                    break;
                case TypeAttributes.NestedPrivate:
                default:
                    attrs.Add(new AccessAttribute(AccessModifier.Private));
                    break;
            }
            if (t.IsValueType)
            {
                attrs.Add(PrimitiveAttributes.Instance.ValueTypeAttribute);
            }
            else if (t.IsInterface)
            {
                attrs.Add(PrimitiveAttributes.Instance.InterfaceAttribute);
            }
            if (t.IsEnum)
            {
                attrs.Add(PrimitiveAttributes.Instance.EnumAttribute);
            }
            string tName = t.FullName;
            if (tName == "System.Object")
            {
                attrs.Add(PrimitiveAttributes.Instance.RootTypeAttribute);
            }
            else if (tName.StartsWith("System.Collections.Generic.IEnumerable") && this.GetGenericDeclaration().Equals(ImportCecil(typeof(IEnumerable<>), this)))
            {
                if (this.get_IsGenericInstance())
                {
                    attrs.Add(new EnumerableAttribute(GetGenericArguments().First()));
                }
                else
                {
                    attrs.Add(new EnumerableAttribute(GetGenericParameters().First()));
                }
            }
            else if (tName.StartsWith("System.Collections.IEnumerable") && this.Equals(ImportCecil<System.Collections.IEnumerable>(this)))
            {
                attrs.Add(new EnumerableAttribute(ImportCecil<object>(this)));
            }
            string singletonMemberName = GetSingletonMemberName();
            if (singletonMemberName != null)
            {
                attrs.Add(new SingletonAttribute(singletonMemberName));
            }
            else
            {
                var associatedSingleton = GetAssociatedSingleton();
                if (associatedSingleton != null)
                {
                    attrs.Add(new AssociatedTypeAttribute(associatedSingleton));
                }
            }
            return attrs;
        }

        protected override IList<CustomAttribute> GetCustomAttributes()
        {
            return GetResolvedType().CustomAttributes;
        }

        public override bool IsContainerType
        {
            get { return false; }
        }

        public override IContainerType AsContainerType()
        {
            return null;
        }

        public override IType ResolveTypeParameter(IGenericParameter TypeParameter)
        {
            return null;
        }

        public override bool IsComplete
        {
            get { return true; }
        }

        #region Generics

        public override IEnumerable<IType> GetCecilGenericArguments()
        {
            var declGeneric = DeclaringGenericMember;
            if (declGeneric != null)
            {
                return declGeneric.GetCecilGenericParameters().Prefer(declGeneric.GetCecilGenericArguments());
            }
            else
            {
                return new IType[0];
            }
        }

        public override IEnumerable<IType> GetGenericArguments()
        {
            return new IType[0];
        }

        #endregion

        #region Type Members

        protected override IList<MethodDefinition> GetCecilMethods()
        {
            return GetResolvedType().Methods;
        }

        protected override IList<PropertyDefinition> GetCecilProperties()
        {
            return GetResolvedType().Properties;
        }

        protected override IList<FieldDefinition> GetCecilFields()
        {
            return GetResolvedType().Fields;
        }

        protected override IList<EventDefinition> GetCecilEvents()
        {
            return GetResolvedType().Events;
        }

        #endregion

        public override IBoundObject GetDefaultValue()
        {
            var created = CecilTypeBase.Create(GetTypeReference());
            if (created.get_IsPrimitive())
            {
                return created.GetDefaultValue();
            }
            else if (this.get_IsReferenceType())
            {
                return new Flame.Compiler.Expressions.NullExpression();
            }
            throw new NotImplementedException();
        }
    }
}
