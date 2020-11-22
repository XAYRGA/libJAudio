﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Be.IO;
namespace libJAudio
{
    public class JInstrumentKey : JBaseObject
    {
        public float Volume = 1;
        public float Pitch = 1;
        public int baseKey;
        public JInstrumentKeyVelocity[] Velocities; 
    }


    public class JInstrumentKeyVelocity : JBaseObject
    {
        public int baseVel;
        public float Volume;
        public float Pitch;
        public int wave;
        public int wsysid;
        public int velocity;
    }


    public class JInstrument : JBaseObject
    {
        public int id;
        public float Volume;
        public float Pitch;
        public byte oscillatorCount;
        public JOscillator[] oscillators;     
        public bool IsPercussion; 
        public JInstrumentKey[] Keys;


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


        /* 
            JAIV1 INST Structure 
            0x00 int32 0x494E5354 'INST'
            0x04 int32 0 - unused?
            0x08 float frequencyMultiplier
            0x0C float gainMultiplier
            0x10 int32 oscillator table offset
            0x14 int32 oscillator table count
            0x18 int32 effect table offset 
            0x1C int32 effect table size 
            0x20 int32 sensor object table
            0x24 int32 sensor object table count
            0x28 int32 key_region_count
            0x2C *KeyRegion[key_region_count]

        */

        public static JInstrument loadInstrumentV1(BeBinaryReader binStream, int Base)
        {
            var Inst = new JInstrument();
            if (binStream.ReadUInt32() != JIBank.INST) // Check if first 4 bytes are INST
                throw new InvalidDataException("Data is not an INST");
            binStream.ReadUInt32(); // oh god oh god oh god its null
            Inst.Pitch = binStream.ReadSingle();
            Inst.Volume = binStream.ReadSingle();
            var osc1Offset = binStream.ReadUInt32();
            var osc2Offset = binStream.ReadUInt32();
            binStream.ReadUInt32(); // *NOT IMPLEMENTED* //
            binStream.ReadUInt32(); // *NOT IMPLEMENTED* //
            binStream.ReadUInt32(); // *NOT IMPLEMENTED* //
            binStream.ReadUInt32(); // *NOT IMPLEMENTED* //
            // * trashy furry screaming * //
            int keyregCount = binStream.ReadInt32(); // Read number of key regions
            JInstrumentKey[] keys = new JInstrumentKey[0x81]; // Always go for one more.
            int[] keyRegionPointers = new int[keyregCount];
            keyRegionPointers = Helpers.readInt32Array(binStream, keyregCount);
            var keyLow = 0; // For region spanning. 
            for (int i = 0; i < keyregCount; i++) // Loop through all pointers.
            {
                binStream.BaseStream.Position = keyRegionPointers[i] + Base; // Set position to key pointer pos (relative to base)
                var bkey = readKeyRegionV1(binStream, Base); // Read the key region
                for (int b = 0; b < bkey.baseKey - keyLow; b++)
                {
                    //  They're key regions, so we're going to have a toothy / gappy piano. So we need to span the missing gaps with the previous region config.
                    // This means that if the last key was 4, and the next key was 8 -- whatever parameters 4 had will span keys 4 5 6 and 7. 
                    keys[b + keyLow] = bkey; // span the keys
                    keys[127] = bkey;
                }
                keyLow = bkey.baseKey; // Store our last key 
            }
            Inst.Keys = keys;
            byte oscCount = 0;
            if (osc1Offset > 0)
                oscCount++;
            if (osc2Offset > 0)
                oscCount++;
            Inst.oscillatorCount = oscCount; // Redundant?
            Inst.oscillators = new JOscillator[oscCount]; // new oscillator array
            if (osc1Offset != 0) // if the oscillator isnt null
            {
                binStream.BaseStream.Position = osc1Offset + Base; // seek to it's position
                Inst.oscillators[0] = JOscillator.loadOscillatorV1(binStream, Base); // then load it.
            }
            if (osc2Offset != 0) // if the second oscillator isn't null
            {
                binStream.BaseStream.Position = osc2Offset + Base; // seek to its position
                Inst.oscillators[1] = JOscillator.loadOscillatorV1(binStream, Base); // and load it.
            }
            return Inst;
        }

