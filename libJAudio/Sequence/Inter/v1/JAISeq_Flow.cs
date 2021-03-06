﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace libJAudio.Sequence.Inter
{
    public partial class JAISeqInterpreter
    {
        public JAISeqEvent ProcessFlowOps(byte currnet_opcode)
        {
            switch (currnet_opcode)
            {
                /* Open and close track */
                case (byte)JAISeqEvent.OPEN_TRACK:
                    {
                        rI[0] = Sequence.ReadByte();
                        rI[1] = (int)Helpers.ReadUInt24BE(Sequence);  // Pointer to track inside of BMS file (Absolute) 
                        return JAISeqEvent.OPEN_TRACK;
                    }
                case (byte)JAISeqEvent.OPEN_TRACK_BROS:
                    var w = Sequence.ReadByte();
                    return JAISeqEvent.OPEN_TRACK_BROS;
                case (byte)JAISeqEvent.FIN:
                    {
                        return JAISeqEvent.FIN;
                    }

                /* Delays and waits */
                case (byte)JAISeqEvent.WAIT_16: // Wait (UInt16)
                    {
                        var delay = Sequence.ReadUInt16(); // load delay into ir0                  
                        rI[0] = delay;
                        return JAISeqEvent.WAIT_16;
                    }
                case (byte)JAISeqEvent.WAIT_VAR: // Wait (VLQ) see readVlq function. 
                    {
                        var delay = Helpers.ReadVLQ(Sequence); // load delay into ir0
                        rI[0] = delay;
                        return JAISeqEvent.WAIT_VAR;
                    }
                case (byte)JAISeqEvent.WAIT_REGISTER:
                    {
                        var register = Sequence.ReadByte();
                        rI[0] = register;
                        return JAISeqEvent.WAIT_REGISTER;
                    }
                /* Logical jumps */
                case (byte)JAISeqEvent.JUMP: // Unconditional jump
                    {
                        rI[0] = 0; // No condition, r0
                        var addr = Sequence.ReadInt32(); // Absolute address r1
                        //jump(addr);
                        rI[1] = addr;
                        return JAISeqEvent.JUMP;
                    }
                case (byte)JAISeqEvent.JUMP_CONDITIONAL: // Jump based on mode
                    {
                        byte flags = Sequence.ReadByte(); // Read flags.
                        var condition = flags & 15; // last nybble is condition. 
                        var addr = (int)Helpers.ReadUInt24BE(Sequence); // pointer, push to ir1
                        rI[0] = flags;
                        rI[1] = addr;
                        return JAISeqEvent.JUMP_CONDITIONAL;
                    }
                case (byte)JAISeqEvent.RETURN_CONDITIONAL:
                    {
                        var cond = Sequence.ReadByte(); // Read condition byte
                        rI[0] = cond; // Store the condition in ir0
                        return JAISeqEvent.RETURN_CONDITIONAL;
                    }
                case (byte)JAISeqEvent.CALL_CONDITIONAL:
                    {
                        var cond = Sequence.ReadByte();
                        var addr = (int)Helpers.ReadUInt24BE(Sequence);
                        rI[0] = cond; // Set to condition
                        rI[1] = addr; // set ir1 to address jumped
                        return JAISeqEvent.CALL_CONDITIONAL;
                    }
                case (byte)JAISeqEvent.CALL:
                    {
                        var addr = (int)Helpers.ReadUInt24BE(Sequence);
                        rI[0] = addr; // Set address
                        return JAISeqEvent.CALL;
                    }
                case (byte)JAISeqEvent.RETURN:
                    {
                        return JAISeqEvent.RETURN;
                    }
            }
            return JAISeqEvent.UNKNOWN;
        }
    }
}
