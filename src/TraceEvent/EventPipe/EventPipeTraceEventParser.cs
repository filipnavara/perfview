using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Diagnostics.Tracing.Parsers;
using FastSerialization;


#pragma warning disable 1591        // disable warnings on XML comments not being present

namespace Microsoft.Diagnostics.Tracing.EventPipe
{
    public sealed class EventPipeTraceEventParser : ExternalTraceEventParser
    {
        public EventPipeTraceEventParser(TraceEventSource source, bool dontRegister = false)
            : base(source, dontRegister)
        {
        }

        internal bool AddTemplate(PinnedStreamReader reader, EventPipeEventMetaData eventMetadata)
        {
            var key = Tuple.Create(eventMetadata.ProviderId, (TraceEventID)eventMetadata.EventId);
            bool createTemplate = !_templates.ContainsKey(key);
            if (createTemplate)
            {
                var template = ReadEventParametersAndBuildTemplate(reader, eventMetadata);
                _templates.Add(key, template);
                OnNewEventDefintion(template, mayHaveExistedBefore: false);
            }

            return createTemplate;
        }

        #region Override ExternalTraceEventParser
        internal override DynamicTraceEventData TryLookup(TraceEvent unknownEvent)
        {
            if (unknownEvent.IsClassicProvider) return null;

            DynamicTraceEventData template;
            return _templates.TryGetValue(Tuple.Create(unknownEvent.ProviderGuid, unknownEvent.ID), out template) ? template: null;
        }
        #endregion

#region Private

        private DynamicTraceEventData ReadEventParametersAndBuildTemplate(PinnedStreamReader reader, EventPipeEventMetaData metaData)
        {
            int opcode;
            string opcodeName;

            EventPipeTraceEventParser.GetOpcodeFromEventName(metaData.EventName, out opcode, out opcodeName);

            DynamicTraceEventData.PayloadFetchClassInfo classInfo = null;
            DynamicTraceEventData template = new DynamicTraceEventData(null, metaData.EventId, 0, metaData.EventName, Guid.Empty, opcode, opcodeName, metaData.ProviderId, metaData.ProviderName);

            // If the metadata contains no parameter metadata, don't attempt to read it.
            if (!metaData.ContainsParameterMetadata)
            {
                template.payloadNames = new string[0];
                template.payloadFetches = new DynamicTraceEventData.PayloadFetch[0];

                return template;
            }

            // Read the count of event payload fields.
            int fieldCount = reader.ReadInt32();
            Debug.Assert(0 <= fieldCount && fieldCount < 0x4000);

            if (fieldCount > 0)
            {
                // Recursively parse the metadata, building up a list of payload names and payload field fetch objects.
                classInfo = ParseFields(reader, fieldCount);
            }
            else
            {
                classInfo = new DynamicTraceEventData.PayloadFetchClassInfo()
                {
                    FieldNames = new string[0],
                    FieldFetches = new DynamicTraceEventData.PayloadFetch[0]
                };
            }

            template.payloadNames = classInfo.FieldNames;
            template.payloadFetches = classInfo.FieldFetches;

            return template;
        }

