using System;
using System.Collections.Generic;
using System.IO;

namespace Scratchy
{
    class Database
    {
        Dictionary<uint, List<ulong>> database = new Dictionary<uint, List<ulong>>();

        Dictionary<uint, string> idToName = new Dictionary<uint, string>();
        Dictionary<string, uint> nameToId = new Dictionary<string, uint>();
        uint nextId = 0;

        static String fingerprintDir = Android.OS.Environment.GetExternalStoragePublicDirectory("Scratchy") + "/";
        static String fingerprintFile = fingerprintDir + "fingerprints.txt";

        public uint NameToId(string name)
        {
            nextId++;
            nameToId[name] = nextId;
            idToName[nextId] = name;
            return nextId;
        }
        public void AddFingerprints(List<Fingerprint> fps)
        {
            foreach (var fp in fps)
            {
                AddFingerprint(fp);
            }
        }

        void AddFingerprint(Fingerprint fp)
        {
            if (!database.ContainsKey(fp.Address))
                database.Add(fp.Address, new List<ulong>());

            database[fp.Address].Add(fp.Location);
        }

        public string GetMatch(IEnumerable<Fingerprint> sampleFPs)
        {
            try
            {
                Dictionary<uint, List<Tuple<int, int>>> offsets = new Dictionary<uint, List<Tuple<int, int>>>();

                Console.WriteLine("GetMatch Start");
                var albumFPs = new List<Fingerprint>();
                foreach (var sampleFP in sampleFPs)
                {
                    var address = sampleFP.Address;
                    if (database.ContainsKey(address))
                    {
                        foreach (var loc in database[address])
                        {
                            var albumId = Fingerprint.AlbumIdPart(loc);

                            if (!offsets.ContainsKey(albumId))
                                offsets.Add(albumId, new List<Tuple<int, int>>());

                            offsets[albumId].Add(new Tuple<int, int>(Fingerprint.AnchorTimePart(loc), sampleFP.AnchorTime));
                        }
                    }

                }

                Dictionary<uint, Dictionary<int, int>> deltas = new Dictionary<uint, Dictionary<int, int>>();
                foreach (var entry in offsets)
                {
                    var albumId = entry.Key;

                    foreach (var pair in entry.Value)
                    {

                        var delta = pair.Item2 - pair.Item1;
                        if (!deltas.ContainsKey(albumId))
                            deltas.Add(albumId, new Dictionary<int, int>());
                        if (!deltas[albumId].ContainsKey(delta))
                            deltas[albumId].Add(delta, 1);
                        else
                            deltas[albumId][delta]++;

                    }
                }

                var fname = fingerprintDir + DateTime.Now.ToString("s") + ".log";
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(fname))
                {
                    foreach (var d in deltas)
                    {
                        file.Write(d.Key); file.Write(","); file.WriteLine(idToName[d.Key]);

                        foreach (var p in d.Value)
                        {
                            file.Write(p.Key);
                            file.Write(",");
                            file.WriteLine(p.Value);
                        }
                    }
                }
              
                uint bestEntry = 0;
                int maxDeltaCount = 0;
                int maxEverDeltaCount = 0;

                foreach (var entry in deltas)
                {
                    maxDeltaCount = 0;
                    foreach (var count in entry.Value)
                    {
                        if (count.Value > maxDeltaCount)
                        {
                            maxDeltaCount = count.Value;
                        }
                    }

                    if (maxDeltaCount > maxEverDeltaCount)
                    {
                        maxEverDeltaCount = maxDeltaCount;
                        bestEntry = entry.Key;
                    }
                }

                Console.WriteLine("GetMatch End");
                return bestEntry == 0 ? null : idToName[bestEntry];
            }
            catch
            {
                return null;
            }
        }

        public void Save()
        {
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(fingerprintFile))
            {
                foreach(var entry in nameToId)
                {
                    file.Write("N"); file.Write(',');
                    file.Write(entry.Key); file.Write(',');
                    file.Write(entry.Value); file.WriteLine();
                }

                foreach (var entry in database)
                {
                    file.Write(entry.Key);
                    file.Write(',');
                    foreach (var dp in entry.Value)
                    {
                        file.Write(dp);
                        file.Write(',');
                    }
                    file.WriteLine();
                }
            }
        }

        public void Load()
        {
            var a = Android.OS.Environment.GetExternalStoragePublicDirectory("Scratchy/Music") + "/";

            if (!File.Exists(fingerprintFile))
            {
                Console.WriteLine("Fingerprint file not found:" + fingerprintFile);
                return;
            }

            database.Clear();

            using (System.IO.StreamReader file = new System.IO.StreamReader(fingerprintFile))
            {
                while (!file.EndOfStream)
                {
                    var line = file.ReadLine();
                    var items = line.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    if (items[0] == "N")
                    {
                        var name = items[1];
                        var id = uint.Parse(items[2]);

                        nameToId[name] = id;
                        idToName[id] = name;
                        nextId = Math.Max(nextId, id);
                    }
                    else
                    {
                        var dps = new List<ulong>();

                        for (int i = 1; i < items.Length; i++)
                        {
                            dps.Add(ulong.Parse(items[i]));
                        }

                        uint key = uint.Parse(items[0]);
                        if (database.ContainsKey(key))
                            Console.WriteLine("Duplicate database key:" + key);
                        else
                            database.Add(key, dps);
                    }
                }
            }
        }
    }
}