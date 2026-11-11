using System;
using System.Collections.Generic;
using System.Linq;
using NBC.ActionEditor.Model;
using NBC.ActionEditor.Storage;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NBC.ActionEditor.Extensibility
{
    public sealed class EventPropertyContext
    {
        private readonly DocumentSession session;
        private readonly VisualElement host;
        private readonly Action<string> error;
        private readonly string[] ids;
        public EventPropertyContext(DocumentSession session, VisualElement host, Action<string> error)
        { this.session = session; this.host = host; this.error = error; ids = session.Selection.ToArray(); }

        public void Change(string label, Action<TimelineEvent> change)
        {
            try
            {
                using var edit = new DocumentTransaction(session, label);
                DocumentCommands.Change(edit, ids, change); edit.Commit();
            }
            catch (Exception exception) { session.NotifyChanged(); error?.Invoke(exception.Message); }
        }

        private void Field<T>(BaseField<T> field, Func<TimelineEvent, T> get, Action<TimelineEvent, T> set)
        {
            var values = ids.Select(id => get(DocumentCommands.Event(session.Document, id))).ToArray();
            field.SetValueWithoutNotify(values[0]);
            field.showMixedValue = values.Skip(1).Any(v => !EqualityComparer<T>.Default.Equals(v, values[0]));
            field.name = "event-" + field.label;
            field.RegisterValueChangedCallback(evt => Change(field.label, e => set(e, evt.newValue)));
            host.Add(field);
        }
        public void Text(string label, Func<TimelineEvent, string> get, Action<TimelineEvent, string> set)
        {
            Field(new TextField(label) { isDelayed = true }, get, set);
            host.Add(new Button(() => Change(label, e => set(e, null))) { text = "Set " + label + " null", name = "null-" + label });
        }
        public void Number(string label, Func<TimelineEvent, double> get, Action<TimelineEvent, double> set) => Field(new DoubleField(label) { isDelayed = true }, get, set);
        public void Integer(string label, Func<TimelineEvent, int> get, Action<TimelineEvent, int> set) => Field(new IntegerField(label) { isDelayed = true }, get, set);
        public void Boolean(string label, Func<TimelineEvent, bool> get, Action<TimelineEvent, bool> set) => Field(new Toggle(label), get, set);
        public void Vector(string label, Func<TimelineEvent, Vector3> get, Action<TimelineEvent, Vector3> set) => Field(new Vector3Field(label), get, set);
        public void Resource(string label, Func<TimelineEvent, UnityEngine.Object> get, Action<TimelineEvent, UnityEngine.Object> set)
            => Field(new ObjectField(label) { objectType = typeof(UnityEngine.Object), allowSceneObjects = false }, get, set);

        public void StringList(string label, Func<TimelineEvent, List<string>> get, Action<TimelineEvent, List<string>> set)
        {
            host.Add(new Label(label + (ids.Length > 1 ? " (edit each selected)" : "")));
            var values = get(DocumentCommands.Event(session.Document, ids[0]));
            if (values == null) host.Add(new Label("null"));
            else for (var index = 0; index < values.Count; index++)
            {
                var slot = index;
                var field = new TextField("[" + slot + "]") { value = values[slot], isDelayed = true, name = label + "-" + slot };
                field.RegisterValueChangedCallback(evt => Change(label, e =>
                {
                    var copy = new List<string>(get(e) ?? new List<string>());
                    while (copy.Count <= slot) copy.Add(null);
                    copy[slot] = evt.newValue; set(e, copy);
                }));
                host.Add(field);
                host.Add(new Button(() => Change(label, e =>
                {
                    var copy = new List<string>(get(e) ?? new List<string>());
                    if (slot < copy.Count) copy.RemoveAt(slot); set(e, copy);
                })) { text = "Remove [" + slot + "]" });
            }
            host.Add(new Button(() => Change(label, e =>
            {
                var copy = new List<string>(get(e) ?? new List<string>()) { "" }; set(e, copy);
            })) { text = "+ " + label, name = "add-" + label });
            host.Add(new Button(() => Change(label, e => set(e, null))) { text = "Set " + label + " null", name = "null-" + label });
        }
    }
}
