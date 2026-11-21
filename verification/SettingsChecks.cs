using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using KavrosClueHelper;

public sealed class SettingsChecks : MarshalByRefObject
{
    [STAThread]
    public static int Main(string[] args)
    {
        var setup=new AppDomainSetup{ApplicationBase=System.IO.Path.GetFullPath(args[0])};
        AppDomain domain=AppDomain.CreateDomain("Packaged settings",null,setup);
        try
        {
            var worker=(SettingsChecks)domain.CreateInstanceFromAndUnwrap(
                Assembly.GetExecutingAssembly().Location,typeof(SettingsChecks).FullName);
            worker.Check();
            return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
        finally{AppDomain.Unload(domain);}
    }
    public void Check()
    {
        Application.EnableVisualStyles();
        using(var form=new PuzzleSuiteForm(false))
        {
            var footer=form.Controls.Find("BuildCredit",true).OfType<Label>().Single();
            var crest=form.Controls.Find("KavrosCrest",true).OfType<PictureBox>().Single();
            if(!footer.Text.Contains("v0.7.13")||crest.Image==null||form.Icon==null)throw new Exception("Missing branding or incorrect version.");
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var option=(CheckBox)typeof(PuzzleSuiteForm).GetField("wholeRowClicks",flags).GetValue(form);
            var summary=(Label)typeof(PuzzleSuiteForm).GetField("summary",flags).GetValue(form);
            for(int i=0;i<3;i++){
                if(option.Parent!=summary.Parent||option.Bottom>summary.Top||option.GetPreferredSize(Size.Empty).Height>option.Height)
                    throw new Exception("Settings text clipped at scale "+i);
                Console.WriteLine("Settings checkbox ends at "+option.Bottom+", next label starts at "+summary.Top);
                form.Scale(new SizeF(i==0?1.25f:1.2f,i==0?1.25f:1.2f));
            }
            Console.WriteLine("PASS Settings branding and checkbox layout at 100%, 125% and 150%.");
        }
    }
}
