using System;
using System.IO;
using FullSerializer;
using UnityEngine;

namespace NBC.ActionEditor
{
    public class Json
    {
        private sealed class FloatConverter : fsConverter
        {
            public override bool CanProcess(Type type) => type == typeof(float);
            public override bool RequestCycleSupport(Type storageType) => false;
            public override bool RequestInheritanceSupport(Type storageType) => false;
            public override fsResult TrySerialize(object instance, out fsData serialized, Type storageType)
            {
                // The bundled primitive converter rounds Single through Decimal, losing authored bits.
                serialized = new fsData((double)(float)instance);
                return fsResult.Success;
            }
            public override fsResult TryDeserialize(fsData data, ref object instance, Type storageType)
            {
                if (data.IsDouble) instance = (float)data.AsDouble;
                else if (data.IsInt64) instance = (float)data.AsInt64;
                else return fsResult.Fail("Expected a JSON number for Single.");
                return fsResult.Success;
            }
        }

        private static fsSerializer CreateSerializer()
        {
            var serializer = new fsSerializer();
            serializer.AddConverter(new FloatConverter());
            return serializer;
        }

        public static string Serialize(object value, bool isCompressed = false)
        {
            try
            {
                CreateSerializer().TrySerialize(value, out var data).AssertSuccessWithoutWarnings();
                if (isCompressed)
                {
                    return fsJsonPrinter.CompressedJson(data);
                }

                return fsJsonPrinter.PrettyJson(data);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("ActionEditor JSON serialization failed; no data was saved.", e);
            }
        }

        public static object Deserialize(Type type, string serializedState)
        {
            try
            {
                fsData data = fsJsonParser.Parse(serializedState);
                object deserialized = null;
                var ser = CreateSerializer();
                ser.TryDeserialize(data, type, ref deserialized).AssertSuccessWithoutWarnings();

                return deserialized;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return null;
            }
        }
    }
}