using System;
using System.Drawing;
namespace KavrosClueHelper
{
    public sealed class ClueRetention
    {
        public DetectedPuzzle Saved {get;private set;}
        private string dismissed;
        public void Accept(DetectedPuzzle p)
        {
            if(p==null)return;
            if(p.Key!=dismissed)dismissed=null;
            if((p.Kind==PuzzleKind.EmoteClue||p.Kind==PuzzleKind.Scan)&&p.Key!=dismissed)Saved=p;
            else if(p.Kind!=PuzzleKind.EmoteClue&&p.Kind!=PuzzleKind.Scan)Saved=null;
        }
        public DetectedPuzzle Display(DetectedPuzzle active)
        {return active!=null?(active.Key==dismissed?null:active):Saved;}
        public void Missing(){dismissed=null;}
        public void Clear(DetectedPuzzle active=null){Saved=null;dismissed=active==null?null:active.Key;}
        public void Reset(){Saved=null;dismissed=null;}
    }
}

