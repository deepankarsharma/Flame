using System.Collections.Generic;
using Flame.Collections;

namespace Flame.TypeSystem
{
    /// <summary>
    /// Indexes assemblies and resolves types based on their names.
    /// </summary>
    public sealed class TypeResolver
    {
        /// <summary>
        /// Creates an empty type resolver.
        /// </summary>
        public TypeResolver()
        {
            this.assemblySet = new HashSet<IAssembly>();
            this.RootNamespace = new TypeResolverNamespace();
        }

        private HashSet<IAssembly> assemblySet;

        /// <summary>
        /// Gets a list of all assemblies that are taken into consideration
        /// by this type resolver when resolving a type name.
        /// </summary>
        public IEnumerable<IAssembly> Assemblies => assemblySet;

        /// <summary>
        /// Gets the root namespace for this type resolver.
        /// </summary>
        /// <returns>The root namespace.</returns>
        public TypeResolverNamespace RootNamespace { get; private set; }

        /// <summary>
        /// Adds an assembly to this type resolver.
        /// </summary>
        /// <param name="assembly">The assembly to add.</param>
        /// <returns>
        /// <c>true</c> if the assembly was just added;
        /// <c>false</c> if it has already been added before.
        /// </returns>
        public bool AddAssembly(IAssembly assembly)
        {
            var isNew = assemblySet.Add(assembly);
            if (isNew)
            {
                foreach (var type in assembly.Types)
                {
                    RootNamespace.Add(type.FullName, type);
                }
            }
            return isNew;
        }
    }

    /// <summary>
    /// An artifical namespace introduced by a type resolver.
    /// </summary>
    public sealed class TypeResolverNamespace
    {
        internal TypeResolverNamespace()
        {
            this.typeMap = new Dictionary<UnqualifiedName, List<IType>>();
            this.impreciseTypeMap = new Dictionary<string, List<IType>>();
            this.namespaceMap = new Dictionary<UnqualifiedName, TypeResolverNamespace>();
            this.typeSet = new HashSet<IType>();
        }

        private Dictionary<UnqualifiedName, List<IType>> typeMap;

        private Dictionary<string, List<IType>> impreciseTypeMap;

        private Dictionary<UnqualifiedName, TypeResolverNamespace> namespaceMap;

        private HashSet<IType> typeSet;

        /// <summary>
        /// Gets the set of all types defined by this resolver.
        /// </summary>
        public IEnumerable<IType> Types => typeSet;

        /// <summary>
        /// Gets a mapping of names to child namespaces defined in this namespace.
        /// </summary>
        public IReadOnlyDictionary<UnqualifiedName, TypeResolverNamespace> Namespaces =>
            namespaceMap;

        /// <summary>
        /// Gets all types in this namespace with a particular name.
        /// </summary>
        /// <param name="name">The name to look for.</param>
        /// <returns>A list of all types with that name.</returns>
        public IReadOnlyList<IType> ResolveTypes(UnqualifiedName name)
        {
            List<IType> result;
            if (typeMap.TryGetValue(name, out result))
            {
                return result;
            }
            else
            {
                return EmptyArray<IType>.Value;
            }
        }

        /// <summary>
        /// Gets all types in this namespace with a particular name.
        /// </summary>
        /// <param name="name">The name to look for.</param>
        /// <returns>
        /// A list of all types with that name. This includes all simply
        /// named types with name <paramref name="name"/>, regardless of
        /// the number of type parameters in the type's name.
        /// </returns>
        public IReadOnlyList<IType> ResolveTypes(string name)
        {
            List<IType> result;
            if (impreciseTypeMap.TryGetValue(name, out result))
            {
                return result;
            }
            else
            {
                return EmptyArray<IType>.Value;
            }
        }

        private static List<TValue> GetOrCreateBag<TKey, TValue>(
            Dictionary<TKey, List<TValue>> dict,
            TKey key)
        {
            List<TValue> result;
            if (!dict.TryGetValue(key, out result))
            {
                result = new List<TValue>();
                dict[key] = result;
            }
            return result;
        }

        internal void Add(QualifiedName path, IType type)
        {
            var qualifier = path.Qualifier;
            if (path.IsQualified)
            {
                TypeResolverNamespace subNamespace;
                if (!namespaceMap.TryGetValue(qualifier, out subNamespace))
                {
                    subNamespace = new TypeResolverNamespace();
                    namespaceMap[qualifier] = subNamespace;
                }
                subNamespace.Add(path.Name, type);
            }
            else if (typeSet.Add(type))
            {
                GetOrCreateBag(typeMap, qualifier).Add(type);
                GetOrCreateBag(impreciseTypeMap, ToImpreciseName(qualifier)).Add(type);
            }
        }

        /// <summary>
        /// Takes an unqualified name and turns it into an imprecise name by
        /// dropping the type parameter count of simple names and converting
        /// all other kinds of names into strings.
        /// </summary>
        /// <param name="name">The name to convert.</param>
        /// <returns>An imprecise name.</returns>
        public static string ToImpreciseName(UnqualifiedName name)
        {
            if (name is SimpleName)
            {
                return ((SimpleName)name).Name;
            }
            else
            {
                return name.ToString();
            }
        }
    }
}