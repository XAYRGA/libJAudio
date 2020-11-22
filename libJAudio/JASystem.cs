using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Be.IO;
using libJAudio;
using System.Reflection.Metadata.Ecma335;

namespace libJAudio
{
    public class JASystem : JBaseObject
    {
  
        public JIBank[] Banks = new JIBank[0xFF];
        public JWaveSystem[] WaveBanks = new JWaveSystem[0xFF];
        public JAIInitType version;


        public static object loadJASystem(ref byte[] data)
        {
            JASystem newJA;
            var version = checkVersion(ref data);
            
            switch (version)
            {
                case JAIInitType.AAF:
                    var newJA1 = new JAudioArchiveFile();
                    loadJAAF(newJA1, ref data);
                    newJA = newJA1;
                    break;
  
                case JAIInitType.BAA:
                    var newJA2 = new JBinaryAudioArchive();
                    loadJBAA(newJA2, ref data);
                    newJA = newJA2;
                    break;
                case JAIInitType.BX:
                    var newJA3 = new JBxFile();
                    loadJBX(newJA3, ref data);
                    newJA = newJA3;
                    break;
                default:
                    return null;
            }
            newJA.version = version;
            return newJA;
        }


        public static void loadJBX(JBxFile JAS, ref byte[] data)
        {

            var sections = JBxFile.readStream(ref data);
            var stm = new System.IO.MemoryStream(data); // Create a stream for it
            var read = new Be.IO.BeBinaryReader(stm); // Create a reader around the stream
            var wscount = 0;
            var inscount = 0;

            for (int i = 0; i < sections.Length; i++) // Loop through the AAF
            {
                var current_section = sections[i]; // Select current section
                switch (current_section.type) // Get the type
                {
                    case JAIInitSectionType.IBNK: // Instrument bank
                        {
                            stm.Position = current_section.start; // Seek to start
                            var ibnk = JIBank.readStreamV2(read, current_section.start);
#if BX_XAYR_WRONG_INST
                            JAS.Banks[ibnk.id] = ibnk; //Push into bank array.
                            inscount++
#else
                            JAS.Banks[ibnk.id] = ibnk; //Push into bank array.
#endif
                            break;
                        }
                    case JAIInitSectionType.WSYS: // Wave System
                        {
                            stm.Position = current_section.start; // Seek to start
                            var ws = JWaveSystem.readStream(read);
#if BX_XAYR_WRONG_WSYS
                            JAS.WaveBanks[wscount] = ws; // Push to wavebanks
                            wscount++;
#else 
                            JAS.WaveBanks[ws.id] = ws;
#endif
                            break;
                        }
                    default:
                        throw new FormatException("libjAudio failed to load bx file, bx file contains more sections than just WSYS/IBNK. Even though we (somehow) loaded the BX properly, we got a section other than just the two that are possible by the format.\n Hah. This is probably the most descriptive error you've ever seen. Which is ironic because the condition to meet it should never be met. I mean, technically its possible. Cosmic ray errors exist, and those things are weird.");
                }
            }
        }

        /* AAF is safe, we know it's always going to be a V1 instrument format. */
        public static void loadJAAF(JAudioArchiveFile JAS, ref byte[] data)
        {
            var sections = JAudioArchiveFile.readStream(ref data);
            var stm = new System.IO.MemoryStream(data); // Create a stream for it
            var read = new Be.IO.BeBinaryReader(stm); // Create a reader around the stream

            for (int i = 0; i < sections.Length; i++) // Loop through the AAF
            {
                var current_section = sections[i]; // Select current section
                switch (current_section.type) // Get the type
                {
                    case JAIInitSectionType.IBNK: // Instrument bank
                        {
                            stm.Position = current_section.start; // Seek to start
                            var ibnk = JIBank.readStreamV1(read); // Load it
                            JAS.Banks[ibnk.id] = ibnk; //Push into bank array.
                            break;
                        }
                    case JAIInitSectionType.WSYS: // Wave System
                        {
                            stm.Position = current_section.start; // Seek to start
                            var ws = JWaveSystem.readStream(read);
                            JAS.WaveBanks[ws.id] = ws; // Push to wavebanks
                            break;
                        }
                    case JAIInitSectionType.SEQUENCE_COLLECTION:
                        {
                            break;
                        }
                }
            }

        }

