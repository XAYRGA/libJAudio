using System;
using System.Collections.Generic;
using System.Text;

namespace libJAudio
{
    public class JRandEffect : JBaseObject
    {
        public int eBase;
        public int eDistance;
    }


    public class JSensorEffect : JBaseObject
    {
        public byte trigger;
        public byte key;
        public int low;
        public int high;
    }
}