        /* 
            JAIV1 KeyRegion Structure
            0x00 byte baseKey 
            0x01 byte[0x3] unused;
            0x04 int32 velocityRegionCount
            *VelocityRegion[velocityRegionCount] velocities;
        */
        public static JInstrumentKey readKeyRegionV1(BeBinaryReader binStream, int Base)
        {
            JInstrumentKey newKey = new JInstrumentKey();
            newKey.Velocities = new JInstrumentKeyVelocity[0x81]; // Create region array
            //-------
            newKey.baseKey = binStream.ReadByte(); // Store base key
            binStream.BaseStream.Seek(3, SeekOrigin.Current); ; // Skip 3 bytes
            var velRegCount = binStream.ReadInt32(); // Grab vel region count
            int[] velRegPointers = new int[velRegCount]; // Create Pointer array
            velRegPointers = Helpers.readInt32Array(binStream, velRegCount);
            var velLow = 0;  // Again, these are regions -- see LoadInstrument for this exact code ( a few lines above ) 
            for (int i = 0; i < velRegCount; i++)
            {
                binStream.BaseStream.Position = velRegPointers[i] + Base;
                var breg = readKeyVelocityRegionV1(binStream, Base);  // Read the vel region.
                for (int b = 0; b < breg.baseVel - velLow; b++)
                {
                    //  They're velocity regions, so we're going to have a toothy / gappy piano. So we need to span the missing gaps with the previous region config.
                    // This means that if the last key was 4, and the next key was 8 -- whatever parameters 4 had will span keys 4 5 6 and 7. 
                    newKey.Velocities[b] = breg;
                    newKey.Velocities[127] = breg;
                }
                velLow = breg.baseVel;
            }
            return newKey;
        }

        /* 
            JAIV1 Velocity Region Structure
           0x00 byte baseVelocity;
           0x04 byte[0x03] unused;
           0x07 short wsysID;
           0x09 short waveID;
           0x0D float Volume; 
           0x11 float Pitch;
        */

        public static JInstrumentKeyVelocity readKeyVelocityRegionV1(BeBinaryReader binStream, int Base)
        {
            JInstrumentKeyVelocity newReg = new JInstrumentKeyVelocity();
            newReg.baseVel = binStream.ReadByte();
            binStream.BaseStream.Seek(3, SeekOrigin.Current); ; // Skip 3 bytes.
            newReg.velocity = newReg.baseVel;
            newReg.wsysid = binStream.ReadInt16();
            newReg.wave = binStream.ReadInt16();
            newReg.Volume = binStream.ReadSingle();
            newReg.Pitch = binStream.ReadSingle();
            return newReg;
        }

        /* 
         JAIV1 PER2 Structure 
         0x00 int32 0x50455232 'PER2'
         0x04 byte[0x84] unused; // the actual fuck? Waste of 0x84 perfectly delicious bytes.
         0x8C *PercussionKey[100]      

         PER2 PercussionKey Structure
             float pitch;
             float volume;
             byte[0x8] unused?
             int32 velocityRegionCount
             *VelocityRegion[velocityRegionCount]  velocities

        */

