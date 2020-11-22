using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Be.IO;
using System.IO;

namespace libJAudio
{
    public class JIBank : JBaseObject
    {
        public int id;
        public int targetWSYS;
        public JInstrument[] Instruments;

        #region constants 
        // Bank V2
        // WARNING!
        // The instrument loading code is going to be moved in the future from here. I cannot move it currently due to limitations in how i structured a lot of the code. 

        public const int SENS = 0x53454E53; // Sensor effect
        public const int RAND = 0x52414E44; // Random Effect
        public const int OSCT = 0x4F534354; // OSCillator Table
        public const int Osci = 0x4F736369; // Oscillator
        public const int Inst = 0x496E7374; // Instrument
        public const int ENVT = 0x454E5654; // ENVelope Table
        public const int PMAP = 0x504D4150;
        public const int LIST = 0x4C495354;
        public const int Pmap = 0x506D6170;
        public const int Perc = 0x50657263; // Percussion 
        // Bank V1

        public const int BANK = 0x42414E4B;
        public const int PER2 = 0x50455232;

        // Shared 
        public const int INST = 0x494E5354;
        public const int PERC = 0x50455243;
        public const int IBNK = 0x49424e4b;
        #endregion

        #region BankV1 Region

        /*
            JAIV1 IBNK structure
            0x00 int32 0x49424e4b 'IBNK'
            0x04 int32 Section Size
            0x08 int32 Global Bank ID
            0x0C int32 IBankFlags 
            0x10 byte[0x14] padding; 
            0x24 TYPE BANK
        */

        public static JIBank readStreamV1(BeBinaryReader binStream)
        {
            var RetIBNK = new JIBank();
            var Base = (int)binStream.BaseStream.Position;
            RetIBNK.mOffset = (int)binStream.BaseStream.Position;
            RetIBNK.mBase = Base;
            long anchor = 0; // Return / Seekback anchor
            //binStream.BaseStream.Seek(-4, SeekOrigin.Current);
            if (binStream.ReadInt32() != IBNK) // Check if first 4 bytes are IBNK
                throw new InvalidDataException("Data is not an IBNK");
            var SectionSize = binStream.ReadUInt32(); // Read IBNK Size
            var IBankID = binStream.ReadInt32(); // Read the global IBankID

            var IBankFlags = binStream.ReadUInt32(); // Flags?
            binStream.BaseStream.Seek(0x10, SeekOrigin.Current); // Skip Padding
            anchor = binStream.BaseStream.Position;
            var Instruments = loadBankV1(binStream); // Load the instruments

            RetIBNK.id = IBankID; // Store bankID
            RetIBNK.Instruments = Instruments; // Store instruments

            return RetIBNK;
        }

        /* 
        JAIV1 BANK structure 
        0x00 int32 0x42414E4B 'BANK';
        0x04 int32[0xF0] InstrumentPointers
        ---- NOTE: If the instrument pointer is 0, then the instrument for that index is NULL!
        */
        private static JInstrument[] loadBankV1(BeBinaryReader binStream)
        {
            var Base = (int)binStream.BaseStream.Position;
            if (binStream.ReadUInt32() != BANK) // Check if first 4 bytes are BANK
                throw new InvalidDataException("Data is not a BANK");
            var InstrumentPoiners = new int[0xF0]; // Table of pointers for the instruments;
            var Instruments = new JInstrument[0xF0];
            InstrumentPoiners = Helpers.readInt32Array(binStream, 0xF0); //  Read instrument pointers.
            for (int i = 0; i < 0xF0; i++)
            {
                binStream.BaseStream.Position = InstrumentPoiners[i] + Base; // Seek to pointer position
                var type = binStream.ReadInt32(); // Read type
                binStream.BaseStream.Seek(-4, SeekOrigin.Current); // Seek back 4 bytes to undo header read. 

                switch (type)
                {
                    case INST:
                        Instruments[i] = JInstrument.loadInstrumentV1(binStream, Base); // Load instrument
                        break;
                    case PER2:
                        Instruments[i] = JInstrument.readPercussionInstrumentV1(binStream, Base); // Load percussion
                        break;
                    default:
                        // no action, we don't know what it is and it won't misalign.
                        break;
                }
            }
            return Instruments;
        }



