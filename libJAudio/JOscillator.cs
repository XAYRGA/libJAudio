using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Be.IO;
using System.IO;

namespace libJAudio
{
    public class JOscillator : JBaseObject
    {
        public JOscillatorTarget target;
        public float rate;
        public float Width;
        public float Vertex;

        public JEnvelope[] envelopes = new JEnvelope[2];


        /*
         JAIV1 Oscillator Format 
         0x00 - byte mode 
         0x01 - byte[3] unknown
         0x04 - float rate
         0x08 - int32 attackVectorOffset
         0x0C - int32 releaseVectorOffset
         0x10 - float width
         0x14 - float vertex
        */
        public static JOscillator loadOscillatorV1(BeBinaryReader binStream, int Base)
        {
            var Osc = new JOscillator(); // Create new oscillator
            var target = binStream.ReadByte(); // load target -- what is it affecting?
            binStream.BaseStream.Seek(3, SeekOrigin.Current); // read 3 bytes?
            Osc.rate = binStream.ReadSingle(); // Read the rate at which the oscillator progresses -- this will be relative to the number of ticks per beat.
            var attackSustainTableOffset = binStream.ReadInt32(); // Offset of AD table
            var releaseDecayTableOffset = binStream.ReadInt32(); // Offset of SR table
            Osc.Width = binStream.ReadSingle(); // We should load these next, this is the width, ergo the value of the oscillator at 32768. 
            Osc.Vertex = binStream.ReadSingle();  // This is the vertex, the oscillator will always cross this point. 
            // To determine the value of an oscillator, it's Vertex + Width*(value/32768) -- each vector should progress the value, depending on the mode. 
            if (attackSustainTableOffset > 0) // first is AS table
            {
                binStream.BaseStream.Position = attackSustainTableOffset + Base; // Seek to the vector table
                Osc.envelopes[0] = JEnvelope.readEnvelopeV1(binStream, Base); // Load the table
            }
            if (releaseDecayTableOffset > 0) // Next is RD table
            {
                binStream.BaseStream.Position = releaseDecayTableOffset + Base; // Seek to the vector and load it
                Osc.envelopes[1] = JEnvelope.readEnvelopeV1(binStream, Base); // loadddd
            }
            Osc.target = (JOscillatorTarget)target;
            return Osc;
        }
    }

    public enum JOscillatorTarget
    {
        Volume = 1,
        Pitch = 2,
        Pan = 3,
        FX = 4,
        Dolby = 5,
        VibratoStrength = 6,
        TremoloStrength = 7
    }
}
