using Be.IO;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace libJAudio
{

    /* 
     
    0x00 int32 0x42535420
    0x04 int32 0x00000000
    0x08 int32 0x01000000
    0x0C int32 groupTableOffset
    groupTable
    0x00 int32 count 
    0x04 int32*[count] groupSections
    groupSections
    0x00 int32 count 
    0x04 int32 0x00000000; (name pointer)
    0x08 waveDescriptor[count];
    waveDescriptor
    0x00 byte format 
    0x01 uint24* waveInfo*; 
    (varies on format) 
    waveinfo* 
    */

    #region BSTN
    public class JBSTN
    {
        private const int HEAD = 0x4253544E;
        public BSTNSection[] sections;
        public int version;

        public static JBSTN readStream(BeBinaryReader br)
        {
            var Base = (int)br.BaseStream.Position;
            var newBSTN = new JBSTN();
            var head = br.ReadInt32();
            if (head != HEAD)
                throw new InvalidDataException($"Unexpected BSTN header! {head} != {HEAD}");
            br.ReadInt32(); // Skip, alignment. 
            var version = br.ReadInt32();
            if (version != 0x01000000)
                throw new InvalidDataException($"Version is not 0x01000000! ({version})");
            newBSTN.version = version;

            var sectionTableOffset = br.ReadInt32();
            br.BaseStream.Position = sectionTableOffset;  // seek to group table position

            var sectionCount = br.ReadInt32();
            var sectionPointers = Helpers.readInt32Array(br, sectionCount);
            newBSTN.sections = new BSTNSection[sectionCount];

            var anch = br.BaseStream.Position;
            for (int i = 0; i < sectionPointers.Length; i++)
            {
                var sectLocation = sectionPointers[i] + Base;
                br.BaseStream.Position = sectLocation;
                newBSTN.sections[i] = BSTNSection.readStream(br, Base);
            }
            return newBSTN;
        }
    }


    public struct BSTNSection
    {
        public string name;
        public BSTNGroup[] groups;
        public int count;

        public static BSTNSection readStream(BeBinaryReader br, int Base)
        {
            var newSect = new BSTNSection();
            newSect.count = br.ReadInt32();
            var nameOffset = br.ReadInt32();
            newSect.groups = new BSTNGroup[newSect.count];
            var groupPointers = Helpers.readInt32Array(br, newSect.count);
            br.BaseStream.Position = nameOffset + Base;
            newSect.name = JBST.readTerminated(br, 0x00);
            //Console.WriteLine(newSect.name);
            for (int i = 0; i < groupPointers.Length; i++)
            {
                br.BaseStream.Position = groupPointers[i] + Base;
                newSect.groups[i] = BSTNGroup.readStream(br, Base);
            }
            return newSect;
        }
    }

    public struct BSTNGroup
    {
        public string name;
        public string[] waves;

        public static BSTNGroup readStream(BeBinaryReader br, int Base)
        {
            var newSect = new BSTNGroup();
            var count = br.ReadInt32();
            var nameOffset = br.ReadInt32();

            var banch = br.BaseStream.Position;
            br.BaseStream.Position = nameOffset + Base;
            newSect.name = JBST.readTerminated(br, 0x00);
            // Console.WriteLine($"->\t{newSect.name}");
            br.BaseStream.Position = banch;
            newSect.waves = new string[count];
            for (int i = 0; i < count; i++)
            {
                var anch = br.BaseStream.Position + 4;
                var ofs = br.ReadInt32();
                br.BaseStream.Position = ofs + Base;
                newSect.waves[i] = JBST.readTerminated(br, 0x00);
                //Console.WriteLine($"\t->\t{newSect.waves[i]}");
                br.BaseStream.Position = anch;
            }
            return newSect;
        }
    }

    #endregion
    #region BST

    public class JBST
    {
        private const int BST_HEAD = 0x42535420;
        public string name;
        public int version;
        public int sectionCount;
        public BSTSection[] sections;

        public static string readTerminated(BeBinaryReader rd, byte term)
        {
            int count = 0;
            int nextbyte = 0xFFFFFFF;
            byte[] name = new byte[0xFF];
            while ((nextbyte = rd.ReadByte()) != term)
            {
                name[count] = (byte)nextbyte;
                count++;
            }
            return Encoding.ASCII.GetString(name, 0, count);
        }

        public void map_bstn(JBSTN bstn)
        {
            var w = this;
            for (int sid = 0; sid < w.sections.Length; sid++)
            {
                w.sections[sid].name = bstn.sections[sid].name;
                for (int gid = 0; gid < w.sections[sid].groups.Length; gid++)
                {
                    w.sections[sid].groups[gid].name = bstn.sections[sid].groups[gid].name;
                    for (int wid = 0; wid < w.sections[sid].groups[gid].waves.Length; wid++)
                    {
                        w.sections[sid].groups[gid].waves[wid].name = bstn.sections[sid].groups[gid].waves[wid];
                    }
                }
            }
        }


        public static JBST readStream(BeBinaryReader br)
        {
            int Base = (int)br.BaseStream.Position;
            var newBST = new JBST();
            var head = br.ReadInt32();
            if (head != BST_HEAD)
                throw new InvalidDataException($"Unexpected BST header! {head} != {BST_HEAD}");
            br.ReadInt32(); // Skip, alignment. 
            var version = br.ReadInt32();
            if (version != 0x01000000)
                throw new InvalidDataException($"Version is not 0x01000000! ({version})");
            newBST.version = version;

            var sectionTableOffset = br.ReadInt32();
            br.BaseStream.Position = sectionTableOffset + Base;  // seek to group table position

            var sectionCount = br.ReadInt32();
            var sectionPointers = Helpers.readInt32Array(br, sectionCount);
            newBST.sections = new BSTSection[sectionCount];

            var anch = br.BaseStream.Position;
            for (int i = 0; i < sectionPointers.Length; i++)
            {
                br.BaseStream.Position = sectionPointers[i] + Base;
                newBST.sections[i] = BSTSection.readStream(br,Base);
            }
            return newBST;
        }
    }


    public struct BSTGroup
    {
        public string name;
        public BSTWaveInfo[] waves;
        public static BSTGroup readStream(BeBinaryReader br, int Base)
        {
            var newSect = new BSTGroup();
            var count = br.ReadInt32();
            br.ReadInt32(); // Alignment (skip 4 bytes, always 0);
            newSect.waves = new BSTWaveInfo[count];
            for (int i = 0; i < count; i++)
            {
                var anch = br.BaseStream.Position + 4;  // Return to the section in front of the current one 
                var type = br.ReadByte(); // Describes type
                var addr = Helpers.ReadUInt24BE(br);
                br.BaseStream.Position = addr + Base;
                newSect.waves[i] = BSTWaveInfo.readStream(br, type, Base);

                br.BaseStream.Position = anch;
            }
            return newSect;
        }
    }

    public struct BSTSection
    {
        public int count;
        public BSTGroup[] groups;
        public string name;
        public static BSTSection readStream(BeBinaryReader br, int Base)
        {
            //Console.WriteLine($"Section {br.BaseStream.Position:X}");
            var newSect = new BSTSection();
            newSect.count = br.ReadInt32();
            newSect.groups = new BSTGroup[newSect.count];
            var groupPointers = Helpers.readInt32Array(br, newSect.count);
            for (int i = 0; i < groupPointers.Length; i++)
            {
                br.BaseStream.Position = groupPointers[i] + Base;
                newSect.groups[i] = BSTGroup.readStream(br, Base);
            }
            return newSect;
        }
    }

    public struct BSTWaveInfo
    {
        public int format;
        public string streamFilePath;
        public int bankID;
        public int wSoundID;
        public string name;
        public int seqSectID;
        public int seqFXID;
        public int flags;
        public byte streamFormat;
        public int unk1;

        public static BSTWaveInfo readStream(BeBinaryReader br, int fmt, int Base)
        {
            var newWI = new BSTWaveInfo();
            newWI.format = fmt;
            switch (fmt & 0xF0)
            {
                case 0x40:
                    break;
                case 0x50:
                    // Console.WriteLine($"{br.BaseStream.Position:X}");
                    newWI.wSoundID = br.ReadInt16();
                    // Console.WriteLine(newWI.wSoundID);
                    break;
                case 0x60:
                    break;
                case 0x70:
                    newWI.streamFormat = br.ReadByte();
                    newWI.unk1 = br.ReadByte();
                    newWI.flags = br.ReadUInt16();
                    var namePointer = br.ReadInt32();
                    br.BaseStream.Position = namePointer + Base;
                    newWI.streamFilePath = JBST.readTerminated(br, 0x00);
                    break;
            }
            return newWI;
        }
    }
    #endregion
    #region BSC

    public struct JBSCGroup
    {
        public int count;
        public int[] sequenceOffsets;
        public static JBSCGroup readStream(BeBinaryReader br)
        {
            var length = br.ReadInt32();
            var seqPointers = Helpers.readInt32Array(br, length);
            var newGroup = new JBSCGroup();
            newGroup.count = length;
            newGroup.sequenceOffsets = seqPointers;
            return newGroup;
        }

        private static byte[] readUntilSequenceEnd(BeBinaryReader br)
        {
            var len = 0;
            int SEQ_OPCODE = 0xFF00FF;
            var anchor = br.BaseStream.Position;
            try
            {
                while ((SEQ_OPCODE = br.ReadUInt16()) != 0xFFC1 && SEQ_OPCODE != 0xFFC3) // fuck so hard. 
                {
                    br.BaseStream.Position -= 1; // fuck alignment, seek back one byte then read the next short in the buffer lol.
                    len++;
                }
            }
            catch (Exception E) { Console.WriteLine($"Terminated loop... {E.Message}"); }
            br.BaseStream.Position = anchor;
            len += 1;
            byte[] ret = new byte[len];
            br.BaseStream.Read(ret, 0, len);
            return ret;
        }
    }


    public class JBSC
    {
        public int size;
        public JBSCGroup[] groups;
        public static JBSC readStream(BeBinaryReader br, JBST jbst)
        {
            br.ReadInt32(); // skip head lol
            var newBSC = new JBSC();
            newBSC.size = br.ReadInt32();
            newBSC.groups = new JBSCGroup[jbst.sections[0].groups.Length];
            var sectionPointers = Helpers.readInt32Array(br, jbst.sections[0].groups.Length);
            for (int i = 0; i < newBSC.groups.Length; i++)
            {
                br.BaseStream.Position = sectionPointers[i];
                Console.WriteLine(sectionPointers[i]);
                newBSC.groups[i] = JBSCGroup.readStream(br);
            }
            return newBSC;
        }
    }
    #endregion
    #region ASN 
    public class JASNWave
    {
        public string name;
        public ushort mode;
        public ushort id;
        public int index;
    }

    public class JASNCategory
    {
        public string name;
        public ushort id;
        public ushort index;
        public JASNWave[] waves;
    }

    public class JASNTable
    {

        public JASNCategory[] categories = new JASNCategory[0x12]; // hardcoded 

        private static string readName(BeBinaryReader aafRead)
        {
            var ofs = aafRead.BaseStream.Position; // Store where we started 
            byte nextbyte; // Blank byte
            byte[] name = new byte[0x70]; // Array for the name

            int count = 0; // How many we've done
            while ((nextbyte = aafRead.ReadByte()) != 0xFF & nextbyte != 0x00) // Read until we've read 0 or FF
            {
                name[count] = nextbyte; // Store into byte array
                count++; // Count  how many valid bytes  we've read.
            }
            aafRead.BaseStream.Seek(ofs + 0x1C, SeekOrigin.Begin); // Seek 0x1C bytes, because thats the statically allocated space for the wavegroup path. 
            return Encoding.ASCII.GetString(name, 0, count); // Return a string with the name, but only of the valid bytes we've read. 
        }

        public static JASNTable readStream(BeBinaryReader br)
        {
            var Base = br.BaseStream.Position;
            var NewTable = new JASNTable();
            br.ReadBytes(0xE); // skip 0xD bytes
            var waveCount = br.ReadUInt16();
            for (int i = 0; i < 0x12; i++)
            {
                var cat = new JASNCategory();
                cat.name = readName(br);
                var count = br.ReadUInt16();
                cat.id = br.ReadUInt16();
                cat.waves = new JASNWave[count];
                cat.index = (ushort)i;
                NewTable.categories[i] = cat;
            }

            var catSorted = new JASNCategory[0x12];
            Array.Copy(NewTable.categories, catSorted, 0x12);
            for (int i = 0; i < 0x12; i++) // a third __fucking iteration__ on these stupid vectors.
            {
                for (int j = 0; j < 0x12; j++)
                {
                    var current = catSorted[i]; // Grab current oscillator vector, notice the for loop starts at 1
                    var cmp = catSorted[j]; // Grab the previous object
                    if (cmp.id > current.id) // if its time is greater than ours
                    {
                        catSorted[j] = current; // shift us down
                        catSorted[i] = cmp; // shift it up
                    }
                }
            }

            int totalOffset = 0;
            foreach (JASNCategory cat in catSorted)
                for (int i = 0; i < cat.waves.Length; i++)
                {
                    var newWave = new JASNWave()
                    {
                        name = readName(br),
                        mode = br.ReadUInt16(),
                        id = br.ReadUInt16(),
                        index = totalOffset,
                    };
                    cat.waves[i] = newWave;
                    totalOffset++;
                }

            return NewTable;
        }
    }
    #endregion
}
