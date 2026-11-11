using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using FullSerializer;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NBC.ActionEditor.Serialization
{
    /// <summary>One controlled field converter for documents, snapshots and clipboard fragments.</summary>
    public static class DocumentValueConverter
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly Dictionary<Type, FieldInfo[]> fields = new Dictionary<Type, FieldInfo[]>();
        private static InvalidDataException Error(string path, string reason) => new InvalidDataException(path + ": " + reason);
        public static FieldInfo[] Fields(Type type)
        {
            if (fields.TryGetValue(type, out var found)) return (FieldInfo[])found.Clone();
            var result = new List<FieldInfo>();
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
                result.AddRange(current.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(f => !f.IsStatic && !f.IsNotSerialized && !f.IsDefined(typeof(fsIgnoreAttribute), true)
                        && (f.IsPublic || f.IsDefined(typeof(SerializeField), true))));
            if (result.Any(f => f.Name.StartsWith("$", StringComparison.Ordinal)) || result.GroupBy(f => f.Name, StringComparer.Ordinal).Any(g => g.Count() != 1))
                throw Error("$", "conflicting field keys on " + DocumentFormatContext.Identity(type));
            found = result.OrderBy(f => f.Name, StringComparer.Ordinal).ToArray(); fields.Add(type, found);
            return (FieldInfo[])found.Clone();
        }
        private static bool Node(Type type) => typeof(Asset).IsAssignableFrom(type) || typeof(Group).IsAssignableFrom(type)
            || typeof(Track).IsAssignableFrom(type) || typeof(Clip).IsAssignableFrom(type);
        private static bool Components(Type type) => type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4)
            || type == typeof(Quaternion) || type == typeof(Color) || type == typeof(Keyframe);
        private static string[] ComponentNames(Type type)
        {
            if (type == typeof(Color)) return new[] { "r", "g", "b", "a" };
            if (type == typeof(Keyframe)) return new[] { "time", "value", "inTangent", "outTangent", "inWeight", "outWeight", "weightedMode" };
            return type == typeof(Vector2) ? new[] { "x", "y" } : type == typeof(Vector3) ? new[] { "x", "y", "z" } : new[] { "x", "y", "z", "w" };
        }
        private static object Component(object value, string name)
        {
            var field = value.GetType().GetField(name);
            return field != null ? field.GetValue(value) : value.GetType().GetProperty(name).GetValue(value);
        }
        private static Type ComponentType(Type type, string name) => type.GetField(name)?.FieldType ?? type.GetProperty(name).PropertyType;
        private static void SetComponent(object value, string name, object component)
        {
            var field = value.GetType().GetField(name);
            if (field != null) field.SetValue(value, component); else value.GetType().GetProperty(name).SetValue(value, component);
        }
        private static Type Element(Type type)
        {
            if (type.IsArray && type.GetArrayRank() == 1) return type.GetElementType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return type.GetGenericArguments()[0];
            return null;
        }
        public static DocumentValue Capture(object value, Type declaration, DocumentFormatContext context, bool authorContent = false)
            => Capture(value, declaration, context, authorContent, "$", new HashSet<object>(ReferenceComparer.Instance));
        private static DocumentValue Capture(object value, Type declaration, DocumentFormatContext context, bool content, string path, HashSet<object> active)
        {
            if (value == null) return DocumentValue.Null();
            var type = value.GetType();
            if (!declaration.IsAssignableFrom(type) || declaration == typeof(object)) throw Error(path, "unsupported declaration.");
            if (value is Object unity)
            {
                if (context.References == null) throw Error(path, "missing Unity reference resolver.");
                DocumentReference reference;
                try { reference = context.References.Identify(unity, declaration); }
                catch { throw Error(path, "Unity reference cannot be persisted."); }
                ValidateReference(reference, path);
                return Map(("$unity", Map(("guid", DocumentValue.String(reference.Guid)), ("localFileId", DocumentValue.Number(reference.LocalFileId.ToString(Invariant))))));
            }
            if (type == typeof(string)) return DocumentValue.String((string)value);
            if (type == typeof(bool)) return DocumentValue.Boolean((bool)value);
            if (type == typeof(float))
            {
                return DocumentValue.Number(DocumentNumber.Single((float)value, path));
            }
            if (type == typeof(double))
            {
                var number = (double)value;
                if (double.IsNaN(number) || double.IsInfinity(number)) throw Error(path, "non-finite Double.");
                return DocumentValue.Number(number == 0 && BitConverter.DoubleToInt64Bits(number) < 0 ? "-0.0" : number.ToString("R", Invariant));
            }
            if (type.IsEnum) { ValidateEnum(type, value, path); return DocumentValue.Number(Convert.ChangeType(value, Enum.GetUnderlyingType(type), Invariant).ToString()); }
            if (Integer(type)) return DocumentValue.Number(Convert.ToString(value, Invariant));
            if (!type.IsValueType && !active.Add(value)) throw Error(path, "cyclic value graph.");
            try
            {
                var element = Element(type);
                if (element != null)
                    return DocumentValue.List(((IEnumerable)value).Cast<object>().Select((v, i) => Capture(v, element, context, content, path + "[" + i + "]", active)).ToArray());
                if (type == typeof(AnimationCurve))
                {
                    var curve = (AnimationCurve)value;
                    return Map(("keys", Capture(curve.keys, typeof(Keyframe[]), context, content, path + ".keys", active)),
                        ("preWrapMode", Capture(curve.preWrapMode, typeof(UnityEngine.WrapMode), context, content, path + ".preWrapMode", active)),
                        ("postWrapMode", Capture(curve.postWrapMode, typeof(UnityEngine.WrapMode), context, content, path + ".postWrapMode", active)));
                }
                if (Components(type)) return DocumentValue.Object(ComponentNames(type).ToDictionary(n => n, n => Capture(Component(value, n), ComponentType(type, n), context, content, path + "." + n, active), StringComparer.Ordinal));
                RequireBusinessType(type, context, path);
                var members = new Dictionary<string, DocumentValue>(StringComparer.Ordinal);
                if (Node(type) || type != declaration || declaration.IsAbstract || declaration.IsInterface) members.Add("$type", DocumentValue.String(DocumentFormatContext.Identity(type)));
                foreach (var field in Fields(type))
                {
                    if (content && (field.DeclaringType == typeof(Asset) && (field.Name == "viewTimeMin" || field.Name == "viewTimeMax")
                        || field.DeclaringType == typeof(Group) && field.Name == "isCollapsed")) continue;
                    members.Add(field.Name, Capture(field.GetValue(value), field.FieldType, context, content, path + "." + field.Name, active));
                }
                return DocumentValue.Object(members);
            }
            finally { if (!type.IsValueType) active.Remove(value); }
        }
        private static bool Integer(Type type) => type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
            || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong);
        private static void RequireBusinessType(Type type, DocumentFormatContext context, string path)
        {
            if (type == typeof(object) || typeof(IEnumerable).IsAssignableFrom(type) || typeof(Delegate).IsAssignableFrom(type)) throw Error(path, "unsupported value type.");
            context.RequireType(type, path);
        }
        private static void ValidateEnum(Type type, object value, string path)
        {
            if (Enum.IsDefined(type, value)) return;
            if (type.IsDefined(typeof(FlagsAttribute), false))
            {
                try
                {
                    var all = Enum.GetValues(type).Cast<object>().Aggregate(0UL, (mask, v) => mask | Convert.ToUInt64(v, Invariant));
                    if ((Convert.ToUInt64(value, Invariant) & ~all) == 0) return;
                }
                catch (OverflowException) { }
            }
            throw Error(path, "undefined enum value.");
        }
        private static void ValidateReference(DocumentReference reference, string path)
        {
            if (reference.Guid == null || reference.Guid.Length != 32 || reference.Guid.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f'))
                || reference.Guid.All(c => c == '0') || reference.LocalFileId == 0) throw Error(path, "invalid Unity reference identity.");
        }
        public static DocumentValue Map(params (string Key, DocumentValue Value)[] pairs) => DocumentValue.Object(pairs.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
        private static void Keys(DocumentValue value, IEnumerable<string> expected, string path)
        {
            if (value.Kind != DocumentValue.ValueKind.Object || !value.Members.Keys.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(k => k, StringComparer.Ordinal)))
                throw Error(path, "missing, unknown or conflicting members.");
        }
        private static string Scalar(DocumentValue value, DocumentValue.ValueKind kind, string path)
        { if (value.Kind != kind) throw Error(path, "incorrect value kind."); return value.Scalar; }
        /// <summary>Validates the entire token graph before invoking any business constructor.</summary>
        public static object Restore(DocumentValue value, Type declaration, DocumentFormatContext context)
        { var plan = Prepare(value, declaration, context, "$"); return plan(); }
        private static Func<object> Prepare(DocumentValue value, Type declaration, DocumentFormatContext context, string path)
        {
            if (declaration == typeof(object)) throw Error(path, "untyped object is unsupported.");
            if (value.Kind == DocumentValue.ValueKind.Null)
            {
                if (declaration.IsValueType) throw Error(path, "null for value type.");
                return () => null;
            }
            if (typeof(Object).IsAssignableFrom(declaration))
            {
                Keys(value, new[] { "$unity" }, path); var reference = value.Members["$unity"];
                Keys(reference, new[] { "guid", "localFileId" }, path + ".$unity");
                var guid = Scalar(reference.Members["guid"], DocumentValue.ValueKind.String, path);
                var idText = Scalar(reference.Members["localFileId"], DocumentValue.ValueKind.Number, path);
                if (!long.TryParse(idText, NumberStyles.AllowLeadingSign, Invariant, out var id)) throw Error(path, "invalid localFileId.");
                var identity = new DocumentReference(guid, id); ValidateReference(identity, path);
                if (context.References == null) throw Error(path, "missing Unity reference resolver.");
                Object resolved;
                try { resolved = context.References.Resolve(identity, declaration); }
                catch { throw Error(path, "unresolved Unity reference."); }
                if (resolved == null || !declaration.IsInstanceOfType(resolved)) throw Error(path, "missing or incompatible Unity reference.");
                return () => resolved;
            }
            if (declaration == typeof(string)) { var text = Scalar(value, DocumentValue.ValueKind.String, path); return () => text; }
            if (declaration == typeof(bool)) { var boolean = Scalar(value, DocumentValue.ValueKind.Boolean, path) == "true"; return () => boolean; }
            if (declaration.IsEnum || Integer(declaration) || declaration == typeof(float) || declaration == typeof(double))
            {
                var text = Scalar(value, DocumentValue.ValueKind.Number, path); object number;
                try
                {
                    if (declaration == typeof(float))
                    {
                        number = DocumentNumber.Single(text, path);
                    }
                    else if (declaration == typeof(double))
                    {
                        var parsed = double.Parse(text, NumberStyles.Float, Invariant);
                        if (double.IsNaN(parsed) || double.IsInfinity(parsed)) throw Error(path, "non-finite Double.");
                        number = parsed == 0 && text[0] == '-' ? BitConverter.Int64BitsToDouble(long.MinValue) : parsed;
                    }
                    else
                    {
                        if (text.IndexOfAny(new[] { '.', 'e', 'E' }) >= 0) throw Error(path, "integer required.");
                        number = Convert.ChangeType(text, declaration.IsEnum ? Enum.GetUnderlyingType(declaration) : declaration, Invariant);
                        if (declaration.IsEnum) { number = Enum.ToObject(declaration, number); ValidateEnum(declaration, number, path); }
                    }
                }
                catch (Exception error) when (error is FormatException || error is OverflowException) { throw Error(path, "numeric overflow or invalid number."); }
                return () => number;
            }
            var element = Element(declaration);
            if (element != null)
            {
                if (value.Kind != DocumentValue.ValueKind.Array) throw Error(path, "array required.");
                var plans = value.Items.Select((v, i) => Prepare(v, element, context, path + "[" + i + "]")).ToArray();
                return () =>
                {
                    if (declaration.IsArray) { var array = Array.CreateInstance(element, plans.Length); for (var i = 0; i < plans.Length; i++) array.SetValue(plans[i](), i); return array; }
                    var list = (IList)Activator.CreateInstance(declaration); foreach (var plan in plans) list.Add(plan()); return list;
                };
            }
            if (declaration == typeof(AnimationCurve))
            {
                Keys(value, new[] { "keys", "preWrapMode", "postWrapMode" }, path);
                var keys = Prepare(value.Members["keys"], typeof(Keyframe[]), context, path + ".keys");
                var pre = Prepare(value.Members["preWrapMode"], typeof(UnityEngine.WrapMode), context, path + ".preWrapMode");
                var post = Prepare(value.Members["postWrapMode"], typeof(UnityEngine.WrapMode), context, path + ".postWrapMode");
                return () => new AnimationCurve((Keyframe[])keys()) { preWrapMode = (UnityEngine.WrapMode)pre(), postWrapMode = (UnityEngine.WrapMode)post() };
            }
            if (Components(declaration))
            {
                var names = ComponentNames(declaration); Keys(value, names, path);
                var plans = names.Select(n => Prepare(value.Members[n], ComponentType(declaration, n), context, path + "." + n)).ToArray();
                return () => { var result = Activator.CreateInstance(declaration); for (var i = 0; i < names.Length; i++) SetComponent(result, names[i], plans[i]()); return result; };
            }
            if (value.Kind != DocumentValue.ValueKind.Object) throw Error(path, "object required.");
            var type = declaration;
            var tagged = value.Members.TryGetValue("$type", out var tag);
            if (Node(type) || type.IsAbstract || type.IsInterface || tagged)
            {
                if (!tagged) throw Error(path, "missing type identity.");
                type = context.ResolveType(Scalar(tag, DocumentValue.ValueKind.String, path + ".$type"), declaration, path);
            }
            RequireBusinessType(type, context, path);
            var members = Fields(type); Keys(value, members.Select(f => f.Name).Concat(tagged ? new[] { "$type" } : Array.Empty<string>()), path);
            var memberPlans = members.Select(f => Prepare(value.Members[f.Name], f.FieldType, context, path + "." + f.Name)).ToArray();
            return () =>
            {
                object result;
                try { result = Activator.CreateInstance(type, true); }
                catch { throw Error(path, "trusted constructor failed for " + DocumentFormatContext.Identity(type)); }
                for (var i = 0; i < members.Length; i++) members[i].SetValue(result, memberPlans[i]());
                return result;
            };
        }
        internal sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
