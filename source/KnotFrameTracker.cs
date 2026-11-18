using System;
namespace KavrosClueHelper
{
    public sealed class KnotFrameTracker
    {
        public KnotPuzzle History {get;private set;}
        private string rawKey;
        private int frames;
        private DateTime seen;
        public bool Observe(DetectedPuzzle puzzle,DateTime now,bool live)
        {
            string key=puzzle.Key;
            frames=rawKey==key&&(now-seen).TotalMilliseconds<1500?frames+1:1;
            rawKey=key;seen=now;
            puzzle.Knot=KnotImageReader.Merge(puzzle.Knot,History);
            bool moving=live&&frames<2;
            // Keep the last settled memory while animation/rapid rotations are
            // changing. Merge considers every rotation, including skipped clicks.
            if(!moving||History==null)History=puzzle.Knot;
            return moving;
        }
        public void Missing(DateTime now)
        {rawKey=null;frames=0;if((now-seen).TotalMilliseconds>1200)History=null;}
        public void Reset(){History=null;rawKey=null;frames=0;seen=DateTime.MinValue;}
    }
}