        /* 
            ** CHUNKS DO NOT HAVE __ANY__ OFFSET POINTERS, LOCATION IS COMPLETELY VARIABLE ** 
            Chunks must be aligned multiple of 4 bytes of one another.
            JAIV2 IBNK Structure 
            ??? ENVT - Envelope Table 
            ??? OSCT - Oscillator Table 
            ??? RAND - Random Effects Table
            ??? SENS - Sensor Effect Table
            ??? INST - Instrument Table 
            ??? PMAP - Percussion Map
            ??? LIST - Instrument List 
            */
        public static JIBank readStreamV2(BeBinaryReader binStream, int Base)
        {
            var RetIBNK = new JIBank();
            if (binStream.ReadInt32() != IBNK)
                throw new InvalidDataException("Section doesn't have an IBNK header");
            var Boundaries = binStream.ReadInt32() + 8; // total length of our section, the data of the section starts at +8, so we need to account for that, too.
            RetIBNK.id = binStream.ReadInt32(); // Forgot this. Ibank ID. Important.
            var OscTableOffset = findChunk(binStream, OSCT, false, Base, Boundaries); // Load oscillator table chunk
            var EnvTableOffset = findChunk(binStream, ENVT, false, Base, Boundaries); // Load envelope table chunk
            var RanTableOffset = findChunk(binStream, RAND, false, Base, Boundaries); // Load random effect table chunk
            var SenTableOffset = findChunk(binStream, SENS, false, Base, Boundaries); // Load sensor table chunk
            var ListTableOffset = findChunk(binStream, LIST, false, Base, Boundaries); // load the istrument list
            var PmapTableOffset = findChunk(binStream, PMAP, false, Base, Boundaries);  // Percussion mapping lookup table

            binStream.BaseStream.Position = OscTableOffset + Base; // Seek to the position of the oscillator table
            JInstrument.loadBankOscTableV2(binStream, Base, EnvTableOffset); // Load oscillator table, also handles the ENVT!!
            binStream.BaseStream.Position = ListTableOffset + Base; // Seek to the instrument list base
            var instruments = JInstrument.loadInstrumentListV2(binStream, Base); // Load it.
            RetIBNK.Instruments = instruments;
            return RetIBNK;
        }




        // [!] [!] [!] [!] [!] [!] [!] [!] [!] [!] [!] [!] [!] [!] //
        // [!]  THIS FUNCTION DESTROYS YOUR CURRENT POSITION   [!] //
        // [!] Remember to anchor before calling it or trouble [!] //
        // [!] [!] [!] [!] [!] [!] [!] [!] [!] [!] [!] [!] [!] [!] //
        private static int findChunk(BeBinaryReader read, int chunkID, bool immediate, int iBase, int Boundaries)
        {
            if (!immediate) // Indicating we need to search the entire bank (Default)
            {
                read.BaseStream.Position = iBase; // Seek back to IBNK, since i can't follow my own warnings. 
            }
            while (true)
            {
                var pos = (int)read.BaseStream.Position - iBase; // Store the position as an int, relative to ibase. 
                var i = read.ReadInt32(); // Read 4 bytes, since our chunkid is an int32
                if (i == chunkID) // Check to see if the chunk is what we're looking for
                {
                    Console.WriteLine("Found section {0:X}", chunkID);
                    return pos; // Return position relative to the base. 
                }
                else if (pos > (Boundaries)) // we exceedded boundaries
                {
                    Console.WriteLine("Failed to find section", chunkID);
                    return 0;
                }
            }
        }
        #endregion
    }







}