        public static JInstrument readPercussionInstrumentV1(BeBinaryReader binStream, int Base)
        {
            var Inst = new JInstrument();
            if (binStream.ReadUInt32() != JIBank.PER2) // Check if first 4 bytes are PER2
                throw new InvalidDataException("Data is not an PER2");
            Inst.Pitch = 1.0f;
            Inst.Volume = 1.0f;
            Inst.IsPercussion = true;
            binStream.BaseStream.Seek(0x84, SeekOrigin.Current);
            JInstrumentKey[] keys = new JInstrumentKey[100];
            int[] keyPointers = new int[100];
            keyPointers = Helpers.readInt32Array(binStream, 100); // read the pointers.

            for (int i = 0; i < 100; i++) // Loop through all pointers.
            {
                if (keyPointers[i] == 0)
                {
                    continue;
                }
                binStream.BaseStream.Position = keyPointers[i] + Base; // Set position to key pointer pos (relative to base)     
                var newKey = new JInstrumentKey();
                newKey.Pitch = binStream.ReadSingle(); // read the pitch
                newKey.Volume = binStream.ReadSingle(); // read the volume
                binStream.BaseStream.Seek(8, SeekOrigin.Current); // runtime values, skip
                var velRegCount = binStream.ReadInt32(); // read count of regions we have
                newKey.Velocities = new JInstrumentKeyVelocity[0xff]; // 0xFF just in case.
                int[] velRegPointers = Helpers.readInt32Array(binStream, velRegCount);

                var velLow = 0;  // Again, these are regions -- see LoadInstrument for this exact code ( a few lines above ) 
                for (int b = 0; b < velRegCount; b++)
                {
                    binStream.BaseStream.Position = velRegPointers[b] + Base;
                    var breg = readKeyVelocityRegionV1(binStream, Base);  // Read the vel region.
                    for (int c = 0; c < breg.baseVel - velLow; c++)
                    {
                        //  They're velocity regions, so we're going to have a toothy / gappy piano. So we need to span the missing gaps with the previous region config.
                        // This means that if the last key was 4, and the next key was 8 -- whatever parameters 4 had will span keys 4 5 6 and 7. 
                        newKey.Velocities[c] = breg; // store the  region
                        newKey.Velocities[127] = breg;
                    }
                    velLow = breg.baseVel; // store the velocity for spanning
                }
                keys[i] = newKey;
            }
            Inst.Keys = keys;
            return Inst;
        }


        public static JInstrument loadPercussionInstrumentV2(BeBinaryReader binStream, int Base)
        {
            if (binStream.ReadInt32() != Perc)
                throw new InvalidDataException("Perc section started with unexpected data");
            var newPERC = new JInstrument();
            newPERC.IsPercussion = true;
            newPERC.Pitch = 1f;
            newPERC.Volume = 1f;

            var count = binStream.ReadInt32();
            var ptrs = Helpers.readInt32Array(binStream, count);
            var iKeys = new JInstrumentKey[count];
            for (int i = 0; i < count; i++)
            {
                var PmapOffset = ptrs[i];
                if (PmapOffset > 0)
                {
                    var newKey = new JInstrumentKey();
                    newKey.Velocities = new JInstrumentKeyVelocity[0x81];
                    var pmapDataOffs = PmapOffset + Base; // OH LOOK ANOTHER RELATIVE TO BANK BASE.
                    binStream.BaseStream.Position = pmapDataOffs;
                    if (binStream.ReadInt32() != Pmap)
                    {
                        Console.WriteLine("ERROR: Invalid PMAP data {0:X} -- Potential misalignment!", binStream.BaseStream.Position);
                        continue;
                    }

                    newKey.Volume = binStream.ReadInt32();
                    newKey.Pitch = binStream.ReadInt32();
                    //binStream.ReadInt32(); // byte panning 
                    binStream.BaseStream.Seek(8, SeekOrigin.Current); // runtime. 
                    var velRegCount = binStream.ReadInt32();
                    var velLow = 0;
                    for (int b = 0; b < velRegCount; b++)
                    {
                        var breg = readKeyVelRegionV2(binStream, Base);
                        for (int c = 0; c < breg.baseVel - velLow; c++)
                        {
                            //  They're velocity regions, so we're going to have a toothy / gappy piano. So we need to span the missing gaps with the previous region config.
                            // This means that if the last key was 4, and the next key was 8 -- whatever parameters 4 had will span keys 4 5 6 and 7. 
                            newKey.Velocities[c] = breg; // store the  region
                            newKey.Velocities[127] = breg;
                        }
                        velLow = breg.baseVel; // store the velocity for spanning
                    }
                    iKeys[i] = newKey;
                }
            }
            newPERC.Keys = iKeys;
            newPERC.oscillatorCount = 0;
            return newPERC;
        }


