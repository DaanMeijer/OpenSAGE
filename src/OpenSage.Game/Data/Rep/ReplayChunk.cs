﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using OpenSage.Data.Utilities.Extensions;
using OpenSage.FileFormats;
using OpenSage.Logic.Orders;

namespace OpenSage.Data.Rep
{
    [DebuggerDisplay("[{Header.Timecode}]: {Order.OrderType} ({Order.Arguments.Count})")]
    public sealed class ReplayChunk
    {
        public ReplayChunkHeader Header { get; private set; }
        public Order Order { get; private set; }

        public override string ToString()
        {
            var args = new StringBuilder();
            foreach(var argument in Order.Arguments)
            {
                if(args.Length != 0)
                {
                    args.Append(",");
                }
                args.Append(argument);
            }

            return $"[{Header.Timecode}]: {Order.OrderType} ({args.ToString()})";
            return base.ToString();
        }

        internal static ReplayChunk Parse(BinaryReader reader)
        {
            var oldPos = reader.BaseStream.Position;
            var result = new ReplayChunk
            {
                Header = ReplayChunkHeader.Parse(reader)
            };

            var numUniqueArgumentTypes = reader.ReadByte();

            // Pairs of {argument type, count}.
            var argumentCounts = new (OrderArgumentType argumentType, byte count)[numUniqueArgumentTypes];
            for (var i = 0; i < numUniqueArgumentTypes; i++)
            {
                argumentCounts[i] = (reader.ReadByteAsEnum<OrderArgumentType>(), reader.ReadByte());
            }

            var order = new Order((int) result.Header.Number, result.Header.OrderType);
            result.Order = order;

            for (var i = 0; i < numUniqueArgumentTypes; i++)
            {
                ref var argumentCount = ref argumentCounts[i];
                var argumentType = argumentCount.argumentType;

                for (var j = 0; j < argumentCount.count; j++)
                {
                    switch (argumentType)
                    {
                        case OrderArgumentType.Integer:
                            order.AddIntegerArgument(reader.ReadInt32());
                            break;

                        case OrderArgumentType.Float:
                            order.AddFloatArgument(reader.ReadSingle());
                            break;

                        case OrderArgumentType.Boolean:
                            order.AddBooleanArgument(reader.ReadBooleanChecked());
                            break;

                        case OrderArgumentType.ObjectId:
                            order.AddObjectIdArgument(reader.ReadUInt32());
                            break;

                        case OrderArgumentType.Position:
                            order.AddPositionArgument(reader.ReadVector3());
                            break;

                        case OrderArgumentType.ScreenPosition:
                            order.AddScreenPositionArgument(reader.ReadPoint2D());
                            break;

                        case OrderArgumentType.ScreenRectangle:
                            order.AddScreenRectangleArgument(reader.ReadRectangle());
                            break;


                            
                        case OrderArgumentType.Unknown4:
                            //in order to align bytes in a random replay, we needed to read 4. has to do with DrawBoxSelection
                            order.AddIntegerArgument(reader.ReadInt32());
                            //skip silently
                            break;

                            /*
                        case OrderArgumentType.Unknown10:
                            //seems to be 2 bytes, has to do with OrderType 1091. TODO: check this!
                            order.AddIntegerArgument(reader.ReadInt16());
                            break;
                            */

                        default:
                            throw new InvalidOperationException();
                    }
                }
            }

            return result;
        }
    }
}
