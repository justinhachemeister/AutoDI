﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

[assembly: InternalsVisibleTo("AutoDI.Container.Tests")]

namespace AutoDI.Container.Fody
{
    internal class InternalMap
    {
        private readonly Dictionary<Type, Delegate> _accessors = new Dictionary<Type, Delegate>();

        public void AddOnce<TKey, TValue>(TValue instance)
        {
            _accessors[typeof(TKey)] = new Func<TValue>(() => instance);
        }

        public void AddLazy<TKey, TValue>(Func<TValue> create)
        {
            var lazy = new Lazy<TValue>(create);
            _accessors[typeof(TKey)] = new Func<TValue>(() => lazy.Value);
        }

        public void AddSingle<TKey, TValue>(Func<TValue> create) where TValue : class
        {
            var weakRef = new WeakReference<TValue>(default(TValue));
            _accessors[typeof(TKey)] = new Func<TValue>(() =>
            {
                lock (weakRef)
                {
                    if (!weakRef.TryGetTarget(out TValue value))
                    {
                        value = create();
                        weakRef.SetTarget(value);
                    }
                    return value;
                }
            });
        }

        public void AddAlways<TKey, TValue>(Func<TValue> create)
        {
            _accessors[typeof(TKey)] = create;
        }

        public T Get<T>()
        {
            if (_accessors.TryGetValue(typeof(T), out Delegate d)
                && d is Func<T> func)
            {
                return func();
            }
            return default(T);
        }
    }

    internal class Settings
    {
        internal Settings(Behaviors behavior)
        {
            Behavior = behavior;
        }

        public Behaviors Behavior { get; }

        public IList<MatchType> Types { get; } = new List<MatchType>();

        public IList<Map> Maps { get; } = new List<Map>();

        public static Settings Parse(XElement fodyWeaversRoot)
        {
            Behaviors behavior = Behaviors.Default;

            XElement containerRoot = fodyWeaversRoot.DescendantNodes().OfType<XElement>().FirstOrDefault(x => x.Name.LocalName == "AutoDI.Container");
            if (containerRoot == null)
                throw new InvalidOperationException("Could not find AutoDI.Container element in FodyWeavers.xml"); //How did we get here?

            var behaviorAttribute = containerRoot.GetAttributeValue("Behavior");
            if (behaviorAttribute != null)
            {
                behavior = Behaviors.None;
                foreach (string value in behaviorAttribute.Split(','))
                {
                    if (Enum.TryParse(value, out Behaviors @enum))
                        behavior |= @enum;
                }
            }

            var rv = new Settings(behavior);

            foreach (XElement typeNode in containerRoot.DescendantNodes().OfType<XElement>()
                .Where(x => string.Equals(x.Name.LocalName, "Type", StringComparison.OrdinalIgnoreCase)))
            {
                string typePattern = typeNode.GetAttributeValue("Name");
                if (string.IsNullOrWhiteSpace(typePattern)) continue;
                string createStr = typeNode.GetAttributeValue("Create");
                Create create;
                if (createStr == null || !Enum.TryParse(createStr, out create))
                {
                    create = Create.Once;
                }

                rv.Types.Add(new MatchType(typePattern, create));
            }

            foreach (XElement mapNode in containerRoot.DescendantNodes().OfType<XElement>()
                .Where(x => string.Equals(x.Name.LocalName, "Map", StringComparison.OrdinalIgnoreCase)))
            {
                string from = mapNode.GetAttributeValue("From");
                if (string.IsNullOrWhiteSpace(from)) continue;
                string to = mapNode.GetAttributeValue("To");
                if (string.IsNullOrWhiteSpace(to)) continue;

                rv.Maps.Add(new Map(from, to));
            }

            return rv;
        }
    }

    internal static class XmlMixins
    {
        public static string GetAttributeValue(this XElement element, string attributeName)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            return element.Attributes()
                .FirstOrDefault(a => string.Equals(a.Name.LocalName, attributeName, StringComparison.OrdinalIgnoreCase))
                ?.Value;
        }
    }

    internal class Map
    {
        private readonly string _to;
        private readonly Regex _fromRegex;

        public Map(string from, string to)
        {
            _to = to;
            _fromRegex = new Regex(from);
        }


        public bool TryGetMap(string fromType, out string mappedType)
        {
            Match fromMatch = _fromRegex.Match(fromType);
            if (fromMatch.Success)
            {
                mappedType = _fromRegex.Replace(fromType, _to);
                return true;
            }
            mappedType = null;
            return false;
        }
    }

    internal class MatchType
    {
        private readonly Regex _typeRegex;
        public MatchType(string type, Create create)
        {
            _typeRegex = new Regex(type);
            Create = create;
        }

        public Create Create { get; }

        public bool Matches(string type) => _typeRegex.IsMatch(type);
    }

    public enum Create
    {
        OnceLazy,
        Once,
        Single,
        Always
    }

    [Flags]
    public enum Behaviors
    {
        None = 0,
        SingleInterfaceImplementation = 1,
        IncludeClasses = 2,
        Default = SingleInterfaceImplementation | IncludeClasses
    }
}