        /* 
         JAIV2 Velocity Region Structure
           0x00 byte baseVelocity;
           0x04 byte[0x03] unused;
           0x07 short wsysID;
           0x09 short waveID;
           0x0D float Volume; 
           0x11 float Pitch;
       */

        public static JInstrumentKeyVelocity readKeyVelRegionV2(BeBinaryReader binStream, int Base)
        {
            JInstrumentKeyVelocity newReg = new JInstrumentKeyVelocity();
            newReg.baseVel = binStream.ReadByte();
            binStream.BaseStream.Seek(3, SeekOrigin.Current); ; // Skip 3 bytes.
            newReg.velocity = newReg.baseVel;
            newReg.wsysid = binStream.ReadInt16();
            newReg.wave = binStream.ReadInt16();
            newReg.Volume = binStream.ReadSingle();
            newReg.Pitch = binStream.ReadSingle();

            return newReg;
        }




        private static JOscillator[] bankOscillators;


        /* 
            NOTE ABOUT "OSCILLATORS"
            The envelope table must be loaded before the oscillator!
        */




        /*
            JAIV2 LIST STRUCTURE
            0x00 - int32 0x4C495354 'LIST';
            0x04 - int32 length 
            0x08 - int32 count 
            0x0c - int32[count] instrumentPointers (RELATIVE TO IBANK 0x00)
        */

        public static JInstrument[] loadInstrumentListV2(BeBinaryReader binStream, int Base)
        {
            JInstrument[] instruments = new JInstrument[0xF0]; // JSystem doesn't have more than 0xF0 instruments in each bank
            if (binStream.ReadInt32() != LIST) // Verify we're loading the right section
                throw new InvalidDataException("LIST data section started with unexpected data " + binStream.BaseStream.Position); // Throw if it's not the right data
            binStream.ReadInt32(); // Section Length // Section lenght doesn't matter, but we have to read it to keep alignment.
            var count = binStream.ReadInt32(); // Count of entries in the section (Including nulls.)
            // why are these FUCKS relative whenever literally nothing else in the file is ? //
            var pointers = Helpers.readInt32Array(binStream, count); // This will be an in32[] of pointers

            for (int i = 0; i < count; i++)
            {
                if (pointers[i] < 1) // Instrument is empty.
                    continue; // Instrument is empty. Skip this iteration
                binStream.BaseStream.Position = Base + pointers[i]; // FUCK THIS. Err I mean. Seek to the position of the instrument index + the base of the bank.
                var IID = binStream.ReadInt32();  // read the identity at the base of each section
                binStream.BaseStream.Seek(-4, SeekOrigin.Current); // Seek back identity (We read 4 bytes for the ID)

                switch (IID) // Switch ID
                {
                    case Inst: // It's a regular instrument 
                        instruments[i] = loadInstrumentV2(binStream, Base); // Ask it to load (We're already just behind the Inst)
                        break;
                    case Perc: // Percussion Instrument 
                        instruments[i] = JInstrument.loadPercussionInstrumentV2(binStream, Base);
                        break;
                    default:
                        Console.WriteLine("unknown inst index {0:X}", binStream.BaseStream.Position);
                        break;
                }
            }
            return instruments;
        }