        private DynamicTraceEventData.PayloadFetchClassInfo ParseFields(PinnedStreamReader reader, int numFields)
        {
            string[] fieldNames = new string[numFields];
            DynamicTraceEventData.PayloadFetch[] fieldFetches = new DynamicTraceEventData.PayloadFetch[numFields];

            ushort offset = 0;
            for (int fieldIndex = 0; fieldIndex < numFields; fieldIndex++)
            {
                DynamicTraceEventData.PayloadFetch payloadFetch = new DynamicTraceEventData.PayloadFetch();

                // Read the TypeCode for the current field.
                TypeCode typeCode = (TypeCode)reader.ReadInt32();

                // Fill out the payload fetch object based on the TypeCode.
                switch (typeCode)
                {
                    case TypeCode.Boolean:
                        {
                            payloadFetch.Type = typeof(bool);
                            payloadFetch.Size = 4; // We follow windows conventions and use 4 bytes for bool.
                            payloadFetch.Offset = offset;
                            break;
                        }
                    case TypeCode.Char:
                        {
                            payloadFetch.Type = typeof(char);
                            payloadFetch.Size = sizeof(char);
                            payloadFetch.Offset = offset;
                            break;
                        }
                    case TypeCode.SByte:
                        {
                            payloadFetch.Type = typeof(SByte);
                            payloadFetch.Size = sizeof(SByte);
                            payloadFetch.Offset = offset;
                            break;
                        }
                    case TypeCode.Byte:
                        {
                            payloadFetch.Type = typeof(byte);
                            payloadFetch.Size = sizeof(byte);
                            payloadFetch.Offset = offset;
                            break;
                        }
                    case TypeCode.Int16:
                        {
                            payloadFetch.Type = typeof(Int16);
                            payloadFetch.Size = sizeof(Int16);
                            payloadFetch.Offset = offset;
                            break;
                        }
                    case TypeCode.UInt16:
                        {
                            payloadFetch.Type = typeof(UInt16);
                            payloadFetch.Size = sizeof(UInt16);
                            payloadFetch.Offset = offset;
                            break;
                        }
                    case TypeCode.Int32:
                        {
                            payloadFetch.Type = typeof(Int32);
                            payloadFetch.Size = sizeof(Int32);
                            payloadFetch.Offset = offset;
                            break;
                        }
                    case TypeCode.UInt32:
                        {
                            payloadFetch.Type = typeof(UInt32);
                            payloadFetch.Size = sizeof(UInt32);
                            payloadFetch.Offset = offset;
                            break;
                        }
                    case TypeCode.Int64:
                        {
                            payloadFetch.Type = typeof(Int64);
                            payloadFetch.Size = sizeof(Int64);
                            payloadFetch.Offset = offset;
                            break;
                        }
                    case TypeCode.UInt64:
                        {
                            payloadFetch.Type = typeof(UInt64);
                            payloadFetch.Size = sizeof(UInt64);
                            payloadFetch.Offset = offset;
                            break;
                        }
                    case TypeCode.Single:
                        {
                            payloadFetch.Type = typeof(Single);
                            payloadFetch.Size = sizeof(Single);
                            payloadFetch.Offset = offset;
                            break;
                        }
                    case TypeCode.Double:
                        {
                            payloadFetch.Type = typeof(Double);
                            payloadFetch.Size = sizeof(Double);
                            payloadFetch.Offset = offset;
                            break;
                        }
                    case TypeCode.Decimal:
                        {
                            payloadFetch.Type = typeof(Decimal);
                            payloadFetch.Size = sizeof(Decimal);
                            payloadFetch.Offset = offset;
                            break;
                        }
                    case TypeCode.DateTime:
                        {
                            payloadFetch.Type = typeof(DateTime);
                            payloadFetch.Size = 8;
                            payloadFetch.Offset = offset;
                            break;
                        }
                    case EventPipeTraceEventParser.GuidTypeCode:
                        {
                            payloadFetch.Type = typeof(Guid);
                            payloadFetch.Size = 16;
                            payloadFetch.Offset = offset;
                            break;
                        }
                    case TypeCode.String:
                        {
                            payloadFetch.Type = typeof(String);
                            payloadFetch.Size = DynamicTraceEventData.NULL_TERMINATED;
                            payloadFetch.Offset = offset;
                            break;
                        }
                    case TypeCode.Object:
                        {
                            // TypeCode.Object represents an embedded struct.

                            // Read the number of fields in the struct.  Each of these fields could be an embedded struct,
                            // but these embedded structs are still counted as single fields.  They will be expanded when they are handled.
                            int structFieldCount = reader.ReadInt32();
                            DynamicTraceEventData.PayloadFetchClassInfo embeddedStructClassInfo = ParseFields(reader, structFieldCount);
                            if (embeddedStructClassInfo == null)
                            {
                                throw new Exception("Unable to parse metadata for embedded struct.");
                            }
                            payloadFetch = DynamicTraceEventData.PayloadFetch.StructPayloadFetch(offset, embeddedStructClassInfo);
                            break;
                        }
                    default:
                        {
                            throw new NotSupportedException($"{typeCode} is not supported.");
                        }
                }

                // Read the string name of the event payload field.
                fieldNames[fieldIndex] = reader.ReadNullTerminatedUnicodeString();

                // Update the offset into the event for the next payload fetch.
                if (payloadFetch.Size >= DynamicTraceEventData.SPECIAL_SIZES || offset == ushort.MaxValue)
                {
                    offset = ushort.MaxValue;           // Indicate that the offset must be computed at run time.
                }
                else
                {
                    offset += payloadFetch.Size;
                }

                // Save the current payload fetch.
                fieldFetches[fieldIndex] = payloadFetch;
            }

            return new DynamicTraceEventData.PayloadFetchClassInfo()
            {
                FieldNames = fieldNames,
                FieldFetches = fieldFetches
            };
        }

        private static void GetOpcodeFromEventName(string eventName, out int opcode, out string opcodeName)
        {
            opcode = 0;
            opcodeName = null;

            if (eventName != null)
            {
                if (eventName.EndsWith("Start", StringComparison.OrdinalIgnoreCase))
                {
                    opcode = (int)TraceEventOpcode.Start;
                    opcodeName = nameof(TraceEventOpcode.Start);
                }
                else if (eventName.EndsWith("Stop", StringComparison.OrdinalIgnoreCase))
                {
                    opcode = (int)TraceEventOpcode.Stop;
                    opcodeName = nameof(TraceEventOpcode.Stop);
                }
            }
        }

        // Guid is not part of TypeCode (yet), we decided to use 17 to represent it, as it's the "free slot" 
        // see https://github.com/dotnet/coreclr/issues/16105#issuecomment-361749750 for more
        internal const TypeCode GuidTypeCode = (TypeCode)17;

        Dictionary<Tuple<Guid, TraceEventID>, DynamicTraceEventData> _templates = new Dictionary<Tuple<Guid, TraceEventID>, DynamicTraceEventData>();
#endregion
    }
}