        public static void loadJBAA(JBinaryAudioArchive JAS, ref byte[] data)
        {
            var sections = JBinaryAudioArchive.readStream(ref data);
            var stm = new System.IO.MemoryStream(data); // Create a stream for it
            var read = new Be.IO.BeBinaryReader(stm); // Create a reader around the stream

            for (int i = 0; i < sections.Length; i++) // Loop through the AAF
            {
                var current_section = sections[i]; // Select current section
                switch (current_section.type) // Get the type
                {
                    case JAIInitSectionType.IBNK: // Instrument bank -- alright seriously fuck these they can be either v1 or v2 in baa, sometimes mixed.
                        {
                            stm.Position = current_section.start; // Seek to start
                            stm.Seek(0x20, System.IO.SeekOrigin.Current); // have to detect the type.
                            var secondChunkID = read.ReadInt32();

                            JIBank ibnk;
                            //THIS IS YOUR FAULT, FOUR SWORDS ADVENTURE.
                            if (secondChunkID == 0x42414E4B) // V1 type banks always have literal `BANK` after them. 
                            {
                                stm.Position = current_section.start;
                                ibnk = JIBank.readStreamV1(read); // Load it
                                JAS.Banks[ibnk.id] = ibnk; //Push into bank array. 
                                break;
                            }

                            stm.Position = current_section.start; // Reset stream position to section base.
                                                                  // Though V2 has 'ENVT' just after it, don't check for it. It's either v1 or v2.                              
                            stm.Position = current_section.start;
                            ibnk = JIBank.readStreamV2(read, current_section.start); // Load it
                            JAS.Banks[ibnk.id] = ibnk; //Push into bank array. 
                            break;
                        }
                    case JAIInitSectionType.WSYS: // Wave System -- the same as jaiv1?
                        {
                            stm.Position = current_section.start; // Seek to start
                            var ws = JWaveSystem.readStream(read);
                            if (JAS.WaveBanks[ws.id] != null)
                            {
                                var ows = JAS.WaveBanks[ws.id]; // Merge duplicate wavetable ID's.
                                foreach (int k in ws.WaveTable.Keys)
                                {
                                    ows.WaveTable[k] = ws.WaveTable[k];
                                }
                            }
                            else
                            {
                                JAS.WaveBanks[ws.id] = ws; // Push to wavebanks
                            }
                            break;
                        }
                    case JAIInitSectionType.SEQUENCE_COLLECTION:
                        {
                            break;
                        }
                }
            }

        }

        /* This class originally had something more clever going on, Zelda Four Swords threw a giant fucking wrench in my code. */
        /* I'd not recommend using it, as it might be removed in the future if the codebase for it doesn't grow. */
        public static JAIInitType checkVersion(ref byte[] data)
        {
            var JStream = new MemoryStream(data);
            var JReader = new BeBinaryReader(JStream);
            var hdr = JReader.ReadUInt32();
            if (hdr == 1094803260) // AA_< LITERAL , opening of BAA archive or BAA Format
            {
                JReader.Close();
                JStream.Close();
                return JAIInitType.BAA; // return BAA type
            }
            else
            { /* PIKMIN BX ARCHIVE */
                /* CHECKING FOR BX 
                * This is not 100% accurate, but the likelyhood of something like this actually getting confused with AAF is slim to none.
                * Considering there's only one game that uses BX. 
               */

                JStream.Position = 0; // reset pos;
                var BXWSOffs = JReader.ReadInt32(); // should point to location in file.
                if (BXWSOffs < JStream.Length) // check if is within BX 
                {
                    JStream.Position = BXWSOffs;
                    var WSO = JReader.ReadInt32();
                    if (WSO < JStream.Length) // fall out, not valid
                    {
                        var WSYS = JReader.ReadInt32();
                        if (WSYS == 0x57535953) // 0x57535953 is literal WSYS
                        {
                            JReader.Close(); // flush / close streams
                            JStream.Close(); // flush and close streams
                            return JAIInitType.BX;
                        }
                    }
                }
            }
            // * The init type is otherwise AAF.
            {
                JReader.Close();
                JStream.Close();
                return JAIInitType.AAF; // JAIInitSection v1 doesn't have an identifying header.
            }
        }
    }

}