        /* JAIV2 Instrument Structure 
              0x00 int32 = 0x496E7374 'Inst'
              0x04 int32 oscillatorCount
              0x08 int32[oscillatorCount] oscillatorIndicies
              ???? int32 0 
              ???? int32 keyRegionCount 
              ???? keyRegion[keyRegionCount]
              ???? float gain
              ???? float freqmultiplier
             
        */
        public static JInstrument loadInstrumentV2(BeBinaryReader binStream, int Base)
        {
            var newInst = new JInstrument();
            newInst.IsPercussion = false; // Instrument isn't percussion
            // This is wrong, they come at the end of the instrument
            //newInst.Pitch = 1; // So these kinds of instruments don't initialize with a pitch value, which is strange. 
            //newInst.Volume = 1; // I guess they figured that it was redundant since they're already doing it in 3 other places. 
            if (binStream.ReadInt32() != Inst)
                throw new InvalidDataException("Inst section started with unexpected data");
            var osciCount = binStream.ReadInt32(); // Read the count of the oscillators. 
            newInst.oscillatorCount = (byte)osciCount; // Hope no instrument never ever ever has > 255 oscillators lol.
            newInst.oscillators = new JOscillator[osciCount]; // Initialize the instrument with the proper amount of oscillaotrs
            for (int i = 0; i < osciCount; i++) // Loop through and read each oscillator.
            {
                var osciIndex = binStream.ReadInt32(); // Each oscillator is stored as a 32 bit index.
                newInst.oscillators[i] = bankOscillators[osciIndex]; // We loaded the oscillators already, I hope.  So this will grab them by their index.                
            }
            var notpadding = binStream.ReadInt32(); // NOT PADDING. FUCK. Probably effects.
            Helpers.readInt32Array(binStream, notpadding);
            var keyRegCount = binStream.ReadInt32();
            var keyLow = 0; // For region spanning. 
            JInstrumentKey[] keys = new JInstrumentKey[0x81]; // Always go for one more.
            for (int i = 0; i < keyRegCount; i++) // Loop through all pointers.
            {
                var bkey = readKeyRegionV2(binStream, Base); // Read the key region
                //Console.WriteLine("KREG BASE KEY {0}", bkey.baseKey);
                for (int b = 0; b < bkey.baseKey - keyLow; b++)
                {
                    //  They're key regions, so we're going to have a toothy / gappy piano. So we need to span the missing gaps with the previous region config.
                    // This means that if the last key was 4, and the next key was 8 -- whatever parameters 4 had will span keys 4 5 6 and 7. 
                    keys[b + keyLow] = bkey; // span the keys
                    keys[127] = bkey;
                }
                keyLow = bkey.baseKey; // Store our last key 
            }
            newInst.Keys = keys;

            newInst.Volume = binStream.ReadSingle(); // ^^
            newInst.Pitch = binStream.ReadSingle(); // Pitch and volume come last???
            // WE HAVE READ EVERY BYTE IN THE INST, WOOO
            return newInst;
        }




        /* 
          JAIV2 KeyRegion Structure
          0x00 byte baseKey 
          0x01 byte[0x3] unused;
          0x04 int32 velocityRegionCount
          VelocityRegion[velocityRegionCount] velocities; // NOTE THESE ARENT POINTERS, THESE ARE ACTUAL OBJECTS.
        */

        public static JInstrumentKey readKeyRegionV2(BeBinaryReader binStream, int Base)
        {
            JInstrumentKey newKey = new JInstrumentKey();
            newKey.Velocities = new JInstrumentKeyVelocity[0x81]; // Create region array
            //-------
            //Console.WriteLine(binStream.BaseStream.Position);
            newKey.baseKey = binStream.ReadByte(); // Store base key
            binStream.BaseStream.Seek(3, SeekOrigin.Current); ; // Skip 3 bytes
            var velRegCount = binStream.ReadInt32(); // Grab vel region count
            var velLow = 0;  // Again, these are regions -- see LoadInstrument for this exact code ( a few lines above ) 
            for (int i = 0; i < velRegCount; i++)
            {
                var breg = JInstrument.readKeyVelRegionV2(binStream, Base);  // Read the vel region.

                for (int b = 0; b < breg.baseVel - velLow; b++)
                {
                    //  They're velocity regions, so we're going to have a toothy / gappy piano. So we need to span the missing gaps with the previous region config.
                    // This means that if the last key was 4, and the next key was 8 -- whatever parameters 4 had will span keys 4 5 6 and 7. 
                    newKey.Velocities[b] = breg;
                    newKey.Velocities[127] = breg;
                }
                velLow = breg.baseVel;
            }
            return newKey;
        }



        /* JAIV1 OSCT Structure
            0x00 int32 0x4F534354 'OSCT'
            0x04 int32 SectionLength (+8 for entire section)
            0x08 int32 OscillatorCouint       
        */

