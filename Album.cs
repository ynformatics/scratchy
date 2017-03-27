using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System.IO;

namespace Scratchy
{
    class Album
    {
        public int AlbumId { get; set; }
        public string Name { get; set; }
        int currentTrack;
        Dictionary<int, string> tracks;
        public Album(string m3ufile)
        {
            currentTrack = 1;
            tracks = new Dictionary<int, string>();
            var trackNames = File.ReadAllLines(m3ufile);
            foreach (var trackName in trackNames)
            {
                if (!trackName.StartsWith("#") && trackName.Length > 0)
                    tracks.Add(currentTrack++, trackName);
            }

            currentTrack = 0;
        }
        public string GetNextTrack()
        {
            currentTrack++;
            if (tracks.ContainsKey(currentTrack))
                return tracks[currentTrack];
            else
                return string.Empty;
        }
    }
}