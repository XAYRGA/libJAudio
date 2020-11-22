using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Be.IO;
using System.IO;

namespace libJAudio
{
    public class JEnvelope : JBaseObject
    {
        public static JEnvelopeVector zeroVector = new JEnvelopeVector { mode = JEnvelopeVectorMode.Linear, delay = 0, value = 0 };
        public JEnvelopeVector[] vectorList;

        /*
        JAIV1 Oscillator Vector Structure 
            These are weird. so i'll just do it like this
            short mode
            short time
            short value

            when you read anything over 8 for the mode, read the last two shorts then stop reading -- so the last value in the array would be

            0x000F, 0x0000, 0x0000
        */
        public static JEnvelope readEnvelopeV1(BeBinaryReader binStream, int Base)
        {
            var ret = new JEnvelope();
            ret.mBase = Base;
            ret.mOffset = (int)binStream.BaseStream.Position;
            var len = 0;
            // This function cheats a little bit :) 
            // We dive in not knowing the length of the table -- the table is stopped whenever one of the MODE bytes is more than 0xB. 
            var seekBase = binStream.BaseStream.Position;
            for (int i = 0; i < 10; i++)
            {
                var mode = binStream.ReadInt16(); // reads the first 2 bytes of the table
                len++; // The reason we do this, is because we still need to read the loop flag, or stop flag.
                if (mode < 0xB) // This determines the mode, the mode will always be less than 0xB -- unless its telling the table to end. 
                {
                    // If it is, then we definitely want to read this entry, so we increment the counter
                    binStream.ReadInt32(); // Then skip the actual entry data
                }
                else // The value was over 10 
                {
                    break;  // So we need to stop the loop
                }
            }
            binStream.BaseStream.Position = seekBase;  // After we have an idea how big the table is -- we want to seek back to the beginning of it.
            JEnvelopeVector[] OscVecs = new JEnvelopeVector[len]; // And create an array the size of our length.

            for (int i = 0; i < len; i++) // we read - 1 because we don't want to read the end value yet
            {
                var vector = new JEnvelopeVector
                {
                    mode = (JEnvelopeVectorMode)binStream.ReadInt16(), // Read the values of each into their places
                    delay = binStream.ReadInt16(), // read time 
                    value = binStream.ReadInt16() // read value
                };
                OscVecs[i] = vector;
            }
            // Setting up references. 

            for (int idx = 0; idx < OscVecs.Length - 1; idx++)
            {
                OscVecs[idx].next = OscVecs[idx + 1]; // current vector objects next is the one after it.
            }
      
            ret.vectorList = OscVecs;
            return ret; // finally, return. 
        }

        /*
         * SAME AS JAIV1
            JAIV2 Oscillator Vector Structure 
            These are weird. so i'll just do it like this
            short mode
            short time
            short value

        when you read anything over 8 for the mode, read the last two shorts then stop reading -- so the last value in the array would be

        0x000F, 0x0000, 0x0000
    */
        public static JEnvelope readEnvelopeV2(BeBinaryReader binStream, int Base)
        {

            var ret = new JEnvelope();
            ret.mBase = Base;
            ret.mOffset = (int)binStream.BaseStream.Position;
            var len = 0;
            // This function cheats a little bit :) 
            // We dive in not knowing the length of the table -- the table is stopped whenever one of the MODE bytes is more than 0xB. 
            var seekBase = binStream.BaseStream.Position;
            for (int i = 0; i < 10; i++)
            {
                var mode = binStream.ReadInt16(); // reads the first 2 bytes of the table
                len++; // The reason we do this, is because we still need to read the loop flag, or stop flag.
                if (mode < 0xB) // This determines the mode, the mode will always be less than 0xB -- unless its telling the table to end. 
                {
                    // If it is, then we definitely want to read this entry, so we increment the counter
                    binStream.ReadInt32(); // Then skip the actual entry data
                }
                else // The value was over 10 
                {
                    break;  // So we need to stop the loop
                }
            }
            binStream.BaseStream.Position = seekBase;  // After we have an idea how big the table is -- we want to seek back to the beginning of it.
            JEnvelopeVector[] OscVecs = new JEnvelopeVector[len]; // And create an array the size of our length.

            for (int i = 0; i < len; i++) // we read - 1 because we don't want to read the end value yet
            {
                var vector = new JEnvelopeVector
                {
                    mode = (JEnvelopeVectorMode)binStream.ReadInt16(), // Read the values of each into their places
                    delay = binStream.ReadInt16(), // read time 
                    value = binStream.ReadInt16() // read value
                };
                OscVecs[i] = vector;
            } // Go down below for the last vector, after sorting


            // Setting up references. 
            for (int idx = 0; idx < OscVecs.Length - 1; idx++)
            {
                OscVecs[idx].next = OscVecs[idx + 1]; // current vector objects next is the one after it.
            }
       
            ret.vectorList = OscVecs;
            return ret; // finally, return. 
        }

    }

    public class JEnvelopeVector : JBaseObject
    {
        public JEnvelopeVectorMode mode;
        public short delay;
        public short value;
        public JEnvelopeVector next;
    }

    public enum JEnvelopeVectorMode
    {
        Linear = 0,
        Square = 1,
        SquareRoot = 2,
        SampleCell = 3,

        Loop = 13,
        Hold = 14,
        Stop = 15,
    }
}
