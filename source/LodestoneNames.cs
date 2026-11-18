using System;
namespace KavrosClueHelper
{
    public static class LodestoneNames
    {
        public static string Clean(string name)
        {
            if(name==null)return "";
            name=name.Replace("\u00c3\u0082\u00c2\u00b4", "'").Replace("\u00c2\u00b4", "'")
                .Replace('\u00b4', '\'').Replace('`', '\'').Replace('\u2019', '\'').Replace('\u2018', '\'');
            foreach(string suffix in new[]{" lodestone", " lodestone (Kavros)"})
            {
                if(name=="Draynor"+suffix)return "Draynor Village"+suffix;
                if(name=="Wilderness"+suffix)return "Wilderness Crater"+suffix;
            }
            return name;
        }
    }
}
