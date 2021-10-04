using System;
using System.Text;

namespace ThunderED.Classes
{
    [Serializable]
    public class WebSkillItem
    {
        public string Name { get; set; }
        public int ValueActive { get; set; }
        public int ValueTrained { get; set; }
        public string Visual { get; set; }
        public bool IsCategory { get; set; }

        public void UpdateVisual()
        {
            var sb = new StringBuilder(5);
            for (var i = 0; i < 5; i++)
            {
                var color = GetSkillCellColor(i+1);
                sb.Append(
                    $"<svg width=\"16\" height=\"16\">Browser does not support SVG<rect width = \"15\" height = \"15\" style = \"fill:{color};stroke-width:1;stroke:rgb(0,0,0)\"></svg>");
            }

            Visual = sb.ToString();
        }

        private string GetSkillCellColor(int number)
        {
            if (IsCategory) ValueActive = ValueTrained = -1;
            var bgcolor = "white";
            switch (ValueTrained)
            {
                case -1:
                    return "gray";
                case 0:
                    return bgcolor;
                case var a when a < 5 && a > 2: 
                    bgcolor = number <= ValueTrained ? "yellow" : bgcolor;
                    break;
                case var a when a <= 2:
                    bgcolor = number <= ValueTrained ? "#C0C0C0" : bgcolor;
                    break;
                case 5:
                    bgcolor = "green";
                    break;
            }

            return bgcolor;
        }
    }
}