        public static void loadBankOscTableV2(BeBinaryReader binStream, int Base, int EnvTableOffset)
        {
            if (binStream.ReadInt32() != OSCT) // Check if it has the oscillator table header
                throw new InvalidDataException("Oscillator table section started with unexpected data " + binStream.BaseStream.Position); // Throw if it doesn't
            binStream.ReadInt32(); // This is the section length, its mainly used for seeking the file so we won't touch it.
            var count = binStream.ReadInt32(); // Read the count
            bankOscillators = new JOscillator[count]; // Initialize the bank oscillators with the number of oscillators int he table
            for (int i = 0; i < count; i++) // Loop through each onne
            {
                var returnPos = binStream.BaseStream.Position; // Save our position, the oscillator load function destroys our position.
                bankOscillators[i] = loadOscillatorV2(binStream, EnvTableOffset + Base); // Ask the oscillator to load
                binStream.BaseStream.Position = returnPos + 0x1c;// Oscillatrs are 0x1c in length, so advance to the next osc.
            }
        }



        // We have to have this LUT for this function for JAIV2, because all the effect targets are shifted down one. 
        // Instead of rewriting the entire oscillator section for this, we can just make this LUT to convert them to the JAIV1 formats.
        // Really strange change.
        public static JOscillatorTarget[] OscillatorTargetConversionLUT = new JOscillatorTarget[]
        {
            JOscillatorTarget.Volume, // 0 
            JOscillatorTarget.Pitch, // 1 
            JOscillatorTarget.Pan, // 2 
            JOscillatorTarget.FX, // 3
            JOscillatorTarget.Dolby, // 4
        };

        /*
          JAIV2 Oscillator Structure
          0x00 - int32 0x4F736369 'Osci'
          0x04 - byte mode 
          0x05 - byte[3] unknown
          0x08 - float rate
          0x0c - int32 attackVectorOffset (RELATIVE TO ENVT + 0x08)
          0x10 - int32 releaseVectorOffset (RELATIVE TO ENVT + 0x08)
          0x14 - float width
          0x18 - float vertex
       */

        /* NOTE THAT THESE OSCILLATORS HAVE THE SAME FORMAT AS JAIV1, HOWEVER THE VECTORS ARE IN THE ENVT */
        public static JOscillator loadOscillatorV2(BeBinaryReader binStream, int EnvTableBase)
        {
            var Osc = new JOscillator(); // Create new oscillator           
            if (binStream.ReadInt32() != Osci) // Read first 4 bytes
                throw new InvalidDataException("Oscillator format is invalid. " + binStream.BaseStream.Position);

            var target = binStream.ReadByte(); // load target -- what is it affecting?
            binStream.BaseStream.Seek(3, SeekOrigin.Current); // read 3 bytes?
            Osc.rate = binStream.ReadSingle(); // Read the rate at which the oscillator progresses -- this will be relative to the number of ticks per beat.
            var attackSustainTableOffset = binStream.ReadInt32(); // Offset of AD table
            var releaseDecayTableOffset = binStream.ReadInt32(); // Offset of SR table
            Osc.Width = binStream.ReadSingle(); // We should load these next, this is the width, ergo the value of the oscillator at 32768. 
            Osc.Vertex = binStream.ReadSingle();  // This is the vertex, the oscillator will always cross this point. 
            // To determine the value of an oscillator, it's Vertex + Width*(value/32768) -- each vector should progress the value, depending on the mode. 
            // We need to add + 8 to the offsets, because the pointers are the offset based on where the data starts, not the section
            if (attackSustainTableOffset > 0) // first is AS table
            {
                binStream.BaseStream.Position = attackSustainTableOffset + EnvTableBase + 8; // Seek to the vector table
                Osc.envelopes[0] = JEnvelope.readEnvelopeV2(binStream, EnvTableBase + 8); // Load the table
            }
            if (releaseDecayTableOffset > 0) // Next is RD table
            {
                binStream.BaseStream.Position = releaseDecayTableOffset + EnvTableBase + 8; // Seek to the vector and load it
                Osc.envelopes[1] = JEnvelope.readEnvelopeV2(binStream, EnvTableBase + 8); // loadddd
            }
            Osc.target = OscillatorTargetConversionLUT[target];
            return Osc;
        }



    }
}
