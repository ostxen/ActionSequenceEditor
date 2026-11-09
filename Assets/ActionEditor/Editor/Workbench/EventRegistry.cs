using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Model;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor.Extensibility
{
    // Preview resources belong to the caller, which must dispose them on cancel/switch/close.
    public interface IEventPreview : IDisposable { void Evaluate(double time); }
    public interface IEventExporter { byte[] Export(TimelineEvent definition); }
    public interface IEventRegistration { IEnumerable<EventDescriptor> Describe(); }

    public sealed class EventDescriptor
    {
        public string TypeId { get; }
        public Type DefinitionType { get; }
        public string Owner { get; }
        public Func<TimelineEvent> Factory { get; }
        public Func<TimelineEvent, string> Summary { get; }
        public Action<EventPropertyContext> Properties { get; }
        public Func<TimelineEvent, IEnumerable<string>> Validate { get; }
        public Func<TimelineEvent, IEventPreview> Preview { get; }
        public Func<IEventExporter> Exporter { get; }

        public EventDescriptor(string typeId, Type definitionType, string owner, Func<TimelineEvent> factory,
            Func<TimelineEvent, string> summary, Action<EventPropertyContext> properties,
            Func<TimelineEvent, IEnumerable<string>> validate = null,
            Func<TimelineEvent, IEventPreview> preview = null, Func<IEventExporter> exporter = null)
        {
            TypeId = typeId; DefinitionType = definitionType; Owner = owner; Factory = factory;
            Summary = summary; Properties = properties; Validate = validate; Preview = preview; Exporter = exporter;
        }
    }

    public sealed class EventRegistry
    {
        private readonly List<EventDescriptor> descriptors = new();
        public IReadOnlyList<EventDescriptor> Descriptors => descriptors.AsReadOnly();

        public static EventRegistry CreateDefault()
        {
            var registry = new EventRegistry();
            // Gather all providers before publishing, so discovery cannot silently replace an owner.
            var candidates = new List<EventDescriptor>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IEventRegistration>().Where(t => !t.IsAbstract && !t.IsInterface)
                .OrderBy(t => t.AssemblyQualifiedName, StringComparer.Ordinal))
                candidates.AddRange(((IEventRegistration)Activator.CreateInstance(type)).Describe());
            registry.Register(candidates);
            return registry;
        }

        public void Register(IEnumerable<EventDescriptor> candidates)
        {
            var pending = candidates.ToArray();
            var all = new List<EventDescriptor>(descriptors);
            foreach (var descriptor in pending)
            {
                if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.TypeId) || string.IsNullOrWhiteSpace(descriptor.Owner)
                    || descriptor.DefinitionType == null || descriptor.DefinitionType.IsAbstract
                    || !typeof(TimelineEvent).IsAssignableFrom(descriptor.DefinitionType)
                    || descriptor.Factory == null || descriptor.Summary == null || descriptor.Properties == null)
                    throw new InvalidOperationException("Incomplete event registration.");
                var conflict = all.Find(d => d.TypeId == descriptor.TypeId || d.DefinitionType == descriptor.DefinitionType);
                if (conflict != null) throw new InvalidOperationException($"Registration conflict: {conflict.Owner} ({conflict.TypeId}) vs {descriptor.Owner} ({descriptor.TypeId}).");
                var first = CreateChecked(descriptor);
                var second = CreateChecked(descriptor);
                if (ReferenceEquals(first, second) || first.Id == second.Id)
                    throw new InvalidOperationException("Factory must return independent definitions and IDs: " + descriptor.Owner);
                all.Add(descriptor);
            }
            descriptors.AddRange(pending);
        }

        public EventDescriptor Find(TimelineEvent item) => item == null ? null : descriptors.Find(d => d.TypeId == item.TypeId && d.DefinitionType == item.GetType());
        public TimelineEvent Create(string typeId)
        {
            var descriptor = descriptors.Find(d => d.TypeId == typeId) ?? throw new InvalidOperationException("Unknown event type: " + typeId);
            return CreateChecked(descriptor);
        }

        private static TimelineEvent CreateChecked(EventDescriptor descriptor)
        {
            var value = descriptor.Factory();
            if (value == null || value.GetType() != descriptor.DefinitionType || value.TypeId != descriptor.TypeId || !StableId.IsValid(value.Id)
                || !TimelineTime.IsFinite(value.Start) || value.Start < 0 || !TimelineTime.IsFinite(value.Duration)
                || (value.Kind == EventKind.Point ? value.Duration != 0 : value.Duration <= 0))
                throw new InvalidOperationException("Invalid event factory: " + descriptor.Owner);
            return value;
        }

        public List<DocumentIssue> Inspect(ActionDocument document)
        {
            var issues = DocumentValidation.Inspect(document);
            if (document == null) return issues;
            if (UnityEditor.SerializationUtility.HasManagedReferencesWithMissingTypes(document))
                issues.Add(new DocumentIssue(document.Id, "Missing managed-reference types; saving is disabled."));
            // Inspect serialized references without replacing missing objects with null.
            using (var serialized = new SerializedObject(document))
            {
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference
                        && ((property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                            || (property.objectReferenceValue != null && !EditorUtility.IsPersistent(property.objectReferenceValue))))
                        issues.Add(new DocumentIssue(document.Id, "Invalid persistent resource reference: " + property.propertyPath));
            }
            foreach (var group in document.Groups ?? new List<DocumentGroup>())
                foreach (var track in group?.Tracks ?? new List<DocumentTrack>())
                    foreach (var item in track?.Events ?? new List<TimelineEvent>())
                    {
                        if (item == null) continue;
                        var descriptor = Find(item);
                        if (descriptor == null) { issues.Add(new DocumentIssue(item.Id, "Unregistered event type: " + item.TypeId)); continue; }
                        try
                        {
                            foreach (var message in descriptor.Validate?.Invoke(item) ?? Enumerable.Empty<string>())
                                issues.Add(new DocumentIssue(item.Id, message));
                        }
                        catch (Exception exception) { issues.Add(new DocumentIssue(item.Id, "Validator failed: " + exception.Message)); }
                    }
            return issues;
        }

        public void RequireValid(ActionDocument document)
        {
            var issues = Inspect(document);
            if (issues.Count != 0) throw new InvalidOperationException(issues[0].Message);
        }
    }

    public sealed class PlatformEventRegistration : IEventRegistration
    {
        public IEnumerable<EventDescriptor> Describe()
        {
            yield return new EventDescriptor("actioneditor.signal", typeof(SignalEvent), "ActionEditor.Signal", () => new SignalEvent(),
                e => ((SignalEvent)e).Message ?? e.Name, context =>
                {
                    context.Text("Message", e => ((SignalEvent)e).Message, (e, v) => ((SignalEvent)e).Message = v);
                    context.Resource("Resource", e => ((SignalEvent)e).Resource, (e, v) => ((SignalEvent)e).Resource = v);
                    context.StringList("Tags", e => ((SignalEvent)e).Tags, (e, v) => ((SignalEvent)e).Tags = v);
                });
            yield return new EventDescriptor("actioneditor.interval", typeof(IntervalEvent), "ActionEditor.Interval", () => new IntervalEvent { Duration = 1 },
                e => e.Name, context =>
                {
                    context.Number("Value", e => ((IntervalEvent)e).Value, (e, v) => ((IntervalEvent)e).Value = (float)v);
                    context.Vector("Offset", e => ((IntervalEvent)e).Offset, (e, v) => ((IntervalEvent)e).Offset = v);
                    context.Resource("Resource", e => ((IntervalEvent)e).Resource, (e, v) => ((IntervalEvent)e).Resource = v);
                }, e => TimelineTime.IsFinite(((IntervalEvent)e).Value) ? Array.Empty<string>() : new[] { "Value must be finite." });
        }
    }
}
