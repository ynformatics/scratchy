namespace Scratchy
{
    class Fingerprint
    {
        public uint Address { get; set; }
        public ulong Location { get; set; }
        public Fingerprint(uint address, ulong location)
        {
            Address = address;
            Location = location;
        }
        public Fingerprint(uint albumId, uint anchorTime, uint anchorFreq, uint targetTime, uint targetFreq)
        {
            Location = (ulong)anchorTime << 32 | albumId;

            Address = anchorFreq << 23 | targetFreq << 14 | targetTime - anchorTime;
        }
        public int AnchorTime {  get { return (int)(Location >> 32); } }
        public int TimeDelta {  get {  return (int)(Address & 0x2FFF); } }
        public uint AlbumId {   get {  return  (uint)(Location & 0xFFFFFFFF); } }

        public static int AnchorTimePart(ulong location)
        {
            return (int)(location >> 32);
        }
        public static uint AlbumIdPart(ulong location)
        {
            return (uint)(location & 0xFFFFFFFF);
        }

    }
}