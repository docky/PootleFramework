using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using UnityEngine;
namespace Docky.BodyShapeMotion.Editor
{
    public static class BodyShapeAudit
    {
        public static string Run(BodyShapeLab lab)
        {
            if(lab.samples.Length!=64 || lab.samples.Any(s=>!s || !s.clip || !s.GetComponent<BodyShapeAdjustment>().profile))throw new System.InvalidOperationException("The audit needs all 64 samples with clips and shape profiles.");
            int focus=lab.focusRow;bool only=lab.focusOnly;lab.Focus(focus,false);
            var csv=new StringBuilder("clip,strength,mean_hand_proxy_depth_m,max_hand_proxy_depth_m,mean_shoe_proxy_depth_m,max_shoe_proxy_depth_m,max_tip_displacement_m\n");
            try
            {
                foreach(var sample in lab.samples.OrderBy(s=>s.row).ThenBy(s=>s.column))
                {
                    var correction=sample.GetComponent<BodyShapeAdjustment>();float hands=0,feet=0,maxHand=0,maxFeet=0,maxMove=0;sample.Initialize();
                    for(int i=0;i<48;i++)
                    {
                        sample.Sample(sample.clip.length*(i+.5f)/48,0,true);
                        hands+=correction.HandPenetration;feet+=correction.FootPenetration;maxHand=Mathf.Max(maxHand,correction.HandPenetration);maxFeet=Mathf.Max(maxFeet,correction.FootPenetration);maxMove=Mathf.Max(maxMove,correction.MaxDisplacement);
                    }
                    var values=new[]{correction.strength,hands/48,maxHand,feet/48,maxFeet,maxMove};
                    csv.AppendLine(sample.clip.name+","+string.Join(",",values.Select(v=>v.ToString("G8",CultureInfo.InvariantCulture))));sample.Stop();
                }
                string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Builds","BodyShapeMotion"));Directory.CreateDirectory(folder);
                string path=Path.Combine(folder,"doggy-overlap-audit.csv");File.WriteAllText(path,csv.ToString());return path;
            }
            finally {lab.Focus(focus,only);DoggyBodyShapeLabBuilder.Preview(lab,lab.phase);}
        }
    }
}
