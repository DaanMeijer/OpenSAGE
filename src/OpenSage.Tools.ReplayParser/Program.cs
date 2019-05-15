using System;
using System.IO;
using System.Text;
using OpenSage.Data;
using OpenSage.Data.Rep;

namespace OpenSage.Tools.ReplayParser
{
    class Program
    {
        static void Main(string[] args)
        {
            
            var reader = new BinaryReader(File.Open(args[0], FileMode.Open), Encoding.Unicode, true);
            var replayFile = ReplayFile.FromBinaryReader(reader);

            foreach(var chunk in replayFile.Chunks)
            {
                if(chunk.Header.OrderType == Logic.Orders.OrderType.SetCameraPosition)
                {
                    continue;
                }
                Console.WriteLine(chunk.ToString());
            }
        
        }
    }
}
