using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Be.IO;

namespace libJAudio
{
    public class JWaveInfo : JBaseObject
    {
        public int id;
        public ushort format;
        public ushort key;
        public double sampleRate;
        public int sampleCount;

        public string wsysFile; 
        public int wsys_start;
        public int wsys_size;

        public bool loop;
        public int loop_start;
        public int loop_end;

        public string fsPath;

        public byte[] pcmData;

        public static JWaveInfo loadWave(BeBinaryReader binStream, int Base)
        {
            var newWave = new JWaveInfo();
            binStream.ReadByte(); // First byte unknown?
            newWave.format = binStream.ReadByte(); // Read wave format, usually 5
            newWave.key = binStream.ReadByte(); // Read the base tuning key
            //Console.WriteLine(newWave.key);
            binStream.ReadByte(); // fourth byte unknown?
            newWave.sampleRate = binStream.ReadSingle(); // Read the samplerate
            newWave.wsys_start = binStream.ReadInt32(); // Read the offset in the AW
            newWave.wsys_size = binStream.ReadInt32(); // Read the length in the AW
            newWave.loop = binStream.ReadUInt32() == UInt32.MaxValue ? true : false; // Check if it loops?
            newWave.loop_start = binStream.ReadInt32(); // Even if looping is disabled, it should still read loops
            newWave.loop_end = binStream.ReadInt32(); // Sample index of loop end
            newWave.sampleCount = binStream.ReadInt32(); // Sample count
            return newWave;
        }

    }
}
